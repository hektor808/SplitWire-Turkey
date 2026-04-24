using System;
using System.Collections.Generic;
using SplitWireTurkey;
using Xunit;

namespace SplitWireTurkey.Tests;

public sealed class LanguageManagerTests
{
    [Fact]
    public void LoadLanguage_UsesFallbackToTurkish_WhenRequestedLanguageMissing()
    {
        var provider = new FakeLanguageResourceProvider(new Dictionary<string, string>
        {
            ["TR"] = "{\"hello\":\"Merhaba\"}"
        });

        LanguageManager.SetResourceProvider(provider);

        var loaded = LanguageManager.LoadLanguage("EN");
        var translated = LanguageManager.GetText("hello");

        Assert.True(loaded);
        Assert.Equal("Merhaba", translated);

        LanguageManager.ResetResourceProvider();
    }

    [Fact]
    public void GetText_FormatsArguments_WhenTranslationContainsFormatTokens()
    {
        var provider = new FakeLanguageResourceProvider(new Dictionary<string, string>
        {
            ["TR"] = "{\"greet\":\"Merhaba {0}\"}"
        });

        LanguageManager.SetResourceProvider(provider);
        LanguageManager.LoadLanguage("TR");

        var result = LanguageManager.GetText("greet", "Dünya");

        Assert.Equal("Merhaba Dünya", result);

        LanguageManager.ResetResourceProvider();
    }

    private sealed class FakeLanguageResourceProvider : ILanguageResourceProvider
    {
        private readonly Dictionary<string, string> _translations;

        public FakeLanguageResourceProvider(Dictionary<string, string> translations)
        {
            _translations = translations;
        }

        public string BuildPath(string languageCode) => languageCode.ToUpperInvariant();

        public bool Exists(string path) => _translations.ContainsKey(path);

        public string ReadAllText(string path) => _translations[path];
    }
}
