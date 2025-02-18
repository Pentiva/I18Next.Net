﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using I18Next.Net.Formatters;
using I18Next.Net.Internal;
using Microsoft.Extensions.Logging;

namespace I18Next.Net.Plugins;

public class DefaultInterpolator : IInterpolator
{
    private readonly ILogger _logger;
    private const string ExpressionPrefixPlain = @"{{";
    private const string ExpressionPrefix = @"\{\{";
    private const string ExpressionSuffix = @"\}\}";
    private const string NestingPrefix = @"\$t\(";
    private const string NestingPrefixPlain = @"$t(";
    private const string NestingSuffix = @"\)";

    private const string UnescapedExpressionPrefix = @"-";
    private const string UnescapedExpressionSuffix = @"";

    private static readonly Regex ExpressionRegex = new($"{ExpressionPrefix}(.+?){ExpressionSuffix}");

    private static readonly Regex UnescapedExpressionRegex =
        new($"{ExpressionPrefix}{UnescapedExpressionPrefix}(.+?){UnescapedExpressionSuffix}{ExpressionSuffix}");

    private static readonly Regex NestingRegex = new($"{NestingPrefix}(.+?){NestingSuffix}");


    private List<IFormatter> _formatters;

    public IFormatter DefaultFormatter { get; set; }

    public bool EscapeValues { get; set; } = true;

    public string FormatSeparator { get; set; } = ",";

    public int MaximumReplaces { get; set; } = 1000;

    public Func<string, Match, string> MissingValueHandler { get; set; }

    public bool UseFastNestingMatch { get; set; } = true;

    public DefaultInterpolator(ILogger logger)
    {
        _logger = logger;
        DefaultFormatter = new DefaultFormatter(_logger);
    }

    public virtual bool CanNest(string source)
    {
        return UseFastNestingMatch ? source.Contains(NestingPrefixPlain) : NestingRegex.IsMatch(source);
    }

    public List<IFormatter> Formatters => _formatters ??= new List<IFormatter>();

    public virtual Task<string> InterpolateAsync(string source, string key, string language, IDictionary<string, object> args)
    {
        if (!source.Contains(ExpressionPrefixPlain))
            return Task.FromResult(source);

        var unescapeMatches = UnescapedExpressionRegex.Matches(source);
        var matches = ExpressionRegex.Matches(source);

        var result = source;
        var replaces = 0;

        for (var i = 0; i < unescapeMatches.Count; i++)
        {
            var match = unescapeMatches[i];
            result = HandleUnescapeRegexMatch(result, language, args, match);

            replaces++;

            if (replaces >= MaximumReplaces)
                break;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            if (replaces >= MaximumReplaces)
                break;

            var match = matches[i];
            result = HandleRegexMatch(result, language, args, match);

            replaces++;
        }

        return Task.FromResult(result);
    }

    public virtual async Task<string> NestAsync(
        string source,
        string language,
        IDictionary<string, object> args,
        TranslateAsyncDelegate translateAsync)
    {
        var matches = NestingRegex.Matches(source);

        var result = source;

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            result = await HandleNestingRegexMatchAsync(result, language, args, translateAsync, match);
        }

        return result;
    }

    protected virtual string EscapeValue(string value)
    {
        return value;
    }

    protected virtual string Format(object value, string format, string language)
    {
        if (_formatters != null)
            foreach (var formatter in _formatters)
            {
                if (formatter.CanFormat(value, format, language))
                    return formatter.Format(value, format, language);
            }

        return DefaultFormatter.Format(value, format, language);
    }

    protected static object GetValue(string key, IDictionary<string, object> args)
    {
        if (args == null)
            return null;

        if (key.IndexOf('.') < 0)
            return args.TryGetValue(key, out var value) ? value : null;

        var keyParts = key.Split('.');

        object lastObject = args;

        foreach (var subKey in keyParts)
        {
            if (lastObject == null)
                return null;

            if (lastObject is IDictionary<string, object> dict)
            {
                if (!dict.TryGetValue(subKey, out lastObject))
                    return null;

                continue;
            }

            var lastObjectType = lastObject.GetType();

            if (lastObjectType.IsClass && lastObjectType.Namespace == null)
            {
                var lastDict = lastObject.ToDictionary();

                if (!lastDict.TryGetValue(subKey, out lastObject))
                    return null;
            }
        }

        return lastObject;
    }

    protected virtual string GetValueForExpression(string key, string language, IDictionary<string, object> args)
    {
        key = key.Trim();

        if (key.IndexOf(FormatSeparator, StringComparison.Ordinal) < 0)
            return GetValue(key, args)?.ToString();

        var keyParts = key.Split(FormatSeparator, 2);
        var actualKey = keyParts[0].Trim();
        var format = keyParts[1].Trim();
        var value = GetValue(actualKey, args);

        return Format(value, format, language);
    }

    protected virtual async Task<string> HandleNestingRegexMatchAsync(
        string source,
        string language,
        IDictionary<string, object> args,
        TranslateAsyncDelegate translateAsync,
        Match match)
    {
        var expression = match.Groups[1];
        string key;
        IDictionary<string, object> childArgs;

        if (expression.Value.IndexOf(FormatSeparator, StringComparison.Ordinal) < 0)
        {
            key = expression.Value;
            childArgs = args;
        }
        else
        {
            var keyParts = expression.Value.Split(FormatSeparator, 2);
            key = keyParts[0];

            var childArgsString = keyParts[1].Trim();
            childArgs = await ParseNestedArgsAsync(childArgsString, language, args);
        }

        var value = await translateAsync(language, key, childArgs);

        if (value == null)
            return source;

        if (value.Contains(match.Value))
            return source;

        return source.ReplaceFirst(match.Value, value);
    }

    protected virtual string HandleRegexMatch(string source, string language, IDictionary<string, object> args, Match match)
    {
        var expression = match.Groups[1];
        var value = GetValueForExpression(expression.Value, language, args) ?? MissingValueHandler?.Invoke(source, match) ?? string.Empty;

        if (EscapeValues)
            value = EscapeValue(value);

        source = source.ReplaceFirst(match.Value, value);
        return source;
    }

    protected virtual string HandleUnescapeRegexMatch(string source, string language, IDictionary<string, object> args, Match match)
    {
        var expression = match.Groups[1];
        var value = GetValueForExpression(expression.Value, language, args) ?? MissingValueHandler?.Invoke(source, match) ?? string.Empty;

        source = source.ReplaceFirst(match.Value, value);
        return source;
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Json serializer on uses primitive types, and JsonDocument, which should be referenced by `ObjectToInferredTypesConverter`.")]
    protected virtual async Task<IDictionary<string, object>> ParseNestedArgsAsync(
        string argsString,
        string language,
        IDictionary<string, object> parentArgs)
    {
        argsString = await InterpolateAsync(argsString, null, language, parentArgs);
        argsString = argsString.Replace('\'', '"');

        IDictionary<string, object> args = JsonSerializer.Deserialize <Dictionary<string, object>>(argsString, new JsonSerializerOptions() {
            Converters = {
                new ObjectToInferredTypesConverter()
            }
        });

        if (parentArgs != null)
            args = parentArgs.MergeLeft(args);

        return args;
    }

    private class ObjectToInferredTypesConverter : JsonConverter<object> {
        public override object Read(
            ref Utf8JsonReader    reader,
            Type                  typeToConvert,
            JsonSerializerOptions options) => reader.TokenType switch {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt64(out var l) => l,
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String when reader.TryGetDateTime(out var datetime) => datetime,
            JsonTokenType.String => reader.GetString()!,
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };

        public override void Write(
            Utf8JsonWriter        writer,
            object                objectToWrite,
            JsonSerializerOptions options) => throw new NotImplementedException("This should never need to write.");
            //JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
    }
}
