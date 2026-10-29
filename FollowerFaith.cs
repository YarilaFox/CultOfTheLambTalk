using System;
using System.Collections.Generic;

namespace FollowerTalk;

internal sealed class FollowerFaith
{
    private readonly Dictionary<int, int> conversationDay = new();

    public void FromConversation(Follower follower, string effect)
    {
        if (effect != "up" && effect != "down") return;
        int id = follower.Brain.Info.ID;
        int day = DataManager.Instance.CurrentDayIndex;
        if (conversationDay.TryGetValue(id, out int previousDay) && previousDay == day)
            return;
        if (Change(follower, effect == "up" ? 1 : -1))
            conversationDay[id] = day;
    }

    public bool Change(Follower follower, int amount)
    {
        if (follower == null || follower.Brain == null) return false;
        FollowerInfo? info = FollowerInfo.GetInfoByID(follower.Brain.Info.ID, false);
        if (info == null) return false;
        float before = info.Faith;
        info.Faith = Math.Max(0f, Math.Min(100f, before + amount));
        Plugin.Instance.LogFaith(follower.Brain.Info.Name, before, info.Faith);
        return info.Faith != before;
    }
}
