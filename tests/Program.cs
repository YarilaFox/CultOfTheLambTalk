using System;
using FollowerTalk;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("Failed: " + name);
}

Check(TalkLanguage.IsValid(TalkLanguage.Auto), "auto is valid");
Check(!TalkLanguage.IsValid("de"), "unsupported manual mode is invalid");
Check(TalkLanguage.Ui(TalkLanguage.Auto, true) == TalkLanguage.Russian, "Russian UI fallback");
Check(TalkLanguage.Ui(TalkLanguage.Auto, false) == TalkLanguage.English, "English UI fallback");
Check(TalkLanguage.Ui(TalkLanguage.English, true) == TalkLanguage.English, "manual UI override");

string mode = TalkLanguage.Auto;
string fallback = TalkLanguage.English;
Check(TalkLanguage.Context(mode, "Привет, как дела?", fallback) == TalkLanguage.Russian,
    "first Russian turn");
Check(TalkLanguage.Context(mode, "What are you doing?", fallback) == TalkLanguage.English,
    "next English turn");
Check(TalkLanguage.Context(mode, "Я люблю Minecraft", fallback) == TalkLanguage.Russian,
    "Russian with a Latin game name");
Check(TalkLanguage.Context(mode, "I love Майнкрафт", fallback) == TalkLanguage.English,
    "English with a Cyrillic game name");
Check(TalkLanguage.Context(mode, "...", TalkLanguage.Russian) == TalkLanguage.Russian,
    "punctuation uses UI fallback");
Check(TalkLanguage.Context(mode, "こんにちは", TalkLanguage.Russian) == TalkLanguage.English,
    "other scripts use the multilingual English context");
Check(TalkLanguage.Context(TalkLanguage.Russian, "Hello", fallback) == TalkLanguage.Russian,
    "manual language overrides player text");
Check(TalkLanguage.ReplyRule(TalkLanguage.Auto, TalkLanguage.English).Contains("LATEST player"),
    "auto reply follows latest player language");
Check(TalkLanguage.ReplyRule(TalkLanguage.Russian, TalkLanguage.Russian).Contains("по-русски"),
    "manual Russian reply");
Console.WriteLine("Language checks passed.");

Check(UiText.Languages.Length == 15, "game mode plus 14 game languages");
foreach (string locale in UiText.Languages)
{
    if (locale == UiText.Game) continue;
    foreach (string key in new[] { "talk.title", "input.hint", "settings.title",
        "ui.language", "key.required", "response.invalid", "conflict.mode",
        "conflict.off", "conflict.feelings", "conflict.leave" })
        Check(UiText.Get(locale, key) != key, locale + ":" + key);
    if (locale != "en")
        Check(UiText.Get(locale, "talk.title") != UiText.Get("en", "talk.title"),
            "translated talk title: " + locale);
}
Check(UiText.Resolve(UiText.Game, "ru-RU") == "ru", "Russian game locale");
Check(UiText.Resolve(UiText.Game, "fr-FR") == "fr", "French game locale");
Check(UiText.Resolve(UiText.Game, "pt-BR") == "pt-BR", "Brazilian Portuguese locale");
Check(UiText.Resolve(UiText.Game, "ChineseTraditional") == "zh-Hant",
    "Traditional Chinese game locale");
Check(UiText.Resolve("ar", "ru-RU") == "ar", "manual UI override");
Console.WriteLine("UI locale checks passed.");

var draft = new DialogueDraft();
draft.Insert("Привет мир");
draft.Move(7, false);
draft.Insert("мой ");
Check(draft.Text == "Привет мой мир", "insert at caret");
draft.Move(7, false);
draft.Move(10, true);
Check(draft.SelectedText() == "мой", "shift selection");
draft.Insert("мы");
Check(draft.Text == "Привет мы мир", "replace selection");
draft.SelectAll();
draft.Insert("A B");
Check(draft.Text == "A B", "select all and paste");
draft.Move(1, false);
draft.DeleteForward();
Check(draft.Text == "AB", "delete within line");
draft.Insert(" ");
Check(draft.Text == "A B", "space inserts at caret");
Check(draft.Display("hint").Contains("▌"), "visible caret");
Console.WriteLine("Dialogue editing checks passed.");

Check(FollowerConflictRules.IsValidMode(FollowerConflictRules.Off), "off mode");
Check(FollowerConflictRules.Level(0) == "calm", "calm level");
Check(FollowerConflictRules.Level(20) == "hurt", "hurt threshold");
Check(FollowerConflictRules.Level(45) == "resentful", "resentful threshold");
Check(FollowerConflictRules.Level(75) == "ready_to_leave", "departure threshold");
Check(FollowerConflictRules.Apply(95, "deep_hurt") == 100, "grievance cap");
Check(FollowerConflictRules.Apply(5, "deep_repair") == 0, "repair floor");
Check(!FollowerConflictRules.AllowDeparture(FollowerConflictRules.Feelings, 100, "leave", false),
    "feelings mode cannot leave");
Check(!FollowerConflictRules.AllowDeparture(FollowerConflictRules.MayLeave, 74, "leave", false),
    "below threshold cannot leave");
Check(!FollowerConflictRules.AllowDeparture(FollowerConflictRules.MayLeave, 100, "leave", true),
    "prisoner cannot leave");
Check(FollowerConflictRules.AllowDeparture(FollowerConflictRules.MayLeave, 75, "leave", false),
    "model can choose departure at final level");
Console.WriteLine("Follower conflict checks passed.");
