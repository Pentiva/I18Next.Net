﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using I18Next.Net.Internal;
using I18Next.Net.Plugins;

namespace I18Next.Net.Formatters;

/// <summary>
///     Translates MomentJS tokens to .Net format tokens and uses this to format DateTime and DateTimeOffset values.
/// </summary>
public class MomentJsFormatter : IFormatter
{
    private static readonly Dictionary<string, string> LocalTokenMap = new()
    {
        { "LT", "t" },
        { "LTS", "T" },
        { "L", "d" },
        { "l", "d" },
        { "LL", "D" },
        { "ll", "D" },
        { "LLL", "G" },
        { "lll", "g" },
        { "LLLL", "F" },
        { "llll", "f" }
    };

    private static readonly Regex LocalTokenRegex = new(@"(\[[^\[]*\])|(\\)?(LTS|LT|LL?L?L?|l{1,4}|\[)");

    private static readonly Dictionary<string, string> TokenMap = new()
    {
        { "M", "M" },
        { "Mo", "-" },
        { "MM", "MM" },
        { "MMM", "MMM" },
        { "MMMM", "MMMM" },
        { "Q", "-" },
        { "Qo", "-" },
        { "D", "d" },
        { "Do", "-" },
        { "DD", "dd" },
        { "DDD", "-" },
        { "DDDo", "-" },
        { "DDDD", "-" },
        { "d", "-" },
        { "do", "-" },
        { "dd", "ddd" },
        { "ddd", "dddd" },
        { "dddd", "ddddd" },
        { "e", "-" },
        { "E", "-" },
        { "w", "-" },
        { "wo", "-" },
        { "ww", "-" },
        { "W", "-" },
        { "Wo", "-" },
        { "WW", "-" },
        { "YY", "yy" },
        { "YYYY", "yyyy" },
        { "Y", "\\Y" },
        { "gg", "\\g\\g" },
        { "gggg", "\\G\\G" },
        { "GG", "\\g\\g" },
        { "GGGG", "\\G\\G\\G\\G" },
        { "A", "tt" },
        { "a", "-" },
        { "H", "H" },
        { "HH", "HH" },
        { "h", "h" },
        { "hh", "hh" },
        { "k", "-" },
        { "kk", "-" },
        { "m", "m" },
        { "mm", "mm" },
        { "s", "s" },
        { "ss", "ss" },
        { "S", "f" },
        { "SS", "ff" },
        { "SSS", "fff" },
        { "SSSS", "ffff" },
        { "SSSSS", "fffff" },
        { "SSSSSS", "ffffff" },
        { "SSSSSSS", "fffffff" },
        { "SSSSSSSS", "-" },
        { "SSSSSSSSS", "-" },
        { "z", "-" },
        { "zz", "-" },
        { "Z", "zzz" },
        { "ZZ", "-" },
        { "X", "-" },
        { "x", "-" }
    };

    private static readonly Regex TokenRegex =
        new(
            @"(\[[^\[]*\])|(\\)?([Hh]mm(ss)?|Mo|MM?M?M?|Do|DDDo|DD?D?D?|ddd?d?|do?|w[o|w]?|W[o|W]?|Qo?|YYYYYY|YYYYY|YYYY|YY|gg(ggg?)?|GG(GGG?)?|e|E|a|A|hh?|HH?|kk?|mm?|ss?|S{1,9}|x|X|zz?|ZZ?|.)");

    public bool CanFormat(object value, string format, string language)
    {
        return value is DateTime or DateTimeOffset;
    }

    public string Format(object value, string format, string language)
    {
        if (value == null)
            return null;

        var culture = CultureInfo.GetCultureInfo(language);

        if (value is DateTime dt)
            return ReplaceTokens(dt, format, culture);

        if (value is DateTimeOffset dto)
            return ReplaceTokens(dto, format, culture);

        return value.ToString();
    }

    private static string AddOrdinal(int num)
    {
        if (num <= 0)
            return num.ToString();

        return (num % 100) switch
        {
            11 => num + "th",
            12 => num + "th",
            13 => num + "th",
            _ => (num % 10) switch
            {
                1 => num + "st",
                2 => num + "nd",
                3 => num + "rd",
                _ => num + "th"
            }
        };
    }

    private static int GetQuarter(int month)
    {
        return (month + 2) / 3;
    }

    private static string GetSpecialTokenValue(DateTimeOffset value, string token, CultureInfo culture)
    {
        switch (token)
        {
            case "Mo":   return AddOrdinal(value.Month);
            case "Q":    return GetQuarter(value.Month).ToString();
            case "Qo":   return AddOrdinal(GetQuarter(value.Month));
            case "Do":   return AddOrdinal(value.Day);
            case "DDD":  return value.DayOfYear.ToString();
            case "DDDo": return AddOrdinal(value.DayOfYear);
            case "DDDD": return value.DayOfYear.ToString("000");
            case "d":    return ((int) value.DayOfWeek).ToString();
            case "do":   return AddOrdinal((int) value.DayOfWeek);
            case "e":    return ((int) value.DayOfWeek).ToString();
            case "E":    return ((int) value.DayOfWeek + 1).ToString();
            case "w":
            case "wo":
            case "ww":
            case "W":
            case "Wo":
            case "WW":
                return GetWeekTokenValue(value, token, culture);
            case "a":         return value.ToString("tt", culture).ToLower();
            case "k":         return (value.Hour + 1).ToString();
            case "kk":        return (value.Hour + 1).ToString("00");
            case "SSSSSSSS":  return value.ToString("fffffff00", culture);
            case "SSSSSSSSS": return value.ToString("fffffff000", culture);
            case "z":
            case "zz":
                return TimeZoneData.GetFirstForOffset(value.Offset).Abbreviation;
            case "ZZ": return value.ToString("zzz", culture).Replace(":", "");
            case "X":  return value.ToUnixTimeSeconds().ToString();
            case "x":  return value.ToUnixTimeMilliseconds().ToString();
        }

        return token;
    }

    private static string GetWeekTokenValue(DateTimeOffset value, string token, CultureInfo culture)
    {
        switch (token)
        {
            case "w":
            case "wo":
            case "ww":
                var week = culture.Calendar.GetWeekOfYear(value.DateTime, CalendarWeekRule.FirstDay, culture.DateTimeFormat.FirstDayOfWeek);

                return token switch
                {
                    "ww" => week.ToString("00"),
                    "wo" => AddOrdinal(week),
                    _ => week.ToString()
                };

            case "W":
            case "Wo":
            case "WW":
                var weekIso = culture.Calendar.GetWeekOfYear(value.DateTime, culture.DateTimeFormat.CalendarWeekRule,
                    culture.DateTimeFormat.FirstDayOfWeek);

                return token switch
                {
                    "WW" => weekIso.ToString("00"),
                    "Wo" => AddOrdinal(weekIso),
                    _ => weekIso.ToString()
                };
        }

        return token;
    }

    private static string ReplaceTokens(DateTimeOffset value, string format, CultureInfo culture)
    {
        var lastPosition = 0;
        Match localMatch;
        while ((localMatch = LocalTokenRegex.Match(format, lastPosition)).Success)
        {
            lastPosition = localMatch.Index;

            if (localMatch.Value.StartsWith("["))
            {
                lastPosition += localMatch.Length;
                continue;
            }

            if (localMatch.Value.StartsWith("\\"))
            {
                if (localMatch.Value.StartsWith("\\["))
                {
                    lastPosition += localMatch.Length;
                    continue;
                }

                var tokenValue = localMatch.Value[1..];
                format = SwapStringPart(format, localMatch.Index, localMatch.Length, $"[{tokenValue}]");
                lastPosition += tokenValue.Length + 2;
                continue;
            }

            if (LocalTokenMap.TryGetValue(localMatch.Value, out var newToken))
            {
                var tokenValue = value.ToString(newToken, culture);
                format = SwapStringPart(format, localMatch.Index, localMatch.Length, $"[{tokenValue}]");
                lastPosition += tokenValue.Length + 2;
            }
        }

        var matches = TokenRegex.Matches(format);
        var output = string.Empty;

        foreach (var match in matches.Cast<Match>())
        {
            if (match.Value.StartsWith("["))
            {
                output += match.Value[1..^1];
                continue;
            }

            if (match.Value.StartsWith("\\"))
            {
                output += match.Value[1..];
                continue;
            }

            if (TokenMap.TryGetValue(match.Value, out var newToken))
            {
                if (newToken == "-")
                {
                    output += GetSpecialTokenValue(value, match.Value, culture);
                    continue;
                }

                output += value.ToString(newToken + " ", culture).TrimEnd();
                continue;
            }

            output += match.Value;
        }

        return output;
    }

    private static string SwapStringPart(string source, int index, int length, string newPart)
    {
        var before = source[..index];
        var after = source[(index + length)..];

        return $"{before}{newPart}{after}";
    }
}
