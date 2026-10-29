using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FollowerTalk;

internal sealed class FollowerRequests
{
    private readonly string path = Path.Combine(Paths.ConfigPath, "FollowerTalk.requests.json");
    private readonly JObject entries;
    private float nextCheck;

    public FollowerRequests()
    {
        try
        {
            entries = File.Exists(path)
                ? JObject.Parse(File.ReadAllText(path, Encoding.UTF8)) : new JObject();
        }
        catch (Exception error)
        {
            Plugin.Instance.LogRequestError(error);
            entries = new JObject();
        }
    }

    public static bool IsKnownRequest(string request) =>
        request == "none" || request == "build_lumberyard" || request == "build_farm_station";

    public string Context(Follower follower)
    {
        JObject state = State(follower);
        var details = new List<string>();
        if (state["requests"] is JObject requests && requests.Count > 0)
            details.Add("ожидает: " + string.Join(", ", requests.Properties()
                .Select(item => RequestName(item.Name))));
        string? fulfilled = state["fulfilled"]?.Value<string>();
        if (!string.IsNullOrEmpty(fulfilled))
            details.Add("после его просьбы появилась " + RequestName(fulfilled) +
                "; игра уже учла выполненную просьбу");
        return details.Count == 0 ? "Нет ожидающих личных просьб." : string.Join("; ", details);
    }

    public void ObserveReply(Follower follower, ConversationDecision decision)
    {
        JObject state = State(follower);
        bool changed = false;
        bool saidRequest = decision.Request == "build_lumberyard"
            ? decision.Reply.IndexOf("лесопил", StringComparison.OrdinalIgnoreCase) >= 0
            : decision.Request == "build_farm_station" &&
              decision.Reply.IndexOf("ферм", StringComparison.OrdinalIgnoreCase) >= 0;
        if (saidRequest)
        {
            JObject requests = state["requests"] as JObject ?? new JObject();
            if (requests[decision.Request] == null)
            {
                requests[decision.Request] = CountStructures(decision.Request);
                state["requests"] = requests;
                changed = true;
            }
        }

        if (state["fulfilled"] != null)
        {
            state.Remove("fulfilled");
            changed = true;
        }
        if (changed) Save();
    }

    public void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        if (PlayerFarming.Instance == null || PlayerFarming.Location != FollowerLocation.Base) return;

        string prefix = SaveAndLoad.SAVE_SLOT + ":";
        bool changed = false;
        foreach (JProperty follower in entries.Properties().ToList())
        {
            if (!follower.Name.StartsWith(prefix, StringComparison.Ordinal) ||
                follower.Value is not JObject state || state["requests"] is not JObject requests)
                continue;
            foreach (JProperty request in requests.Properties().ToList())
            {
                if (CountStructures(request.Name) <= request.Value.Value<int>()) continue;
                if (!int.TryParse(follower.Name.Substring(prefix.Length), out int followerId))
                    continue;
                FollowerBrain? brain = FollowerBrain.FindBrainByID(followerId);
                if (brain == null || brain.Stats == null || brain.Stats.HasLevelledUp ||
                    DataManager.Instance == null || !DataManager.Instance.ShowLoyaltyBars)
                    continue;
                brain.AddAdoration(FollowerBrain.AdorationActions.Quest, null);
                state["fulfilled"] = request.Name;
                requests.Remove(request.Name);
                changed = true;
                Plugin.Instance.LogRequestReward(follower.Name, request.Name);
            }
        }
        if (changed) Save();
    }

    private JObject State(Follower follower)
    {
        string key = SaveAndLoad.SAVE_SLOT + ":" + follower.Brain.Info.ID;
        if (entries[key] is JObject state) return state;
        state = new JObject { ["requests"] = new JObject() };
        entries[key] = state;
        return state;
    }

    private static string RequestName(string request) => request switch
    {
        "build_lumberyard" => "лесопилка",
        "build_farm_station" => "фермерская станция",
        _ => request
    };

    private static int CountStructures(string request)
    {
        try
        {
            if (request == "build_lumberyard")
            {
                var stations = new List<Structures_LumberjackStation>();
                StructureManager.TryGetAllStructuresOfType(ref stations, FollowerLocation.Base, null);
                return stations.Count(station => station != null && station.Data != null);
            }
            if (request == "build_farm_station")
            {
                var stations = new List<Structures_FarmerStation>();
                StructureManager.TryGetAllStructuresOfType(ref stations, FollowerLocation.Base, null);
                return stations.Count(station => station != null && station.Data != null);
            }
        }
        catch (Exception error) { Plugin.Instance.LogRequestError(error); }
        return 0;
    }

    private void Save()
    {
        try
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, entries.ToString(), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        catch (Exception error) { Plugin.Instance.LogRequestError(error); }
    }
}
