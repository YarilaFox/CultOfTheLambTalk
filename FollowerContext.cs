using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FollowerTalk;

internal readonly struct FollowerActivity
{
    public FollowerActivity(FollowerTaskType task, int structureId)
    {
        Task = task;
        StructureId = structureId;
    }

    public FollowerTaskType Task { get; }
    public int StructureId { get; }
}

internal static class FollowerContext
{
    private static readonly FieldInfo? PreviousTask = typeof(interaction_FollowerInteraction)
        .GetField("previousTaskType", BindingFlags.Instance | BindingFlags.NonPublic);

    public static FollowerActivity Snapshot(Follower follower,
        interaction_FollowerInteraction? interaction)
    {
        if (follower == null || follower.Brain == null)
            return new FollowerActivity(FollowerTaskType.None, 0);
        FollowerTaskType task = follower.Brain.CurrentTaskType;
        int structureId = follower.Brain.CurrentTaskUsingStructureID;
        if (interaction != null)
        {
            try
            {
                if (PreviousTask?.GetValue(interaction) is FollowerTaskType previous &&
                    MeaningfulTask(previous)) task = previous;
            }
            catch (System.Exception) { }
        }
        return new FollowerActivity(task, structureId);
    }

    public static string Capture(Follower follower, string language,
        FollowerActivity? observed = null)
    {
        if (follower == null || follower.Brain == null)
            return TalkLanguage.Pick(language, "Состояние недоступно.", "State unavailable.");

        FollowerBrain brain = follower.Brain;
        var details = new List<string>();
        FollowerInfo? info = FollowerInfo.GetInfoByID(brain.Info.ID, false);
        if (info != null)
        {
            string belief = info.Dissent >= FollowerInfo.DISSENTER_THRESHOLD
                ? TalkLanguage.Pick(language, "отступник", "dissenter")
                : info.Faith <= FollowerInfo.CRITICAL_FAITH_THRESHOLD
                    ? TalkLanguage.Pick(language, "вера почти исчезла", "faith nearly gone")
                    : info.Faith <= FollowerInfo.LOW_FAITH_THRESHOLD
                        ? TalkLanguage.Pick(language, "сомневается", "doubtful")
                        : TalkLanguage.Pick(language, "вера не низкая", "faith not low");
            details.Add(TalkLanguage.Pick(language, "личная вера ", "personal faith ") +
                Math.Round(info.Faith) + "/100 (" + belief + ")");
            details.Add(TalkLanguage.Pick(language, "недовольство ", "dissent ") +
                Math.Round(info.Dissent) + "/100");
        }
        else details.Add(TalkLanguage.Pick(language, "данные о вере и недовольстве недоступны",
            "faith and dissent data unavailable"));

        if (brain.Stats != null)
        {
            details.Add(TalkLanguage.Pick(language, "уровень игровой верности ", "game loyalty level ") +
                brain.Info.XPLevel + TalkLanguage.Pick(language,
                    ", прогресс верности ", ", loyalty progress ") +
                Math.Round(brain.Stats.Adoration) + "/" +
                Math.Round(brain.Stats.MAX_ADORATION));
            details.Add(TalkLanguage.Pick(language, "настроение ", "mood ") +
                Math.Round(brain.Stats.Happiness) + TalkLanguage.Pick(language, ", сытость ", ", satiety ") +
                Math.Round(brain.Stats.Satiation) + TalkLanguage.Pick(language, ", отдых ", ", rest ") +
                Math.Round(brain.Stats.Rest) + TalkLanguage.Pick(language, ", болезнь ", ", illness ") +
                Math.Round(brain.Stats.Illness));
            var thoughts = new List<string>();
            if (brain.Stats.Thoughts != null)
                foreach (ThoughtData thought in brain.Stats.Thoughts
                    .Where(item => item != null)
                    .OrderByDescending(item => Math.Abs(item.Modifier))
                    .Take(6))
                {
                    string name = FollowerThoughts.GetLocalisedName(thought.ThoughtType, brain.Info.ID);
                    if (!string.IsNullOrWhiteSpace(name))
                        thoughts.Add(thought.ThoughtType + ": " + name + " (" +
                            TalkLanguage.Pick(language, "влияние ", "effect ") +
                            thought.Modifier.ToString("+0.##;-0.##;0") + ")");
                }
            details.Add(TalkLanguage.Pick(language, "активные мысли: ", "active thoughts: ") +
                (thoughts.Count > 0 ? string.Join("; ", thoughts) :
                    TalkLanguage.Pick(language, "не обнаружены", "none observed")));
        }

        if (brain.Info.Traits != null && brain.Info.Traits.Count > 0)
            details.Add(TalkLanguage.Pick(language, "черты: ", "traits: ") +
                string.Join(", ", brain.Info.Traits.Take(8)));
        if (brain.Info.IsDrunk)
            details.Add(TalkLanguage.Pick(language, "сейчас пьян", "currently drunk"));
        if (brain.FollowingPlayer)
            details.Add(TalkLanguage.Pick(language,
                "сейчас следуешь за Ягнёнком по его просьбе",
                "currently following the Lamb at their request"));
        FollowerTaskType current = observed?.Task ?? brain.CurrentTaskType;
        if (!MeaningfulTask(current))
            current = brain.CurrentTaskType;
        if (!MeaningfulTask(current))
            current = brain.SavedFollowerTaskType;
        details.Add(TalkLanguage.Pick(language, "чем ты занимался непосредственно перед разговором: ",
            "what you were doing immediately before the conversation: ") +
            (MeaningfulTask(current) ? FollowerActions.TaskName(current, language,
                observed?.StructureId ?? 0) :
                TalkLanguage.Pick(language, "точно не удалось установить",
                    "could not be determined reliably")));
        var recent = (brain._taskMemory ?? new List<TaskAndTime>())
            .OrderByDescending(item => item.Time)
            .Select(item => item.Task)
            .Where(MeaningfulTask)
            .Where(task => task != current)
            .Distinct()
            .Take(3)
            .ToList();
        if (recent.Count > 0)
            details.Add(TalkLanguage.Pick(language,
                "раньше тебе поручали: ",
                "earlier tasks you were assigned: ") +
                string.Join(", ", recent.Select(task => FollowerActions.TaskName(task, language))));
        return string.Join(". ", details) + ".";
    }

    private static bool MeaningfulTask(FollowerTaskType task) => task != FollowerTaskType.None &&
        task != FollowerTaskType.Idle && task != FollowerTaskType.ManualControl &&
        task != FollowerTaskType.GetPlayerAttention && task != FollowerTaskType.FollowPlayer &&
        task != FollowerTaskType.GreetPlayer && task != FollowerTaskType.AwaitInstructions &&
        task != FollowerTaskType.ChangeLocation;
}
