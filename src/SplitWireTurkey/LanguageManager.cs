using System;
using System.Collections.Generic;
using System.Text.Json;
using SplitWireTurkey.Services.Runtime;

namespace SplitWireTurkey
{
    public interface ILanguageResourceProvider
    {
        string BuildPath(string languageCode);
        bool Exists(string path);
        string ReadAllText(string path);
    }

    public sealed class FileLanguageResourceProvider : ILanguageResourceProvider
    {
        private readonly IFileSystem _fileSystem;

        public FileLanguageResourceProvider(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public string BuildPath(string languageCode)
        {
            return _fileSystem.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "res",
                "Languages",
                $"{languageCode.ToLowerInvariant()}.json");
        }

        public bool Exists(string path) => _fileSystem.FileExists(path);

        public string ReadAllText(string path) => _fileSystem.ReadAllText(path);
    }

    /// <summary>
    /// Dil yönetimi için sınıf
    /// </summary>
    public static class LanguageManager
    {
        private static Dictionary<string, object> _currentTranslations = new Dictionary<string, object>();
        private static string _currentLanguage = "TR";
        private static ILanguageResourceProvider _resourceProvider = new FileLanguageResourceProvider();

        /// <summary>
        /// Mevcut dil
        /// </summary>
        public static string CurrentLanguage => _currentLanguage;

        public static void SetResourceProvider(ILanguageResourceProvider provider)
        {
            _resourceProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public static void ResetResourceProvider()
        {
            _resourceProvider = new FileLanguageResourceProvider();
        }

        /// <summary>
        /// Dil dosyasını yükler
        /// </summary>
        public static bool LoadLanguage(string languageCode)
        {
            try
            {
                _currentLanguage = languageCode;

                var languagePath = _resourceProvider.BuildPath(languageCode);

                if (!_resourceProvider.Exists(languagePath))
                {
                    // Fallback olarak TR dilini dene
                    if (languageCode != "TR")
                    {
                        languagePath = _resourceProvider.BuildPath("TR");
                    }

                    if (!_resourceProvider.Exists(languagePath))
                    {
                        return false;
                    }
                }

                var jsonContent = _resourceProvider.ReadAllText(languagePath);
                _currentTranslations = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);

                return _currentTranslations != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dil yüklenirken hata: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Çeviri metnini alır
        /// </summary>
        public static string GetText(string key, params object[] args)
        {
            try
            {
                if (_currentTranslations == null || !_currentTranslations.ContainsKey(key))
                {
                    return key; // Anahtar bulunamazsa anahtarı döndür
                }

                var value = _currentTranslations[key];

                if (value is JsonElement element)
                {
                    var text = element.GetString();
                    if (string.IsNullOrEmpty(text))
                    {
                        return key;
                    }

                    // String.Format benzeri işlem
                    if (args != null && args.Length > 0)
                    {
                        try
                        {
                            return string.Format(text, args);
                        }
                        catch
                        {
                            return text;
                        }
                    }

                    return text;
                }

                return key;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Çeviri alınırken hata: {ex.Message}");
                return key;
            }
        }

        /// <summary>
        /// İç içe geçmiş çeviri anahtarından metin alır (örn: "tabs.main")
        /// </summary>
        public static string GetText(string category, string key, params object[] args)
        {
            try
            {
                if (_currentTranslations == null || !_currentTranslations.ContainsKey(category))
                {
                    return $"{category}.{key}";
                }

                var categoryValue = _currentTranslations[category];
                if (categoryValue is JsonElement categoryElement)
                {
                    var categoryDict = JsonSerializer.Deserialize<Dictionary<string, object>>(categoryElement.GetRawText());
                    if (categoryDict != null && categoryDict.ContainsKey(key))
                    {
                        var value = categoryDict[key];
                        if (value is JsonElement element)
                        {
                            var text = element.GetString();
                            if (string.IsNullOrEmpty(text))
                            {
                                return $"{category}.{key}";
                            }

                            // String.Format benzeri işlem
                            if (args != null && args.Length > 0)
                            {
                                try
                                {
                                    return string.Format(text, args);
                                }
                                catch
                                {
                                    return text;
                                }
                            }

                            return text;
                        }
                    }
                }

                return $"{category}.{key}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"İç içe çeviri alınırken hata: {ex.Message}");
                return $"{category}.{key}";
            }
        }
    }
}
