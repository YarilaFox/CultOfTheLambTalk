namespace FollowerTalk;

internal static class TalkLanguage
{
    public const string Auto = "auto";
    public const string Russian = "ru";
    public const string English = "en";

    public static bool IsValid(string mode) =>
        mode == Auto || mode == Russian || mode == English;

    public static string Ui(string mode, bool gameIsRussian) =>
        mode == Russian ? Russian : mode == English ? English :
        gameIsRussian ? Russian : English;

    public static string Context(string mode, string playerMessage, string fallback)
    {
        if (mode != Auto) return mode == Russian ? Russian : English;
        int cyrillic = 0;
        int latin = 0;
        int other = 0;
        int previousScript = 0;
        foreach (char character in playerMessage)
        {
            int script = character >= '\u0400' && character <= '\u052f' ? 1 :
                (character >= 'a' && character <= 'z') ||
                (character >= 'A' && character <= 'Z') ? 2 : 0;
            if (script == 1 && previousScript != 1) cyrillic++;
            if (script == 2 && previousScript != 2) latin++;
            if (script == 0 && previousScript != 3 && char.IsLetter(character)) other++;
            if (script == 0 && char.IsLetter(character)) script = 3;
            previousScript = script;
        }
        if (cyrillic >= latin && cyrillic > 0) return Russian;
        if (latin > 0) return English;
        if (other > 0) return English;
        return fallback;
    }

    public static string ReplyRule(string mode, string language) => mode == Auto
        ? Pick(language,
            "Отвечай на языке ПОСЛЕДНЕГО сообщения игрока, даже если он отличается от языка этого контекста.",
            "Reply in the language of the LATEST player message, even if it differs from this context language.")
        : Pick(language, "Отвечай по-русски.", "Reply in English.");

    public static string Pick(string language, string russian, string english) =>
        language == Russian ? russian : english;
}
