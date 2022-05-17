using System;

namespace I18Next.Net;

public class TranslationOptions
{
    private string _defaultNamespace;

    public string DefaultNamespace
    {
        get => _defaultNamespace;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{nameof(DefaultNamespace)} cannot be blank.", nameof(value));

            _defaultNamespace = value;
        }
    }

    public string[] FallbackLanguages { get; set; }

    public string[] FallbackNamespaces { get; set; }
}
