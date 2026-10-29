using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using Newtonsoft.Json.Linq;

namespace FollowerTalk;

internal sealed class FollowerMemory
{
    private JObject data = new();
    private string saveId = "";
    private string path = "";

    public List<ConversationTurn> History(Follower follower)
    {
        JArray? turns = FollowerState(follower)["turns"] as JArray;
        var history = new List<ConversationTurn>();
        if (turns == null) return history;
        foreach (JToken turn in turns)
        {
            string text = turn["text"]?.Value<string>() ?? "";
            if (!string.IsNullOrWhiteSpace(text))
                history.Add(new ConversationTurn(turn["player"]?.Value<bool>() == true, text));
        }
        return history;
    }

    public void AddTurn(Follower follower, bool player, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        JObject state = FollowerState(follower);
        JArray turns = state["turns"] as JArray ?? new JArray();
        if (player && turns.Count > 0 && turns.Last?["player"]?.Value<bool>() == true)
            turns.RemoveAt(turns.Count - 1);
        turns.Add(new JObject { ["player"] = player, ["text"] = text.Trim() });
        while (turns.Count > (player ? 11 : 10)) turns.RemoveAt(0);
        state["turns"] = turns;
        Save();
    }

    public void Remember(Follower follower, string note, string source)
    {
        note = note.Trim();
        if (note.Length == 0) return;
        if (note.Length > 240) note = note.Substring(0, 240);
        JObject state = FollowerState(follower);
        JArray notes = state["notes"] as JArray ?? new JArray();
        if (notes.Any(item => string.Equals(item["text"]?.Value<string>(), note,
            StringComparison.OrdinalIgnoreCase))) return;
        notes.Add(new JObject
        {
            ["text"] = note,
            ["source"] = source.Length <= 320 ? source : source.Substring(0, 320),
            ["day"] = DataManager.Instance.CurrentDayIndex
        });
        while (notes.Count > 8) notes.RemoveAt(0);
        state["notes"] = notes;
        Save();
    }

    public bool Announce(string text)
    {
        EnsureSave();
        if (path.Length == 0) return false;
        text = text.Trim();
        if (text.Length == 0) return false;
        if (text.Length > 320) text = text.Substring(0, 320);
        JArray announcements = data["announcements"] as JArray ?? new JArray();
        announcements.Add(new JObject
        {
            ["text"] = text,
            ["day"] = DataManager.Instance.CurrentDayIndex
        });
        while (announcements.Count > 5) announcements.RemoveAt(0);
        data["announcements"] = announcements;
        Match address = Regex.Match(text,
            @"(?:называйте|зовите)\s+меня\s+(.+?)(?:[.!?]|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (address.Success)
            data["preferred_address"] = address.Groups[1].Value.Trim(' ', '"', '\'');
        return Save();
    }

    public string Context(Follower follower)
    {
        JObject state = FollowerState(follower);
        var parts = new List<string>();
        if (state["notes"] is JArray notes && notes.Count > 0)
            parts.Add("Личные важные наблюдения: " + string.Join("; ", notes
                .Select(item =>
                {
                    string? note = item["text"]?.Value<string>();
                    string? source = item["source"]?.Value<string>();
                    if (string.IsNullOrEmpty(note)) return null;
                    if (string.IsNullOrEmpty(source)) return note;
                    return note + " (свидетельство: " + source.Substring(0,
                        Math.Min(120, source.Length)) + ")";
                }).Where(item => !string.IsNullOrEmpty(item))));
        if (data["announcements"] is JArray announcements && announcements.Count > 0)
            parts.Add("Слова игрока, сказанные всему культу у алтаря: " +
                string.Join("; ", announcements.Select(item => item["text"]?.Value<string>())
                    .Where(item => !string.IsNullOrEmpty(item))));
        string? address = data["preferred_address"]?.Value<string>();
        if (!string.IsNullOrWhiteSpace(address))
            parts.Add("Игрок попросил обращаться к нему как «" + address +
                "». Если обращаешься к игроку по имени или титулу, используй это обращение " +
                "в подходящей грамматической форме; не повторяй его в каждой реплике");
        return parts.Count == 0 ? "Личных и общих долговременных заметок пока нет." :
            string.Join(". ", parts) + ".";
    }

    private JObject FollowerState(Follower follower)
    {
        EnsureSave();
        JObject followers = data["followers"] as JObject ?? new JObject();
        data["followers"] = followers;
        string id = follower.Brain.Info.ID.ToString();
        JObject state = followers[id] as JObject ?? new JObject();
        followers[id] = state;
        return state;
    }

    private void EnsureSave()
    {
        string id = DataManager.Instance?.SaveUniqueID ?? "";
        if (id == saveId) return;
        saveId = id;
        data = new JObject { ["version"] = 1 };
        path = "";
        if (string.IsNullOrWhiteSpace(id)) return;
        using SHA256 sha = SHA256.Create();
        string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id)))
            .Replace("-", "").Substring(0, 20);
        path = Path.Combine(Paths.ConfigPath, "FollowerTalk.memory." + hash + ".json");
        try
        {
            if (File.Exists(path)) data = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception error)
        {
            path = "";
            Plugin.Instance.LogMemoryError(error);
        }
    }

    private bool Save()
    {
        if (path.Length == 0) return false;
        try
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, data.ToString(), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            return true;
        }
        catch (Exception error)
        {
            Plugin.Instance.LogMemoryError(error);
            return false;
        }
    }
}
