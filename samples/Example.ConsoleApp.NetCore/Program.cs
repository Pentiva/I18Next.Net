﻿using System;
using I18Next.Net;
using I18Next.Net.Backends;
using I18Next.Net.Extensions;
using I18Next.Net.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Example.ConsoleApp.NetCore;

internal class Program
{
    private static ITranslationBackend _backend;

    private static void Main()
    {
        SetupBackend();

        SampleOne();

        SampleTwo();

        Console.ReadKey();
    }

    private static void SampleOne()
    {
        Console.WriteLine("Sample one: Without Microsoft.Extensions.DependencyInjection");

        var translator = new DefaultTranslator(_backend);

        var i18Next = new I18NextNet(_backend, translator);

        Console.WriteLine("English translation:");
        i18Next.Language = "en";
        Console.WriteLine(i18Next.T("exampleKey"));

        Console.WriteLine("German translation:");
        i18Next.Language = "de";
        Console.WriteLine(i18Next.T("exampleKey"));

        Console.WriteLine();
    }

    private static void SampleTwo()
    {
        Console.WriteLine("Sample one: Using Microsoft.Extensions.DependencyInjection");

        var services = new ServiceCollection();

        // Register I18Next.Net
        services.AddI18NextLocalization(builder => builder.AddBackend(_backend).UseFallbackLanguage("en"));

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var scopeProvider = scope.ServiceProvider;

        Console.WriteLine("The first example uses the II18Next interface for direct access to I18Next");

        var i18Next = scopeProvider.GetRequiredService<II18Next>();

        Console.WriteLine("English translation:");
        i18Next.Language = "en";
        Console.WriteLine(i18Next.T("exampleKey"));

        Console.WriteLine("German translation:");
        i18Next.Language = "de";
        Console.WriteLine(i18Next.T("exampleKey"));


        Console.WriteLine();
        Console.WriteLine("The second example uses Microsoft's IStringLocalizer interface for translations.");

        var localizer = scopeProvider.GetRequiredService<IStringLocalizer>();

        Console.WriteLine("English translation:");
        i18Next.Language = "en";
        Console.WriteLine(localizer["exampleKey"]);

        Console.WriteLine("German translation:");
        i18Next.Language = "de";
        Console.WriteLine(localizer["exampleKey"]);


        Console.WriteLine();
        Console.WriteLine("It is also possible to use Microsoft's IStringLocalizer<T> interface for translations.");

        var localizerGeneric = scopeProvider.GetRequiredService<IStringLocalizer<Program>>();

        Console.WriteLine("English translation:");
        i18Next.Language = "en";
        Console.WriteLine(localizerGeneric["exampleKey"]);

        Console.WriteLine("German translation:");
        i18Next.Language = "de";
        Console.WriteLine(localizerGeneric["exampleKey"]);
        Console.WriteLine(localizerGeneric["exampleKey2"]);

        Console.WriteLine();
    }

    private static void SetupBackend()
    {
        var backend = new InMemoryBackend();

        backend.AddTranslation("en", "translation", "exampleKey", "My English text.");
        backend.AddTranslation("en", "translation", "exampleKey2", "My English fallback.");
        backend.AddTranslation("de", "translation", "exampleKey", "Mein deutscher text.");

        _backend = backend;
    }
}
