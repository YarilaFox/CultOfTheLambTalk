using System;
using System.Collections.Generic;
using System.Linq;

namespace FollowerTalk;

internal static class FollowerContext
{
    public static string Capture(Follower follower)
    {
        if (follower == null || follower.Brain == null)
            return "Состояние недоступно.";

        FollowerBrain brain = follower.Brain;
        var details = new List<string>();
        FollowerInfo? info = FollowerInfo.GetInfoByID(brain.Info.ID, false);
        if (info != null)
        {
            string belief = info.Dissent >= FollowerInfo.DISSENTER_THRESHOLD
                ? "отступник"
                : info.Faith <= FollowerInfo.CRITICAL_FAITH_THRESHOLD
                    ? "вера почти исчезла"
                    : info.Faith <= FollowerInfo.LOW_FAITH_THRESHOLD
                        ? "сомневается"
                        : "вера не низкая";
            details.Add("личная вера " + Math.Round(info.Faith) + "/100 (" + belief + ")");
            details.Add("недовольство " + Math.Round(info.Dissent) + "/100");
        }
        else details.Add("данные о вере и недовольстве недоступны");

        if (brain.Stats != null)
        {
            details.Add("уровень игровой верности " + brain.Info.XPLevel +
                ", прогресс верности " + Math.Round(brain.Stats.Adoration) +
                "/" + Math.Round(brain.Stats.MAX_ADORATION));
            details.Add("настроение " + Math.Round(brain.Stats.Happiness) +
                ", сытость " + Math.Round(brain.Stats.Satiation) +
                ", отдых " + Math.Round(brain.Stats.Rest) +
                ", болезнь " + Math.Round(brain.Stats.Illness));
            var thoughts = new List<string>();
            if (brain.Stats.Thoughts != null)
                foreach (ThoughtData thought in brain.Stats.Thoughts
                    .Where(item => item != null)
                    .OrderByDescending(item => Math.Abs(item.Modifier))
                    .Take(6))
                {
                    string name = FollowerThoughts.GetLocalisedName(thought.ThoughtType, brain.Info.ID);
                    if (!string.IsNullOrWhiteSpace(name))
                        thoughts.Add(name + " (влияние " + thought.Modifier.ToString("+0.##;-0.##;0") + ")");
                }
            details.Add("активные мысли: " + (thoughts.Count > 0
                ? string.Join("; ", thoughts) : "не обнаружены"));
        }

        if (brain.Info.Traits != null && brain.Info.Traits.Count > 0)
            details.Add("черты: " + string.Join(", ", brain.Info.Traits.Take(8)));
        if (brain.Info.IsDrunk) details.Add("сейчас пьян");
        details.Add("текущее занятие: " + brain.CurrentTaskType);
        return string.Join(". ", details) + ".";
    }
}
