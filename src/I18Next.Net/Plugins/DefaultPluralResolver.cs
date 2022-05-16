using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace I18Next.Net.Plugins;

public enum JsonFormat
{
    Version1 = 1,
    Version2 = 2,
    Version3 = 3,
    Version4 = 4
}

public class DefaultPluralResolver : IPluralResolver
{
    private static readonly Dictionary<int, Func<int, int>> PluralizationFilters = new()
    {
            // @formatter:off
            { 1, n => n > 1 ? 1 : 0 },
            { 2, n => n != 1 ? 1 : 0 },
            { 3, n => 0 },
            { 4, n => n % 10 == 1 && n % 100 != 11 ? 0 : n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20) ? 1 : 2 },
            { 5, n => n == 0 ? 0 : n == 1 ? 1 : n == 2 ? 2 : n % 100 >= 3 && n % 100 <= 10 ? 3 : n % 100 >= 11 ? 4 : 5 },
            { 6, n => n == 1 ? 0 : n >= 2 && n <= 4 ? 1 : 2 },
            { 7, n => n == 1 ? 0 : n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20) ? 1 : 2 },
            { 8, n => n == 1 ? 0 : n == 2 ? 1 : n != 8 && n != 11 ? 2 : 3 },
            { 9, n => n >= 2 ? 1 : 0 },
            { 10, n => n == 1 ? 0 : n == 2 ? 1 : n < 7 ? 2 : n < 11 ? 3 : 4 },
            { 11, n => n == 1 || n == 11 ? 0 : n == 2 || n == 12 ? 1 : n > 2 && n < 20 ? 2 : 3 },
            { 12, n => n % 10 != 1 || n % 100 == 11 ? 1 : 0 },
            { 13, n => n != 0 ? 1 : 0 },
            { 14, n => n == 1 ? 0 : n == 2 ? 1 : n == 3 ? 2 : 3 },
            { 15, n => n % 10 == 1 && n % 100 != 11 ? 0 : n % 10 >= 2 && (n % 100 < 10 || n % 100 >= 20) ? 1 : 2 },
            { 16, n => n % 10 == 1 && n % 100 != 11 ? 0 : n != 0 ? 1 : 2 },
            { 17, n => n == 1 || n % 10 == 1 ? 0 : 1 },
            { 18, n => n == 0 ? 0 : n == 1 ? 1 : 2 },
            { 19, n => n == 1 ? 0 : n == 0 || n % 100 > 1 && n % 100 < 11 ? 1 : n % 100 > 10 && n % 100 < 20 ? 2 : 3 },
            { 20, n => n == 1 ? 0 : n == 0 || n % 100 > 0 && n % 100 < 20 ? 1 : 2 },
            { 21, n => n % 100 == 1 ? 1 : n % 100 == 2 ? 2 : n % 100 == 3 || n % 100 == 4 ? 3 : 0 }
        // @formatter:on
    };

    private static readonly PluralizationSet[] PluralizationSets =
    {
        new()
        {
            Languages = new[]
            {
                "ach", "ak", "am", "arn", "br", "fil", "gun", "ln", "mfe", "mg", "mi", "oc", "pt", "pt-BR",
                "tg", "ti", "tr", "uz", "wa"
            },
            Numbers = new[] { 1, 2 },
            Fc = 1
        },
        new()
        {
            Languages = new[]
            {
                "af", "an", "ast", "az", "bg", "bn", "ca", "da", "de", "dev", "el", "en",
                "eo", "es", "et", "eu", "fi", "fo", "fur", "fy", "gl", "gu", "ha", "he", "hi",
                "hu", "hy", "ia", "it", "kn", "ku", "lb", "mai", "ml", "mn", "mr", "nah", "nap", "nb",
                "ne", "nl", "nn", "no", "nso", "pa", "pap", "pms", "ps", "pt-PT", "rm", "sco",
                "se", "si", "so", "son", "sq", "sv", "sw", "ta", "te", "tk", "ur", "yo"
            },
            Numbers = new[] { 1, 2 },
            Fc = 2
        },
        new()
        {
            Languages = new[]
            {
                "ay", "bo", "cgg", "fa", "id", "ja", "jbo", "ka", "kk", "km", "ko", "ky", "lo",
                "ms", "sah", "su", "th", "tt", "ug", "vi", "wo", "zh"
            },
            Numbers = new[] { 1 },
            Fc = 3
        },
        new()
        {
            Languages = new[] { "be", "bs", "dz", "hr", "ru", "sr", "uk" },
            Numbers = new[] { 1, 2, 5 },
            Fc = 4
        },
        new() { Languages = new[] { "ar" }, Numbers = new[] { 0, 1, 2, 3, 11, 100 }, Fc = 5 },
        new() { Languages = new[] { "cs", "sk" }, Numbers = new[] { 1, 2, 5 }, Fc = 6 },
        new() { Languages = new[] { "csb", "pl" }, Numbers = new[] { 1, 2, 5 }, Fc = 7 },
        new() { Languages = new[] { "cy" }, Numbers = new[] { 1, 2, 3, 8 }, Fc = 8 },
        new() { Languages = new[] { "fr" }, Numbers = new[] { 1, 2 }, Fc = 9 },
        new() { Languages = new[] { "ga" }, Numbers = new[] { 1, 2, 3, 7, 11 }, Fc = 10 },
        new() { Languages = new[] { "gd" }, Numbers = new[] { 1, 2, 3, 20 }, Fc = 11 },
        new() { Languages = new[] { "is" }, Numbers = new[] { 1, 2 }, Fc = 12 },
        new() { Languages = new[] { "jv" }, Numbers = new[] { 0, 1 }, Fc = 13 },
        new() { Languages = new[] { "kw" }, Numbers = new[] { 1, 2, 3, 4 }, Fc = 14 },
        new() { Languages = new[] { "lt" }, Numbers = new[] { 1, 2, 10 }, Fc = 15 },
        new() { Languages = new[] { "lv" }, Numbers = new[] { 1, 2, 0 }, Fc = 16 },
        new() { Languages = new[] { "mk" }, Numbers = new[] { 1, 2 }, Fc = 17 },
        new() { Languages = new[] { "mnk" }, Numbers = new[] { 0, 1, 2 }, Fc = 18 },
        new() { Languages = new[] { "mt" }, Numbers = new[] { 1, 2, 11, 20 }, Fc = 19 },
        new() { Languages = new[] { "or" }, Numbers = new[] { 2, 1 }, Fc = 2 },
        new() { Languages = new[] { "ro" }, Numbers = new[] { 1, 2, 20 }, Fc = 20 },
        new() { Languages = new[] { "sl" }, Numbers = new[] { 5, 1, 2, 3 }, Fc = 21 }
    };

    private static readonly ConcurrentDictionary<string, PluralizationRule> Rules;

    static DefaultPluralResolver()
    {
        lock (PluralizationSets)
        {
            if (Rules != null)
                return;

            Rules = new ConcurrentDictionary<string, PluralizationRule>();

            foreach (var set in PluralizationSets)
            {
                foreach (var language in set.Languages)
                {
                    Rules.TryAdd(language, new PluralizationRule
                    {
                        Numbers = set.Numbers,
                        Filter = PluralizationFilters[set.Fc]
                    });
                }
            }
        }
    }

    public string PluralSeparator { get; set; } = "_";

    public JsonFormat JsonFormatVersion { get; set; } = JsonFormat.Version3;

    public bool UseSimplePluralSuffixIfPossible { get; set; } = true;

    public string GetPluralSuffix(string language, decimal count, bool isOrdinal = false)
    {
        if (JsonFormatVersion == JsonFormat.Version4) {
            var lang = GetLanguagePart(language);
            var aStruct = new Struct(count);
            var abc = isOrdinal ? GetCardinal(lang, aStruct) : GetOrdinal(lang, aStruct);
            return abc switch {
                PluralRulesValues.Zero => "_zero",
                PluralRulesValues.One => "_one",
                PluralRulesValues.Two => "_two",
                PluralRulesValues.Few => "_few",
                PluralRulesValues.Many => "_many",
                PluralRulesValues.Other => "_other",
                null => string.Empty,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        var rule = GetRule(language);

        if (rule == null)
            return string.Empty;

        var numberIndex = rule.Filter((int)count);
        var suffixNumber = numberIndex > rule.Numbers.Length ? numberIndex : rule.Numbers[numberIndex];
        string suffix;

        if (UseSimplePluralSuffixIfPossible && rule.Numbers.Length == 2 && rule.Numbers[0] == 1)
        {
            if (suffixNumber == 2)
                suffix = "plural";
            else if (suffixNumber == 1)
                suffix = null;
            else
                suffix = suffixNumber.ToString();
        }
        else
        {
            suffix = suffixNumber.ToString();
        }

        switch (JsonFormatVersion)
        {
            case JsonFormat.Version1:
                if (suffixNumber == 1)
                    return string.Empty;

                if (suffixNumber > 2)
                    return $"_plural_{suffixNumber}";

                return $"_{suffix}";

            case JsonFormat.Version2:
                if (rule.Numbers.Length == 1 || suffix == null)
                    return string.Empty;

                return $"{PluralSeparator}{suffix}";    

            default:
                if (UseSimplePluralSuffixIfPossible && rule.Numbers.Length == 2 && rule.Numbers[0] == 1)
                    return suffix == null ? string.Empty : $"{PluralSeparator}{suffix}";
                else
                    return $"{PluralSeparator}{numberIndex}";
        }
    }

    public bool NeedsPlural(string language)
    {
        if (JsonFormatVersion == JsonFormat.Version3 || JsonFormatVersion == JsonFormat.Version4)
            return true;

        var rule = GetRule(language);

        return rule != null && rule.Numbers.Length > 1;
    }

    private static string GetLanguagePart(string language)
    {
        var index = language.IndexOf('-');

        if (index == -1)
            return language;

        return language.Substring(0, index);
    }

    private static PluralizationRule GetRule(string language)
    {
        if (Rules.TryGetValue(language, out var rule))
            return rule;

        var languagePart = GetLanguagePart(language);

        if (Rules.TryGetValue(languagePart, out rule))
            return rule;

        return null;
    }

    private class PluralizationRule
    {
        public Func<int, int> Filter { get; set; }

        public int[] Numbers { get; set; }
    }

    private class PluralizationSet
    {
        public int Fc { get; set; }

        public string[] Languages { get; set; }

        public int[] Numbers { get; set; }
    }

    private static PluralRulesValues? GetOrdinal(string abc, Struct prc) {
        return abc switch {
            "af" => PluralRulesValues.Other,
            "am" => PluralRulesValues.Other,
            "ar" => PluralRulesValues.Other,
            "as" when prc.n.In(1, 5, 7, 8, 9, 10) => PluralRulesValues.One,
            "as" when prc.n.In(2, 3) => PluralRulesValues.Two,
            "as" when prc.n == 4 => PluralRulesValues.Few,
            "as" when prc.n == 6 => PluralRulesValues.Many,
            "as" => PluralRulesValues.Other,
            "az" when (prc.i % 10).In(1, 2, 5, 7, 8) || (prc.i % 100).In(20, 50, 70, 80) => PluralRulesValues.One,
            "az" when (prc.i % 10).In(3, 4) || (prc.i % 1000).In(100, 200, 300, 400, 500, 600, 700, 800, 900) =>
                PluralRulesValues.Few,
            "az" when prc.i == 0 || prc.i % 10 == 6 || (prc.i % 100).In(40, 60, 90) => PluralRulesValues.Many,
            "az" => PluralRulesValues.Other,
            "be" when (prc.n % 10).In(2, 3) && (prc.n % 100).NotIn(12, 13) => PluralRulesValues.Few,
            "be" => PluralRulesValues.Other,
            "bg" => PluralRulesValues.Other,
            "bn" when prc.n.In(1, 5, 7, 8, 9, 10) => PluralRulesValues.One,
            "bn" when prc.n.In(2, 3) => PluralRulesValues.Two,
            "bn" when prc.n == 4 => PluralRulesValues.Few,
            "bn" when prc.n == 6 => PluralRulesValues.Many,
            "bn" => PluralRulesValues.Other,
            "bs" => PluralRulesValues.Other,
            "ca" when prc.n.In(1, 3) => PluralRulesValues.One,
            "ca" when prc.n == 2 => PluralRulesValues.Two,
            "ca" when prc.n == 4 => PluralRulesValues.Few,
            "ca" => PluralRulesValues.Other,
            "ce" => PluralRulesValues.Other,
            "cs" => PluralRulesValues.Other,
            "cy" when prc.n.In(0, 7, 8, 9) => PluralRulesValues.Zero,
            "cy" when prc.n == 1 => PluralRulesValues.One,
            "cy" when prc.n == 2 => PluralRulesValues.Two,
            "cy" when prc.n.In(3, 4) => PluralRulesValues.Few,
            "cy" when prc.n.In(5, 6) => PluralRulesValues.Many,
            "cy" => PluralRulesValues.Other,
            "da" => PluralRulesValues.Other,
            "de" => PluralRulesValues.Other,
            "dsb" => PluralRulesValues.Other,
            "el" => PluralRulesValues.Other,
            "en" when prc.n % 10 == 1 && prc.n % 100 != 11 => PluralRulesValues.One,
            "en" when prc.n % 10 == 2 && prc.n % 100 != 12 => PluralRulesValues.Two,
            "en" when prc.n % 10 == 3 && prc.n % 100 != 13 => PluralRulesValues.Few,
            "en" => PluralRulesValues.Other,
            "es" => PluralRulesValues.Other,
            "et" => PluralRulesValues.Other,
            "eu" => PluralRulesValues.Other,
            "fa" => PluralRulesValues.Other,
            "fi" => PluralRulesValues.Other,
            "fil" when prc.n == 1 => PluralRulesValues.One,
            "fil" => PluralRulesValues.Other,
            "fr" when prc.n == 1 => PluralRulesValues.One,
            "fr" => PluralRulesValues.Other,
            "fy" => PluralRulesValues.Other,
            "ga" when prc.n == 1 => PluralRulesValues.One,
            "ga" => PluralRulesValues.Other,
            "gd" when prc.n.In(1, 11) => PluralRulesValues.One,
            "gd" when prc.n.In(2, 12) => PluralRulesValues.Two,
            "gd" when prc.n.In(3, 13) => PluralRulesValues.Few,
            "gd" => PluralRulesValues.Other,
            "gl" => PluralRulesValues.Other,
            "gsw" => PluralRulesValues.Other,
            "gu" when prc.n == 1 => PluralRulesValues.One,
            "gu" when prc.n.In(2, 3) => PluralRulesValues.Two,
            "gu" when prc.n == 4 => PluralRulesValues.Few,
            "gu" when prc.n == 6 => PluralRulesValues.Many,
            "gu" => PluralRulesValues.Other,
            "he" => PluralRulesValues.Other,
            "hi" when prc.n == 1 => PluralRulesValues.One,
            "hi" when prc.n.In(2, 3) => PluralRulesValues.Two,
            "hi" when prc.n == 4 => PluralRulesValues.Few,
            "hi" when prc.n == 6 => PluralRulesValues.Many,
            "hi" => PluralRulesValues.Other,
            "hr" => PluralRulesValues.Other,
            "hsb" => PluralRulesValues.Other,
            "hu" when prc.n.In(1, 5) => PluralRulesValues.One,
            "hu" => PluralRulesValues.Other,
            "hy" when prc.n == 1 => PluralRulesValues.One,
            "hy" => PluralRulesValues.Other,
            "ia" => PluralRulesValues.Other,
            "id" => PluralRulesValues.Other,
            "is" => PluralRulesValues.Other,
            "it" when prc.n.In(11, 8, 80, 800) => PluralRulesValues.Many,
            "it" => PluralRulesValues.Other,
            "ja" => PluralRulesValues.Other,
            "ka" when prc.i == 1 => PluralRulesValues.One,
            "ka" when prc.i == 0 || (prc.i % 100).In(new[] { 40, 60, 80 }.Concat(Enumerable.Range(2, 19))
                                                                         .Select<int, decimal>(i => i)
                                                                         .ToArray()) => PluralRulesValues.Many,
            "ka" => PluralRulesValues.Other,
            "kk" when prc.n % 10 == 6 || prc.n % 10 == 9 || (prc.n % 10 == 0 && prc.n != 0) => PluralRulesValues.Many,
            "kk" => PluralRulesValues.Other,
            "km" => PluralRulesValues.Other,
            "kn" => PluralRulesValues.Other,
            "ko" => PluralRulesValues.Other,
            "kw" when (prc.n == prc.i && prc.n.Between(1, 4)) || (prc.n % 100).In(Array.Empty<int>()
                         .Concat(Enumerable.Range(1, 4)).Concat(Enumerable.Range(21, 4))
                         .Concat(Enumerable.Range(41, 4)).Concat(Enumerable.Range(61, 4))
                         .Concat(Enumerable.Range(81, 4)).Select<int, decimal>(i => i).ToArray()) =>
                PluralRulesValues.One,
            "kw" when prc.n == 5 || prc.n % 100 == 5 => PluralRulesValues.Many,
            "kw" => PluralRulesValues.Other,
            "ky" => PluralRulesValues.Other,
            "lo" when prc.n == 1 => PluralRulesValues.One,
            "lo" => PluralRulesValues.Other,
            "lt" => PluralRulesValues.Other,
            "lv" => PluralRulesValues.Other,
            "mk" when prc.i % 10 == 1       && prc.i % 100 != 11 => PluralRulesValues.One,
            "mk" when prc.i % 10 == 2       && prc.i % 100 != 12 => PluralRulesValues.Two,
            "mk" when (prc.i % 10).In(7, 8) && (prc.i % 100).NotIn(17, 18) => PluralRulesValues.Many,
            "mk" => PluralRulesValues.Other,
            "ml" => PluralRulesValues.Other,
            "mn" => PluralRulesValues.Other,
            "mr" when prc.n == 1 => PluralRulesValues.One,
            "mr" when prc.n.In(2, 3) => PluralRulesValues.Two,
            "mr" when prc.n == 4 => PluralRulesValues.Few,
            "mr" => PluralRulesValues.Other,
            "ms" when prc.n == 1 => PluralRulesValues.One,
            "ms" => PluralRulesValues.Other,
            "my" => PluralRulesValues.Other,
            "nb" => PluralRulesValues.Other,
            "ne" when prc.n == prc.i && prc.n.Between(1, 4) => PluralRulesValues.One,
            "ne" => PluralRulesValues.Other,
            "nl" => PluralRulesValues.Other,
            "or" when prc.n == prc.i &&
                      prc.n.In(new[] { 1, 5 }.Concat(Enumerable.Range(7, 3)).Select<int, decimal>(i => i).ToArray()) =>
                PluralRulesValues.One,
            "or" when prc.n.In(2, 3) => PluralRulesValues.Two,
            "or" when prc.n == 4 => PluralRulesValues.Few,
            "or" when prc.n == 6 => PluralRulesValues.Many,
            "or" => PluralRulesValues.Other,
            "pa" => PluralRulesValues.Other,
            "pl" => PluralRulesValues.Other,
            "prg" => PluralRulesValues.Other,
            "ps" => PluralRulesValues.Other,
            "pt" => PluralRulesValues.Other,
            "ro" when prc.n == 1 => PluralRulesValues.One,
            "ro" => PluralRulesValues.Other,
            "ru" => PluralRulesValues.Other,
            "sd" => PluralRulesValues.Other,
            "si" => PluralRulesValues.Other,
            "sk" => PluralRulesValues.Other,
            "sl" => PluralRulesValues.Other,
            "sq" when prc.n == 1 => PluralRulesValues.One,
            "sq" when prc.n % 10 == 4 && prc.n % 100 != 14 => PluralRulesValues.Many,
            "sq" => PluralRulesValues.Other,
            "sr" => PluralRulesValues.Other,
            "sv" when (prc.n % 10).In(1, 2) && (prc.n % 100).NotIn(11, 12) => PluralRulesValues.One,
            "sv" => PluralRulesValues.Other,
            "sw" => PluralRulesValues.Other,
            "ta" => PluralRulesValues.Other,
            "te" => PluralRulesValues.Other,
            "th" => PluralRulesValues.Other,
            "tk" when (prc.n % 10).In(6, 9) || prc.n == 10 => PluralRulesValues.Few,
            "tk" => PluralRulesValues.Other,
            "tr" => PluralRulesValues.Other,
            "uk" when prc.n % 10 == 3 && prc.n % 100 != 13 => PluralRulesValues.Few,
            "uk" => PluralRulesValues.Other,
            "ur" => PluralRulesValues.Other,
            "uz" => PluralRulesValues.Other,
            "vi" when prc.n == 1 => PluralRulesValues.One,
            "vi" => PluralRulesValues.Other,
            "zh" => PluralRulesValues.Other,
            "zu" => PluralRulesValues.Other,
            _ => null
        };
    }

    private static PluralRulesValues? GetCardinal(string abc, Struct prc) {
        return abc switch {
            "af" when prc.n == 1 => PluralRulesValues.One,
            "af" => PluralRulesValues.Other,
            "ak" when prc.n == prc.i && prc.n.Between(0, 1) => PluralRulesValues.One,
            "ak" => PluralRulesValues.Other,
            "am" when prc.i == 0 || prc.n == 1 => PluralRulesValues.One,
            "am" => PluralRulesValues.Other,
            "ar" when prc.n == 0 => PluralRulesValues.Zero,
            "ar" when prc.n == 1 => PluralRulesValues.One,
            "ar" when prc.n == 2 => PluralRulesValues.Two,
            "ar" when (prc.n % 100).Between(3, 10) => PluralRulesValues.Few,
            "ar" when (prc.n % 100).Between(11, 99) => PluralRulesValues.Many,
            "ar" => PluralRulesValues.Other,
            "as" when prc.i == 0 || prc.n == 1 => PluralRulesValues.One,
            "as" => PluralRulesValues.Other,
            "asa" when prc.n == 1 => PluralRulesValues.One,
            "asa" => PluralRulesValues.Other,
            "ast" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "ast" => PluralRulesValues.Other,
            "az" when prc.n == 1 => PluralRulesValues.One,
            "az" => PluralRulesValues.Other,
            "be" when prc.n % 10 == 1            && prc.n % 100 != 11 => PluralRulesValues.One,
            "be" when (prc.n % 10).Between(2, 4) && (prc.n % 100).NotBetween(12, 14) => PluralRulesValues.Few,
            "be" when prc.n % 10 == 0 || (prc.n % 10).Between(5, 9) || (prc.n % 100).Between(11, 14) =>
                PluralRulesValues.Many,
            "be" => PluralRulesValues.Other,
            "bem" when prc.n == 1 => PluralRulesValues.One,
            "bem" => PluralRulesValues.Other,
            "bez" when prc.n == 1 => PluralRulesValues.One,
            "bez" => PluralRulesValues.Other,
            "bg" when prc.n == 1 => PluralRulesValues.One,
            "bg" => PluralRulesValues.Other,
            "bm" => PluralRulesValues.Other,
            "bn" when prc.i == 0 || prc.n == 1 => PluralRulesValues.One,
            "bn" => PluralRulesValues.Other,
            "bo" => PluralRulesValues.Other,
            "br" when prc.n % 10 == 1 && (prc.n % 100).NotIn(11, 71, 91) => PluralRulesValues.One,
            "br" when prc.n % 10 == 2 && (prc.n % 100).NotIn(12, 72, 92) => PluralRulesValues.Two,
            "br" when
                (prc.n % 10).In(new[] { 9 }.Concat(Enumerable.Range(3, 2)).Select<int, decimal>(i => i).ToArray()) &&
                (prc.n % 100).NotIn(Array.Empty<int>().Concat(Enumerable.Range(10, 10)).Concat(Enumerable.Range(70, 10))
                                         .Concat(Enumerable.Range(90, 10)).Select<int, decimal>(i => i)
                                         .ToArray()) => PluralRulesValues.Few,
            "br" when prc.n != 0 && prc.n % 1000000 == 0 => PluralRulesValues.Many,
            "br" => PluralRulesValues.Other,
            "brx" when prc.n == 1 => PluralRulesValues.One,
            "brx" => PluralRulesValues.Other,
            "bs" when (prc.v == 0 && prc.i % 10 == 1 && prc.i % 100 != 11) || (prc.f % 10 == 1 && prc.f % 100 != 11) =>
                PluralRulesValues.One,
            "bs" when (prc.v == 0 && (prc.i % 10).Between(2, 4) && (prc.i % 100).NotBetween(12, 14)) ||
                      ((prc.f % 10).Between(2, 4) && (prc.f % 100).NotBetween(12, 14)) => PluralRulesValues.Few,
            "bs" => PluralRulesValues.Other,
            "ca" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "ca" => PluralRulesValues.Other,
            "ce" when prc.n == 1 => PluralRulesValues.One,
            "ce" => PluralRulesValues.Other,
            "ceb" when (prc.v == 0 && prc.i.In(1, 2, 3)) || (prc.v == 0 && (prc.i % 10).NotIn(4, 6, 9)) ||
                       (prc.v != 0 && (prc.f % 10).NotIn(4, 6, 9)) => PluralRulesValues.One,
            "ceb" => PluralRulesValues.Other,
            "cgg" when prc.n == 1 => PluralRulesValues.One,
            "cgg" => PluralRulesValues.Other,
            "chr" when prc.n == 1 => PluralRulesValues.One,
            "chr" => PluralRulesValues.Other,
            "ku" when prc.n == 1 => PluralRulesValues.One,
            "ku" => PluralRulesValues.Other,
            "cs" when prc.i == 1          && prc.v == 0 => PluralRulesValues.One,
            "cs" when prc.i.Between(2, 4) && prc.v == 0 => PluralRulesValues.Few,
            "cs" when prc.v != 0 => PluralRulesValues.Many,
            "cs" => PluralRulesValues.Other,
            "cy" when prc.n == 0 => PluralRulesValues.Zero,
            "cy" when prc.n == 1 => PluralRulesValues.One,
            "cy" when prc.n == 2 => PluralRulesValues.Two,
            "cy" when prc.n == 3 => PluralRulesValues.Few,
            "cy" when prc.n == 6 => PluralRulesValues.Many,
            "cy" => PluralRulesValues.Other,
            "da" when prc.n == 1 || (prc.t != 0 && prc.i.In(0, 1)) => PluralRulesValues.One,
            "da" => PluralRulesValues.Other,
            "de" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "de" => PluralRulesValues.Other,
            "dsb" when (prc.v == 0 && prc.i % 100 == 1) || prc.f % 100 == 1 => PluralRulesValues.One,
            "dsb" when (prc.v == 0 && prc.i % 100 == 2) || prc.f % 100 == 2 => PluralRulesValues.Two,
            "dsb" when (prc.v == 0 && (prc.i % 100).Between(3, 4)) || (prc.f % 100).Between(3, 4) => PluralRulesValues
               .Few,
            "dsb" => PluralRulesValues.Other,
            "dv" when prc.n == 1 => PluralRulesValues.One,
            "dv" => PluralRulesValues.Other,
            "dz" => PluralRulesValues.Other,
            "ee" when prc.n == 1 => PluralRulesValues.One,
            "ee" => PluralRulesValues.Other,
            "el" when prc.n == 1 => PluralRulesValues.One,
            "el" => PluralRulesValues.Other,
            "en" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "en" => PluralRulesValues.Other,
            "eo" when prc.n == 1 => PluralRulesValues.One,
            "eo" => PluralRulesValues.Other,
            "es" when prc.n == 1 => PluralRulesValues.One,
            "es" => PluralRulesValues.Other,
            "et" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "et" => PluralRulesValues.Other,
            "eu" when prc.n == 1 => PluralRulesValues.One,
            "eu" => PluralRulesValues.Other,
            "fa" when prc.i == 0 || prc.n == 1 => PluralRulesValues.One,
            "fa" => PluralRulesValues.Other,
            "ff" when prc.i.In(0, 1) => PluralRulesValues.One,
            "ff" => PluralRulesValues.Other,
            "fi" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "fi" => PluralRulesValues.Other,
            "fil" when (prc.v == 0 && prc.i.In(1, 2, 3)) || (prc.v == 0 && (prc.i % 10).NotIn(4, 6, 9)) ||
                       (prc.v != 0 && (prc.f % 10).NotIn(4, 6, 9)) => PluralRulesValues.One,
            "fil" => PluralRulesValues.Other,
            "fo" when prc.n == 1 => PluralRulesValues.One,
            "fo" => PluralRulesValues.Other,
            "fr" when prc.i.In(0, 1) => PluralRulesValues.One,
            "fr" when (prc.e == 0 && prc.i != 0 && prc.i % 1000000 == 0 && prc.v == 0) || prc.e.NotBetween(0, 5) =>
                PluralRulesValues.Many,
            "fr" => PluralRulesValues.Other,
            "fur" when prc.n == 1 => PluralRulesValues.One,
            "fur" => PluralRulesValues.Other,
            "fy" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "fy" => PluralRulesValues.Other,
            "ga" when prc.n == 1 => PluralRulesValues.One,
            "ga" when prc.n == 2 => PluralRulesValues.Two,
            "ga" when prc.n == prc.i && prc.n.Between(3, 6) => PluralRulesValues.Few,
            "ga" when prc.n == prc.i && prc.n.Between(7, 10) => PluralRulesValues.Many,
            "ga" => PluralRulesValues.Other,
            "gd" when prc.n.In(1, 11) => PluralRulesValues.One,
            "gd" when prc.n.In(2, 12) => PluralRulesValues.Two,
            "gd" when prc.n == prc.i && prc.n.In(Array.Empty<int>().Concat(Enumerable.Range(3, 8))
                                                      .Concat(Enumerable.Range(13, 7)).Select<int, decimal>(i => i)
                                                      .ToArray()) => PluralRulesValues.Few,
            "gd" => PluralRulesValues.Other,
            "gl" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "gl" => PluralRulesValues.Other,
            "gsw" when prc.n == 1 => PluralRulesValues.One,
            "gsw" => PluralRulesValues.Other,
            "gu" when prc.i == 0 || prc.n == 1 => PluralRulesValues.One,
            "gu" => PluralRulesValues.Other,
            "gv" when prc.v == 0 && prc.i % 10 == 1 => PluralRulesValues.One,
            "gv" when prc.v == 0 && prc.i % 10 == 2 => PluralRulesValues.Two,
            "gv" when prc.v == 0 && (prc.i % 100).In(0, 20, 40, 60, 80) => PluralRulesValues.Few,
            "gv" when prc.v != 0 => PluralRulesValues.Many,
            "gv" => PluralRulesValues.Other,
            "ha" when prc.n == 1 => PluralRulesValues.One,
            "ha" => PluralRulesValues.Other,
            "haw" when prc.n == 1 => PluralRulesValues.One,
            "haw" => PluralRulesValues.Other,
            "he" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "he" when prc.i == 2 && prc.v == 0 => PluralRulesValues.Two,
            "he" when prc.v == 0 && prc.n == prc.i && prc.n.NotBetween(0, 10) && prc.n % 10 == 0 => PluralRulesValues
               .Many,
            "he" => PluralRulesValues.Other,
            "hi" when prc.i == 0 || prc.n == 1 => PluralRulesValues.One,
            "hi" => PluralRulesValues.Other,
            "hr" when (prc.v == 0 && prc.i % 10 == 1 && prc.i % 100 != 11) || (prc.f % 10 == 1 && prc.f % 100 != 11) =>
                PluralRulesValues.One,
            "hr" when (prc.v == 0 && (prc.i % 10).Between(2, 4) && (prc.i % 100).NotBetween(12, 14)) ||
                      ((prc.f % 10).Between(2, 4) && (prc.f % 100).NotBetween(12, 14)) => PluralRulesValues.Few,
            "hr" => PluralRulesValues.Other,
            "hsb" when (prc.v == 0 && prc.i % 100 == 1) || prc.f % 100 == 1 => PluralRulesValues.One,
            "hsb" when (prc.v == 0 && prc.i % 100 == 2) || prc.f % 100 == 2 => PluralRulesValues.Two,
            "hsb" when (prc.v == 0 && (prc.i % 100).Between(3, 4)) || (prc.f % 100).Between(3, 4) => PluralRulesValues
               .Few,
            "hsb" => PluralRulesValues.Other,
            "hu" when prc.n == 1 => PluralRulesValues.One,
            "hu" => PluralRulesValues.Other,
            "hy" when prc.i.In(0, 1) => PluralRulesValues.One,
            "hy" => PluralRulesValues.Other,
            "ia" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "ia" => PluralRulesValues.Other,
            "id" => PluralRulesValues.Other,
            "ig" => PluralRulesValues.Other,
            "ii" => PluralRulesValues.Other,
            "is" when (prc.t == 0 && prc.i % 10 == 1 && prc.i % 100 != 11) || prc.t != 0 => PluralRulesValues.One,
            "is" => PluralRulesValues.Other,
            "it" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "it" => PluralRulesValues.Other,
            "iu" when prc.n == 1 => PluralRulesValues.One,
            "iu" when prc.n == 2 => PluralRulesValues.Two,
            "iu" => PluralRulesValues.Other,
            "ja" => PluralRulesValues.Other,
            "jgo" when prc.n == 1 => PluralRulesValues.One,
            "jgo" => PluralRulesValues.Other,
            "jmc" when prc.n == 1 => PluralRulesValues.One,
            "jmc" => PluralRulesValues.Other,
            "jv" => PluralRulesValues.Other,
            "ka" when prc.n == 1 => PluralRulesValues.One,
            "ka" => PluralRulesValues.Other,
            "kab" when prc.i.In(0, 1) => PluralRulesValues.One,
            "kab" => PluralRulesValues.Other,
            "kde" => PluralRulesValues.Other,
            "kea" => PluralRulesValues.Other,
            "kk" when prc.n == 1 => PluralRulesValues.One,
            "kk" => PluralRulesValues.Other,
            "kkj" when prc.n == 1 => PluralRulesValues.One,
            "kkj" => PluralRulesValues.Other,
            "kl" when prc.n == 1 => PluralRulesValues.One,
            "kl" => PluralRulesValues.Other,
            "km" => PluralRulesValues.Other,
            "kn" when prc.i == 0 || prc.n == 1 => PluralRulesValues.One,
            "kn" => PluralRulesValues.Other,
            "ko" => PluralRulesValues.Other,
            "ks" when prc.n == 1 => PluralRulesValues.One,
            "ks" => PluralRulesValues.Other,
            "ksb" when prc.n == 1 => PluralRulesValues.One,
            "ksb" => PluralRulesValues.Other,
            "ksh" when prc.n == 0 => PluralRulesValues.Zero,
            "ksh" when prc.n == 1 => PluralRulesValues.One,
            "ksh" => PluralRulesValues.Other,
            "kw" when prc.n == 0 => PluralRulesValues.Zero,
            "kw" when prc.n == 1 => PluralRulesValues.One,
            "kw" when (prc.n % 100).In(2, 22, 42, 62, 82) ||
                      (prc.n % 1000 == 0 &&
                       (prc.n % 100000).In(new[] { 40000, 60000, 80000 }.Concat(Enumerable.Range(1000, 19001))
                                                                        .Select<int, decimal>(i => i).ToArray())) ||
                      (prc.n != 0 && prc.n % 1000000 == 100000) => PluralRulesValues.Two,
            "kw" when (prc.n % 100).In(3, 23, 43, 63, 83) => PluralRulesValues.Few,
            "kw" when prc.n != 1 && (prc.n % 100).In(1, 21, 41, 61, 81) => PluralRulesValues.Many,
            "kw" => PluralRulesValues.Other,
            "ky" when prc.n == 1 => PluralRulesValues.One,
            "ky" => PluralRulesValues.Other,
            "lag" when prc.n == 0 => PluralRulesValues.Zero,
            "lag" when prc.i.In(0, 1) && prc.n != 0 => PluralRulesValues.One,
            "lag" => PluralRulesValues.Other,
            "lb" when prc.n == 1 => PluralRulesValues.One,
            "lb" => PluralRulesValues.Other,
            "lg" when prc.n == 1 => PluralRulesValues.One,
            "lg" => PluralRulesValues.Other,
            "lkt" => PluralRulesValues.Other,
            "ln" when prc.n == prc.i && prc.n.Between(0, 1) => PluralRulesValues.One,
            "ln" => PluralRulesValues.Other,
            "lo" => PluralRulesValues.Other,
            "lt" when prc.n % 10 == 1 && (prc.n % 100).NotBetween(11, 19) => PluralRulesValues.One,
            "lt" when (prc.n % 10).Between(2, 9) && (prc.n % 100).NotBetween(11, 19) => PluralRulesValues.Few,
            "lt" when prc.f != 0 => PluralRulesValues.Many,
            "lt" => PluralRulesValues.Other,
            "lv" when prc.n % 10 == 0 || (prc.n % 100).Between(11, 19) || (prc.v == 2 && (prc.f % 100).Between(11, 19))
                => PluralRulesValues.Zero,
            "lv" when (prc.n % 10 == 1 && prc.n % 100 != 11) || (prc.v == 2 && prc.f % 10 == 1 && prc.f % 100 != 11) ||
                      (prc.v      != 2 && prc.f % 10  == 1) => PluralRulesValues.One,
            "lv" => PluralRulesValues.Other,
            "mas" when prc.n == 1 => PluralRulesValues.One,
            "mas" => PluralRulesValues.Other,
            "mg" when prc.n == prc.i && prc.n.Between(0, 1) => PluralRulesValues.One,
            "mg" => PluralRulesValues.Other,
            "mgo" when prc.n == 1 => PluralRulesValues.One,
            "mgo" => PluralRulesValues.Other,
            "mk" when (prc.v == 0 && prc.i % 10 == 1 && prc.i % 100 != 11) || (prc.f % 10 == 1 && prc.f % 100 != 11) =>
                PluralRulesValues.One,
            "mk" => PluralRulesValues.Other,
            "ml" when prc.n == 1 => PluralRulesValues.One,
            "ml" => PluralRulesValues.Other,
            "mn" when prc.n == 1 => PluralRulesValues.One,
            "mn" => PluralRulesValues.Other,
            "mr" when prc.n == 1 => PluralRulesValues.One,
            "mr" => PluralRulesValues.Other,
            "ms" => PluralRulesValues.Other,
            "mt" when prc.n == 1 => PluralRulesValues.One,
            "mt" when prc.n == 0 || (prc.n % 100).Between(2, 10) => PluralRulesValues.Few,
            "mt" when (prc.n % 100).Between(11, 19) => PluralRulesValues.Many,
            "mt" => PluralRulesValues.Other,
            "my" => PluralRulesValues.Other,
            "naq" when prc.n == 1 => PluralRulesValues.One,
            "naq" when prc.n == 2 => PluralRulesValues.Two,
            "naq" => PluralRulesValues.Other,
            "nb" when prc.n == 1 => PluralRulesValues.One,
            "nb" => PluralRulesValues.Other,
            "nd" when prc.n == 1 => PluralRulesValues.One,
            "nd" => PluralRulesValues.Other,
            "ne" when prc.n == 1 => PluralRulesValues.One,
            "ne" => PluralRulesValues.Other,
            "nl" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "nl" => PluralRulesValues.Other,
            "nn" when prc.n == 1 => PluralRulesValues.One,
            "nn" => PluralRulesValues.Other,
            "nnh" when prc.n == 1 => PluralRulesValues.One,
            "nnh" => PluralRulesValues.Other,
            "nqo" => PluralRulesValues.Other,
            "nr" when prc.n == 1 => PluralRulesValues.One,
            "nr" => PluralRulesValues.Other,
            "nso" when prc.n == prc.i && prc.n.Between(0, 1) => PluralRulesValues.One,
            "nso" => PluralRulesValues.Other,
            "nyn" when prc.n == 1 => PluralRulesValues.One,
            "nyn" => PluralRulesValues.Other,
            "om" when prc.n == 1 => PluralRulesValues.One,
            "om" => PluralRulesValues.Other,
            "or" when prc.n == 1 => PluralRulesValues.One,
            "or" => PluralRulesValues.Other,
            "os" when prc.n == 1 => PluralRulesValues.One,
            "os" => PluralRulesValues.Other,
            "pa" when prc.n == prc.i && prc.n.Between(0, 1) => PluralRulesValues.One,
            "pa" => PluralRulesValues.Other,
            "pl" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "pl" when prc.v == 0 && (prc.i % 10).Between(2, 4) && (prc.i % 100).NotBetween(12, 14) => PluralRulesValues
               .Few,
            "pl" when (prc.v == 0 && prc.i != 1 && (prc.i % 10).Between(0, 1)) ||
                      (prc.v == 0 && (prc.i               % 10).Between(5, 9)) ||
                      (prc.v == 0 && (prc.i               % 100).Between(12, 14)) => PluralRulesValues.Many,
            "pl" => PluralRulesValues.Other,
            "prg" when prc.n % 10 == 0 || (prc.n % 100).Between(11, 19) || (prc.v == 2 && (prc.f % 100).Between(11, 19))
                => PluralRulesValues.Zero,
            "prg" when (prc.n % 10 == 1 && prc.n % 100 != 11) || (prc.v == 2 && prc.f % 10 == 1 && prc.f % 100 != 11) ||
                       (prc.v      != 2 && prc.f % 10  == 1) => PluralRulesValues.One,
            "prg" => PluralRulesValues.Other,
            "ps" when prc.n == 1 => PluralRulesValues.One,
            "ps" => PluralRulesValues.Other,
            "pt" when prc.i.Between(0, 1) => PluralRulesValues.One,
            "pt" => PluralRulesValues.Other,
            "pt-pt" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "pt-pt" => PluralRulesValues.Other,
            "rm" when prc.n == 1 => PluralRulesValues.One,
            "rm" => PluralRulesValues.Other,
            "ro" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "ro" when prc.v != 0 || prc.n == 0 || (prc.n % 100).Between(2, 19) => PluralRulesValues.Few,
            "ro" => PluralRulesValues.Other,
            "rof" when prc.n == 1 => PluralRulesValues.One,
            "rof" => PluralRulesValues.Other,
            "ru" when prc.v == 0 && prc.i % 10 == 1 && prc.i % 100 != 11 => PluralRulesValues.One,
            "ru" when prc.v == 0 && (prc.i % 10).Between(2, 4) && (prc.i % 100).NotBetween(12, 14) => PluralRulesValues
               .Few,
            "ru" when (prc.v == 0 && prc.i % 10 == 0) || (prc.v == 0 && (prc.i % 10).Between(5, 9)) ||
                      (prc.v == 0 && (prc.i % 100).Between(11, 14)) => PluralRulesValues.Many,
            "ru" => PluralRulesValues.Other,
            "rwk" when prc.n == 1 => PluralRulesValues.One,
            "rwk" => PluralRulesValues.Other,
            "sah" => PluralRulesValues.Other,
            "saq" when prc.n == 1 => PluralRulesValues.One,
            "saq" => PluralRulesValues.Other,
            "sd" when prc.n == 1 => PluralRulesValues.One,
            "sd" => PluralRulesValues.Other,
            "se" when prc.n == 1 => PluralRulesValues.One,
            "se" when prc.n == 2 => PluralRulesValues.Two,
            "se" => PluralRulesValues.Other,
            "seh" when prc.n == 1 => PluralRulesValues.One,
            "seh" => PluralRulesValues.Other,
            "ses" => PluralRulesValues.Other,
            "sg" => PluralRulesValues.Other,
            "shi" when prc.i == 0 || prc.n == 1 => PluralRulesValues.One,
            "shi" when prc.n == prc.i && prc.n.Between(2, 10) => PluralRulesValues.Few,
            "shi" => PluralRulesValues.Other,
            "si" when prc.n.In(0, 1) || (prc.i == 0 && prc.f == 1) => PluralRulesValues.One,
            "si" => PluralRulesValues.Other,
            "sk" when prc.i == 1          && prc.v == 0 => PluralRulesValues.One,
            "sk" when prc.i.Between(2, 4) && prc.v == 0 => PluralRulesValues.Few,
            "sk" when prc.v != 0 => PluralRulesValues.Many,
            "sk" => PluralRulesValues.Other,
            "sl" when prc.v == 0 && prc.i % 100 == 1 => PluralRulesValues.One,
            "sl" when prc.v == 0 && prc.i % 100 == 2 => PluralRulesValues.Two,
            "sl" when (prc.v == 0 && (prc.i % 100).Between(3, 4)) || prc.v != 0 => PluralRulesValues.Few,
            "sl" => PluralRulesValues.Other,
            "sma" when prc.n == 1 => PluralRulesValues.One,
            "sma" when prc.n == 2 => PluralRulesValues.Two,
            "sma" => PluralRulesValues.Other,
            "smj" when prc.n == 1 => PluralRulesValues.One,
            "smj" when prc.n == 2 => PluralRulesValues.Two,
            "smj" => PluralRulesValues.Other,
            "smn" when prc.n == 1 => PluralRulesValues.One,
            "smn" when prc.n == 2 => PluralRulesValues.Two,
            "smn" => PluralRulesValues.Other,
            "sms" when prc.n == 1 => PluralRulesValues.One,
            "sms" when prc.n == 2 => PluralRulesValues.Two,
            "sms" => PluralRulesValues.Other,
            "sn" when prc.n == 1 => PluralRulesValues.One,
            "sn" => PluralRulesValues.Other,
            "so" when prc.n == 1 => PluralRulesValues.One,
            "so" => PluralRulesValues.Other,
            "sq" when prc.n == 1 => PluralRulesValues.One,
            "sq" => PluralRulesValues.Other,
            "sr" when (prc.v == 0 && prc.i % 10 == 1 && prc.i % 100 != 11) || (prc.f % 10 == 1 && prc.f % 100 != 11) =>
                PluralRulesValues.One,
            "sr" when (prc.v == 0 && (prc.i % 10).Between(2, 4) && (prc.i % 100).NotBetween(12, 14)) ||
                      ((prc.f % 10).Between(2, 4) && (prc.f % 100).NotBetween(12, 14)) => PluralRulesValues.Few,
            "sr" => PluralRulesValues.Other,
            "ss" when prc.n == 1 => PluralRulesValues.One,
            "ss" => PluralRulesValues.Other,
            "ssy" when prc.n == 1 => PluralRulesValues.One,
            "ssy" => PluralRulesValues.Other,
            "st" when prc.n == 1 => PluralRulesValues.One,
            "st" => PluralRulesValues.Other,
            "sv" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "sv" => PluralRulesValues.Other,
            "sw" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "sw" => PluralRulesValues.Other,
            "syr" when prc.n == 1 => PluralRulesValues.One,
            "syr" => PluralRulesValues.Other,
            "ta" when prc.n == 1 => PluralRulesValues.One,
            "ta" => PluralRulesValues.Other,
            "te" when prc.n == 1 => PluralRulesValues.One,
            "te" => PluralRulesValues.Other,
            "teo" when prc.n == 1 => PluralRulesValues.One,
            "teo" => PluralRulesValues.Other,
            "th" => PluralRulesValues.Other,
            "ti" when prc.n == prc.i && prc.n.Between(0, 1) => PluralRulesValues.One,
            "ti" => PluralRulesValues.Other,
            "tig" when prc.n == 1 => PluralRulesValues.One,
            "tig" => PluralRulesValues.Other,
            "tk" when prc.n == 1 => PluralRulesValues.One,
            "tk" => PluralRulesValues.Other,
            "tn" when prc.n == 1 => PluralRulesValues.One,
            "tn" => PluralRulesValues.Other,
            "to" => PluralRulesValues.Other,
            "tr" when prc.n == 1 => PluralRulesValues.One,
            "tr" => PluralRulesValues.Other,
            "ts" when prc.n == 1 => PluralRulesValues.One,
            "ts" => PluralRulesValues.Other,
            "tzm" when (prc.n == prc.i && prc.n.Between(0, 1)) || (prc.n == prc.i && prc.n.Between(11, 99)) =>
                PluralRulesValues.One,
            "tzm" => PluralRulesValues.Other,
            "ug" when prc.n == 1 => PluralRulesValues.One,
            "ug" => PluralRulesValues.Other,
            "uk" when prc.v == 0 && prc.i % 10 == 1 && prc.i % 100 != 11 => PluralRulesValues.One,
            "uk" when prc.v == 0 && (prc.i % 10).Between(2, 4) && (prc.i % 100).NotBetween(12, 14) => PluralRulesValues
               .Few,
            "uk" when (prc.v == 0 && prc.i % 10 == 0) || (prc.v == 0 && (prc.i % 10).Between(5, 9)) ||
                      (prc.v == 0 && (prc.i % 100).Between(11, 14)) => PluralRulesValues.Many,
            "uk" => PluralRulesValues.Other,
            "ur" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "ur" => PluralRulesValues.Other,
            "uz" when prc.n == 1 => PluralRulesValues.One,
            "uz" => PluralRulesValues.Other,
            "ve" when prc.n == 1 => PluralRulesValues.One,
            "ve" => PluralRulesValues.Other,
            "vi" => PluralRulesValues.Other,
            "vo" when prc.n == 1 => PluralRulesValues.One,
            "vo" => PluralRulesValues.Other,
            "vun" when prc.n == 1 => PluralRulesValues.One,
            "vun" => PluralRulesValues.Other,
            "wae" when prc.n == 1 => PluralRulesValues.One,
            "wae" => PluralRulesValues.Other,
            "wo" => PluralRulesValues.Other,
            "xh" when prc.n == 1 => PluralRulesValues.One,
            "xh" => PluralRulesValues.Other,
            "xog" when prc.n == 1 => PluralRulesValues.One,
            "xog" => PluralRulesValues.Other,
            "yi" when prc.i == 1 && prc.v == 0 => PluralRulesValues.One,
            "yi" => PluralRulesValues.Other,
            "yo" => PluralRulesValues.Other,
            "zh" => PluralRulesValues.Other,
            "zu" when prc.i == 0 || prc.n == 1 => PluralRulesValues.One,
            "zu" => PluralRulesValues.Other,
            _ => null
        };
    }

    private enum PluralRulesValues {
        Zero, One, Two, Few, Many, Other
    }

     /// <summary>
    /// Defines a Plural Rules evaluation context
    /// </summary>
    /// <seealso href="http://unicode.org/reports/tr35/tr35-numbers.html#Operands"/>
    public readonly struct Struct
    {
        /// <summary>
        /// Absolute value of the source number.
        /// </summary>
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
        public decimal n { get; }

        /// <summary>
        /// Integer digits of n.
        /// </summary>
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
        public int i { get; }

        /// <summary>
        /// Number of visible fraction digits in n, with trailing zeros.
        /// </summary>
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
        public int v { get; }

        /// <summary>
        /// Currently, synonym for ‘c’. however, may be redefined in the future.
        /// </summary>
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
        public int e { get; }

        /// <summary>
        /// Visible fractional digits in n, with trailing zeros.
        /// </summary>
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
        public int f { get; }

        /// <summary>
        /// Visible fractional digits in n, without trailing zeros.
        /// </summary>
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
        public int t { get; }

        public Struct(decimal n) : this() {
            this.n = n;
            i      = (int)Math.Truncate(n);
            var initial = n.ToString(CultureInfo.InvariantCulture);

            if (initial.Contains(".")) {
                var decimals = initial.Split('.')[1];
                f = int.Parse(decimals, CultureInfo.InvariantCulture);
                v = decimals.Length;
                var trimmed = decimals.TrimEnd('0');
                t = trimmed.Length > 0 ? int.Parse(trimmed, CultureInfo.InvariantCulture) : 0;
            }
        }
    }
}
public static class NumberExtensions
    {
        /// <summary>
        /// Check if a number is in a specific list of numbers
        /// </summary>
        /// <param name="input">A number to test</param>
        /// <param name="args">An array of numbers</param>
        /// <returns>true if the number is in the list, otherwise false</returns>
        public static bool In(this decimal input, params decimal[] args) => args.Contains(input);

        /// <summary>
        /// Check if a number is in a specific list of numbers
        /// </summary>
        /// <param name="input">A number to test</param>
        /// <param name="args">An array of numbers</param>
        /// <returns>true if the number is in the list, otherwise false</returns>
        public static bool In(this int input, params decimal[] args) => ((decimal)input).In(args);

        /// <summary>
        /// Check if a number is not in a specific list of numbers
        /// </summary>
        /// <param name="input">A number to test</param>
        /// <param name="args">An array of numbers</param>
        /// <returns>true if the number is not in the list, otherwise false</returns>
        public static bool NotIn(this decimal input, params decimal[] args) => !input.In(args);

        /// <summary>
        /// Check if a number is in not a specific list of numbers
        /// </summary>
        /// <param name="input">A number to test</param>
        /// <param name="args">An array of numbers</param>
        /// <returns>true if the number is not in the list, otherwise false</returns>
        public static bool NotIn(this int input, params decimal[] args) => ((decimal)input).NotIn(args);

        /// <summary>
        /// Check if a number is between two specific numbers
        /// </summary>
        /// <param name="input">A number to test</param>
        /// <param name="from">The lowest number included</param>
        /// <param name="to">The highest number included</param>
        /// <returns>true if the number is between the two specified numbers, otherwise false</returns>
        public static bool Between(this decimal input, int from, int to) => input >= from && input <= to;

        /// <summary>
        /// Check if a number is between two specific numbers
        /// </summary>
        /// <param name="input">A number to test</param>
        /// <param name="from">The lowest number included</param>
        /// <param name="to">The highest number included</param>
        /// <returns>true if the number is between the two specified numbers, otherwise false</returns>
        public static bool Between(this int input, int from, int to) => ((decimal)input).Between(from, to);

        /// <summary>
        /// Check if a number is not between two specific numbers
        /// </summary>
        /// <param name="input">A number to test</param>
        /// <param name="from">The lowest number included</param>
        /// <param name="to">The highest number included</param>
        /// <returns>true if the number is not between the two specified numbers, otherwise false</returns>
        public static bool NotBetween(this decimal input, int from, int to) => !input.Between(from, to);

        /// <summary>
        /// Check if a number is not between two specific numbers
        /// </summary>
        /// <param name="input">A number to test</param>
        /// <param name="from">The lowest number included</param>
        /// <param name="to">The highest number included</param>
        /// <returns>true if the number is not between the two specified numbers, otherwise false</returns>
        public static bool NotBetween(this int input, int from, int to) => ((decimal)input).NotBetween(from, to);
    }
