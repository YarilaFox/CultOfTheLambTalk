using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace FollowerTalk;

internal static class UiText
{
    public const string Game = "game";
    public static readonly string[] Languages =
    {
        Game, "en", "fr", "de", "es", "ja", "ko", "pt-BR", "ru",
        "zh-Hans", "zh-Hant", "it", "ar", "nl", "tr"
    };

    public static readonly string[] LanguageNames =
    {
        "Game", "English", "Français", "Deutsch", "Español", "日本語", "한국어",
        "Português", "Русский", "简体中文", "繁體中文", "Italiano", "العربية",
        "Nederlands", "Türkçe"
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Catalog = Load();

    public static bool IsValidMode(string mode) => Array.IndexOf(Languages, mode) >= 0;

    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "en";
        string code = language.Trim().Replace('_', '-').ToLowerInvariant();
        if (code.StartsWith("zh") || code.StartsWith("chinese"))
            return code.Contains("hant") || code.Contains("tw") || code.Contains("hk") ||
                   code.Contains("traditional") ? "zh-Hant" : "zh-Hans";
        if (code.StartsWith("pt") || code.StartsWith("portuguese")) return "pt-BR";
        if (code.StartsWith("es") || code == "spanish") return "es";
        if (code.StartsWith("fr") || code == "french") return "fr";
        if (code.StartsWith("de") || code == "german") return "de";
        if (code.StartsWith("ja") || code == "japanese") return "ja";
        if (code.StartsWith("ko") || code == "korean") return "ko";
        if (code.StartsWith("ru") || code == "russian") return "ru";
        if (code.StartsWith("it") || code == "italian") return "it";
        if (code.StartsWith("ar") || code == "arabic") return "ar";
        if (code.StartsWith("nl") || code == "dutch") return "nl";
        if (code.StartsWith("tr") || code == "turkish") return "tr";
        return "en";
    }

    public static string Resolve(string mode, string gameLanguage) =>
        mode == Game ? Normalize(gameLanguage) :
        IsValidMode(mode) ? mode : Normalize(gameLanguage);

    public static string Get(string language, string key)
    {
        if (Catalog.TryGetValue(language, out var values) &&
            values.TryGetValue(key, out string? value) && value != null) return value;
        return Catalog.TryGetValue("en", out var english) &&
            english.TryGetValue(key, out string? fallback) && fallback != null ? fallback : key;
    }

    private static Dictionary<string, Dictionary<string, string>> Load()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("FollowerTalk.ui.json")
            ?? throw new InvalidOperationException("FollowerTalk.ui.json is missing.");
        using var reader = new StreamReader(stream);
        JObject json = JObject.Parse(reader.ReadToEnd());
        var keys = json["keys"] as JArray ?? throw new InvalidDataException("Missing UI keys.");
        var locales = json["locales"] as JObject ?? throw new InvalidDataException("Missing UI locales.");
        var catalog = new Dictionary<string, Dictionary<string, string>>();
        foreach (JProperty locale in locales.Properties())
        {
            if (locale.Value is not JArray entries || entries.Count != keys.Count)
                throw new InvalidDataException("Incomplete UI locale: " + locale.Name);
            var values = new Dictionary<string, string>();
            for (int index = 0; index < keys.Count; index++)
            {
                string key = keys[index]?.Value<string>() ?? "";
                string value = entries[index]?.Value<string>() ?? "";
                if (key.Length == 0 || value.Length == 0 || values.ContainsKey(key))
                    throw new InvalidDataException("Invalid UI text in " + locale.Name);
                values[key] = value;
            }
            catalog[locale.Name] = values;
        }
        foreach (string language in Languages)
            if (language != Game && !catalog.ContainsKey(language))
                throw new InvalidDataException("Missing UI locale: " + language);
        return catalog;
    }
}
