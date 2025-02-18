﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using I18Next.Net.Backends;
using I18Next.Net.Internal;
using Microsoft.Extensions.Logging;
using I18Next.Net.TranslationTrees;

namespace I18Next.Net.Plugins;

public class DefaultTranslator : ITranslator
{
    private readonly ITranslationBackend _backend;
    private readonly IInterpolator _interpolator;
    private readonly ILogger _logger;
    private readonly IPluralResolver _pluralResolver;

    private readonly ConcurrentDictionary<string, ITranslationTree> _treeCache = new();

    public DefaultTranslator(ITranslationBackend backend, ILogger logger, IPluralResolver pluralResolver, IInterpolator interpolator)
    {
        _backend = backend;
        _logger = logger;
        _pluralResolver = pluralResolver;
        _interpolator = interpolator;
    }

    public DefaultTranslator(ITranslationBackend backend)
    {
        _backend = backend;
        _logger = new TraceLogger();
        _pluralResolver = new DefaultPluralResolver();
        _interpolator = new DefaultInterpolator(_logger);
    }

    public DefaultTranslator(ITranslationBackend backend, IInterpolator interpolator)
    {
        _backend = backend;
        _logger = new TraceLogger();
        _pluralResolver = new DefaultPluralResolver();
        _interpolator = interpolator;
    }

    public bool AllowInterpolation { get; set; } = true;

    public bool AllowNesting { get; set; } = true;

    public bool AllowPostprocessing { get; set; } = true;

    public string ContextSeparator { get; set; } = "_";

    public List<IMissingKeyHandler> MissingKeyHandlers { get; } = new();

    public event EventHandler<MissingKeyEventArgs> MissingKey;

    public List<IPostProcessor> PostProcessors { get; } = new();

    public virtual async Task<string> TranslateAsync(string language, string key, IDictionary<string, object> args, TranslationOptions options)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentNullException(nameof(language));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        string actualNamespace;

        if (key.IndexOf(':') > -1)
        {
            actualNamespace = key[..key.IndexOf(':')];
            key = key[(key.IndexOf(':') + 1)..];
        }
        else
        {
            actualNamespace = options.DefaultNamespace;
        }

        if (language.ToLower() == "cimode")
            return $"{actualNamespace}:{key}";

        var result = await ResolveTranslationAsync(language, actualNamespace, key, args, options);

        if (result == null)
            return key;

        return await ExtendTranslationAsync(result, key, language, args, options);
    }

    private static bool CheckForSpecialArg(IDictionary<string, object> args, string key, params Type[] allowedTypes)
    {
        if (args == null)
            return false;

        if (!args.ContainsKey(key))
            return false;

        var value = args[key];

        foreach (var type in allowedTypes)
        {
            if (value.GetType() == type)
                return true;
        }

        return false;
    }

    private async Task<string> ExtendTranslationAsync(string result, string key, string language, IDictionary<string, object> args,
        TranslationOptions options)
    {
        IDictionary<string, object> replaceArgs;

        if ((args?.ContainsKey("replace") ?? false) && args["replace"].GetType().IsClass)
            replaceArgs = args["replace"].ToDictionary();
        else
            replaceArgs = args;

        if (AllowInterpolation && (!(args?.ContainsKey("interpolate") ?? false) || args["interpolate"] is true))
            result = await _interpolator.InterpolateAsync(result, key, language, replaceArgs);

        if (AllowNesting && (!(args?.ContainsKey("nest") ?? false) || args["nest"] is true) && _interpolator.CanNest(result))
            result = await _interpolator.NestAsync(result, language, replaceArgs,
                (lang2, key2, args2) => TranslateAsync(lang2, key2, args2, options));

        if (AllowPostprocessing && PostProcessors.Count > 0)
            result = HandlePostProcessing(result, language, key, args);

        return result;
    }

    private static string[] GetPostProcessorKeys(IDictionary<string, object> args)
    {
        if (args == null)
            return null;

        if (!args.ContainsKey("postProcess"))
            return null;

        if (args["postProcess"] is string postProcessorStr)
        {
            if (postProcessorStr.IndexOf(',') > -1)
                return postProcessorStr.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            
            return new[] { postProcessorStr };
        }

        var localArgs = args["postProcess"];
        if (localArgs is string[] strings)
            return strings;

        return null;
    }

    private string HandlePostProcessing(string result, string language, string key, IDictionary<string, object> args)
    {
        var postProcessorKeys = GetPostProcessorKeys(args);

        if (postProcessorKeys != null)
            foreach (var postProcessorKey in postProcessorKeys)
            {
                if (string.IsNullOrWhiteSpace(postProcessorKey))
                    continue;

                foreach (var postProcessor in PostProcessors)
                {
                    if (postProcessor.Keyword == postProcessorKey)
                        result = postProcessor.ProcessResult(key, result, args, language, this);
                }
            }

        return result;
    }

    private async Task OnMissingKey(string language, string @namespace, string key, List<string> possibleKeys)
    {
        _logger.LogInformation("Missing translation for {namespace}:{key} in language {language}.", @namespace, key, language);
        
        if (MissingKey == null && MissingKeyHandlers.Count == 0)
            return;

        var args = new MissingKeyEventArgs(language, @namespace, key, possibleKeys.ToArray());

        MissingKey?.Invoke(this, args);

        if (MissingKeyHandlers.Count > 0)
        {
            _logger.LogDebug("Invoking missing key handlers for {namespace}:{key} in language {language}.", @namespace, key, language);

            foreach (var missingKeyHandler in MissingKeyHandlers)
                await missingKeyHandler.HandleMissingKeyAsync(this, args);
        }
    }

    private async Task<string> ResolveTranslationNoFallbackAsync(string language, string ns, string key, IDictionary<string, object> args)
    {
        var translationTree = await ResolveTranslationTreeAsync(language, ns);

        if (translationTree == null)
        {
            _logger.LogDebug("Unable to resolve a translation tree for {ns} with language {language}", ns, language);
            return null;
        }

        var needsPluralHandling = CheckForSpecialArg(args, "count", typeof(int), typeof(long)) && _pluralResolver.NeedsPlural(language);
        var needsContextHandling = CheckForSpecialArg(args, "context", typeof(string));

        var finalKey = key;
        var possibleKeys = new List<string> { finalKey };
        var pluralSuffix = string.Empty;

        if (needsPluralHandling)
        {
            _logger.LogDebug("Translation {ns}:{key} needs plural handling.", ns, key);
            
            var count = (int) Convert.ChangeType(args["count"], typeof(int));
            pluralSuffix = _pluralResolver.GetPluralSuffix(language, count);

            // Fallback for plural if context was not found
            if (needsContextHandling)
                possibleKeys.Add($"{finalKey}{pluralSuffix}");
        }

        // Get key for context if needed
        if (needsContextHandling)
        {
            _logger.LogDebug("Translation {ns}:{key} needs context handling.", ns, key);

            var context = (string) args["context"];
            finalKey = $"{finalKey}{ContextSeparator}{context}";
            possibleKeys.Add(finalKey);
        }

        // Get key for plural if needed
        if (needsPluralHandling)
        {
            finalKey = $"{finalKey}{pluralSuffix}";
            possibleKeys.Add(finalKey);
        }

        string result = null;

        // Iterate over the possible keys starting with most specific pluralkey (-> contextkey only) -> singularkey only
        for (var i = possibleKeys.Count - 1; i >= 0; i--)
        {
            var currentKey = possibleKeys[i];
            result = translationTree.GetValue(currentKey, args);

            if (result != null)
                break;
            
            _logger.LogDebug("Unable to resolve a translation for {currentKey} from the translation tree.", currentKey);
        }
        
        if(result == null)
            await OnMissingKey(language, ns, key, possibleKeys);

        _logger.LogInformation("The resolved translation for {ns}:{key} on language {language} was \"{result}\"", ns, key, language, result);
        
        return result;
    }

    private async Task<string> ResolveTranslationAsync(string language, string ns, string key, IDictionary<string, object> args, TranslationOptions options)
    {
        var result = await ResolveTranslationNoFallbackAsync(language, ns, key, args);

        if (result == null && options?.FallbackNamespaces?.Length > 0)
        {
            foreach (var fallbackNamespace in options.FallbackNamespaces)
            {
                var fallbackResult = await ResolveTranslationNoFallbackAsync(language, fallbackNamespace, key, args);
                if (fallbackResult != null)
                    return fallbackResult;
            }
        }

        if (result == null && options?.FallbackLanguages?.Length > 0)
        {
            foreach (var fallbackLanguage in options.FallbackLanguages)
            {
                var fallbackResult = await ResolveTranslationNoFallbackAsync(fallbackLanguage, ns, key, args);
                if (fallbackResult != null)
                    return fallbackResult;
                
                if (options.FallbackNamespaces?.Length > 0)
                {
                    foreach (var fallbackNamespace in options.FallbackNamespaces)
                    {
                        fallbackResult = await ResolveTranslationNoFallbackAsync(fallbackLanguage, fallbackNamespace, key, args);
                        if (fallbackResult != null)
                            return fallbackResult;
                    }
                }
            }
        }

        return result;
    }

    private async Task<ITranslationTree> ResolveTranslationTreeAsync(string language, string ns)
    {
        var cacheKey = $"{language}.{ns}";
        
        _logger.LogDebug("Trying to resolve translation tree {cacheKey}", cacheKey);

        if (_treeCache.TryGetValue(cacheKey, out var tree))
            return tree;

        tree = await _backend.LoadNamespaceAsync(language, ns);

        _treeCache.TryAdd(cacheKey, tree);

        return tree;
    }
}
