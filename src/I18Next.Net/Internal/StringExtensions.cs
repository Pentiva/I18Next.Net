﻿using System;

namespace I18Next.Net.Internal;

public static class StringExtensions
{
    public static string ReplaceFirst(this string text, string search, string replace)
    {
        var pos = text.IndexOf(search, StringComparison.Ordinal);
        if (pos < 0)
            return text;

        #if NET6_0_OR_GREATER
        return string.Concat(text.AsSpan(0, pos), replace, text.AsSpan(pos + search.Length));
        #else
        return text[..pos] + replace + text[(pos + search.Length)..];
        #endif
    }

    public static string[] Split(this string str, string splitter, int count, StringSplitOptions options)
    {
        return str.Split(new[] { splitter }, count, options);
    }

    public static string[] Split(this string str, string splitter, int count)
    {
        return str.Split(new[] { splitter }, count, StringSplitOptions.None);
    }

    public static string[] Split(this string str, string splitter)
    {
        return str.Split(new[] { splitter }, StringSplitOptions.None);
    }

    public static string[] Split(this string str, string splitter, StringSplitOptions options)
    {
        return str.Split(new[] { splitter }, options);
    }
}
