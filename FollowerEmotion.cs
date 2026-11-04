using System;
using System.Collections.Generic;
using Spine;
using UnityEngine;

namespace FollowerTalk;

internal static class FollowerEmotion
{
    private const int DialogueEmotionTrack = 2;
    private const int DialogueMouthTrack = 3;
    private const string DayMoodPrefix = "FollowerTalk/dayMood/";
    private static readonly Dictionary<int, (Follower Follower, string Emotion)> DayMoods = new();
    private static readonly Dictionary<int, (Follower Follower, string Animation, bool PreviousOverride)>
        DialogueEmotions = new();
    private static string currentSave = "";
    private static float nextScan;
    private static float nextRefresh;
    private static bool mouthTimelinesLogged;

    public static void RememberDayMood(Follower follower, string emotion, FollowerMemory memory)
    {
        if (follower == null || follower.Brain == null ||
            (emotion != "angry" && emotion != "sad" && emotion != "worried" &&
             emotion != "amused")) return;
        memory.SetDayMood(follower, emotion);
        DayMoods[follower.Brain.Info.ID] = (follower, emotion);
    }

    public static void UpdateDayMoods(FollowerMemory memory)
    {
        if (DataManager.Instance == null) return;
        string save = DataManager.Instance.SaveUniqueID ?? "";
        if (save != currentSave)
        {
            DayMoods.Clear();
            currentSave = save;
            nextScan = 0f;
        }
        float now = Time.unscaledTime;
        if (now >= nextScan)
        {
            nextScan = now + 10f;
            foreach (Follower follower in UnityEngine.Object.FindObjectsOfType<Follower>())
            {
                if (follower == null || follower.Brain == null) continue;
                string mood = memory.DayMood(follower);
                if (mood.Length > 0)
                    DayMoods[follower.Brain.Info.ID] = (follower, mood);
            }
        }
        if (now < nextRefresh) return;
        nextRefresh = now + .5f;
        foreach (int id in new List<int>(DayMoods.Keys))
        {
            (Follower follower, string emotion) = DayMoods[id];
            if (follower == null || follower.Brain == null)
            {
                DayMoods.Remove(id);
                continue;
            }
            if (memory.DayMood(follower) != emotion)
            {
                ClearDayMood(follower);
                DayMoods.Remove(id);
                continue;
            }
            if (NativeDialogue.IsTalkingTo(follower) || HasProtectedTask(follower))
            {
                ClearDayMood(follower);
                continue;
            }
            ShowDayMood(follower, emotion);
        }
    }

    private static bool HasProtectedTask(Follower follower)
    {
        FollowerTaskType task = follower.Brain.CurrentTaskType;
        return task == FollowerTaskType.ManualControl ||
            task == FollowerTaskType.GetPlayerAttention ||
            task == FollowerTaskType.Sleep || task == FollowerTaskType.SleepBedRest ||
            task == FollowerTaskType.EatMeal || task == FollowerTaskType.EatStoredFood ||
            task == FollowerTaskType.Drinking || task == FollowerTaskType.AssistRitual;
    }

    private static void ShowDayMood(Follower follower, string emotion)
    {
        try
        {
            var state = follower.Spine?.AnimationState;
            if (state == null) return;
            var current = state.GetCurrent(DialogueEmotionTrack);
            if (current != null) return;
            string face = FaceFor(emotion);
            Animation? animation = state.Data.SkeletonData.FindAnimation(face);
            if (animation == null) return;
            state.SetAnimation(DialogueEmotionTrack,
                new Animation(DayMoodPrefix + emotion, animation.Timelines, animation.Duration), true);
        }
        catch (Exception error) { Plugin.Instance.LogActionError("day mood", error); }
    }

    private static void ClearDayMood(Follower follower)
    {
        try
        {
            var state = follower.Spine?.AnimationState;
            if (state?.GetCurrent(DialogueEmotionTrack)?.Animation?.Name
                .StartsWith(DayMoodPrefix, StringComparison.Ordinal) == true)
                state.ClearTrack(DialogueEmotionTrack);
        }
        catch (Exception error) { Plugin.Instance.LogActionError("clear day mood", error); }
    }

    public static bool StartSpeakingMouth(Follower? follower)
    {
        if (follower == null || follower.Brain == null || follower.Spine == null)
            return false;
        try
        {
            var state = follower.Spine.AnimationState;
            if (state == null) return false;
            var data = state.Data.SkeletonData;
            Animation? talk = data.FindAnimation("Conversations/talk-nice1");
            if (talk == null) return false;
            var mouthTimelines = new ExposedList<Timeline>();
            foreach (Timeline timeline in talk.Timelines)
            {
                string name = timeline switch
                {
                    ISlotTimeline slot when slot.SlotIndex >= 0 &&
                        slot.SlotIndex < data.Slots.Count => data.Slots.Items[slot.SlotIndex].Name,
                    IBoneTimeline bone when bone.BoneIndex >= 0 &&
                        bone.BoneIndex < data.Bones.Count => data.Bones.Items[bone.BoneIndex].Name,
                    _ => ""
                };
                if (name.IndexOf("mouth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("jaw", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("tongue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("lip", StringComparison.OrdinalIgnoreCase) >= 0)
                    mouthTimelines.Add(timeline);
            }
            if (!mouthTimelinesLogged)
            {
                Plugin.Instance.LogMouthSetup(talk.Timelines.Count, mouthTimelines.Count);
                mouthTimelinesLogged = true;
            }
            if (mouthTimelines.Count == 0) return false;
            var speaking = state.SetAnimation(DialogueMouthTrack,
                new Animation("FollowerTalk/mouth", mouthTimelines, talk.Duration), false);
            speaking.MixDuration = 0f;
            speaking.Alpha = 1f;
            speaking.TimeScale = 0f;
            if (talk.Duration > 0f)
                speaking.TrackTime = Mathf.Min(talk.Duration / 4f, .08f);
            return true;
        }
        catch (Exception error)
        {
            Plugin.Instance.LogActionError("mouth animation", error);
            return false;
        }
    }

    public static void AdvanceSpeakingMouth(Follower? follower)
    {
        try
        {
            var speaking = follower?.Spine?.AnimationState?.GetCurrent(DialogueMouthTrack);
            if (speaking?.Animation?.Name != "FollowerTalk/mouth") return;
            float duration = speaking.Animation.Duration;
            if (duration <= 0f) return;
            float step = Mathf.Clamp(duration / 4f, .02f, .08f);
            speaking.TrackTime = (speaking.TrackTime + step) % duration;
        }
        catch (Exception error) { Plugin.Instance.LogActionError("mouth animation", error); }
    }

    public static void StopSpeakingMouth(Follower? follower)
    {
        try
        {
            var state = follower?.Spine?.AnimationState;
            var speaking = state?.GetCurrent(DialogueMouthTrack);
            if (speaking?.Animation?.Name != "FollowerTalk/mouth") return;
            var timelines = speaking.Animation.Timelines;
            var skeleton = follower!.Spine.Skeleton;
            state!.ClearTrack(DialogueMouthTrack);
            foreach (Timeline timeline in timelines)
            {
                if (timeline is ISlotTimeline slot && slot.SlotIndex >= 0 &&
                    slot.SlotIndex < skeleton.Slots.Count)
                    skeleton.Slots.Items[slot.SlotIndex].SetToSetupPose();
                else if (timeline is IBoneTimeline bone && bone.BoneIndex >= 0 &&
                    bone.BoneIndex < skeleton.Bones.Count)
                    skeleton.Bones.Items[bone.BoneIndex].SetToSetupPose();
            }
        }
        catch (Exception error) { Plugin.Instance.LogActionError("reset mouth pose", error); }
    }

    public static void Apply(Follower? follower, string emotion)
    {
        if (follower == null || follower.Brain == null) return;
        if (emotion == "neutral")
        {
            Plugin.Instance.LogEmotion(emotion, "game default");
            Restore(follower);
            return;
        }
        string face = FaceFor(emotion);
        try
        {
            var spine = follower.Spine;
            if (spine == null || spine.AnimationState == null ||
                spine.AnimationState.Data.SkeletonData.FindAnimation(face) == null)
            {
                Plugin.Instance.LogActionError("emotion", new InvalidOperationException(
                    "Follower animation unavailable: " + face));
                return;
            }
            int id = follower.Brain.Info.ID;
            if (!DialogueEmotions.TryGetValue(id, out var previous) || previous.Follower != follower)
                DialogueEmotions[id] = (follower, face, follower.OverridingEmotions);
            else DialogueEmotions[id] = (follower, face, previous.PreviousOverride);
            follower.OverridingEmotions = true;
            spine.AnimationState.SetAnimation(DialogueEmotionTrack, face, true);
            Plugin.Instance.LogEmotion(emotion, face);
        }
        catch (Exception error)
        {
            if (follower.Brain != null &&
                DialogueEmotions.TryGetValue(follower.Brain.Info.ID, out var owned))
            {
                DialogueEmotions.Remove(follower.Brain.Info.ID);
                follower.OverridingEmotions = owned.PreviousOverride;
            }
            Plugin.Instance.LogActionError("emotion", error);
        }
    }

    private static string FaceFor(string emotion) => emotion switch
        {
            "agree" or "amused" => "Emotions/emotion-happy",
            "angry" => "Emotions/emotion-angry",
            "sad" or "uncertain" => "Emotions/emotion-unhappy",
            "worried" => "Emotions/emotion-scared",
            _ => "Emotions/emotion-normal"
        };

    public static void Restore(Follower? follower)
    {
        if (follower == null || follower.Brain == null) return;
        try
        {
            StopSpeakingMouth(follower);
            int id = follower.Brain.Info.ID;
            if (!DialogueEmotions.TryGetValue(id, out var owned) || owned.Follower != follower)
                return;
            DialogueEmotions.Remove(id);
            var state = follower.Spine?.AnimationState;
            if (state?.GetCurrent(DialogueEmotionTrack)?.Animation?.Name != owned.Animation)
                return;
            state.ClearTrack(DialogueEmotionTrack);
            follower.OverridingEmotions = owned.PreviousOverride;
            if (!owned.PreviousOverride) follower.SetEmotionAnimation();
        }
        catch (Exception error)
        {
            Plugin.Instance.LogActionError("restore emotion", error);
        }
    }
}
