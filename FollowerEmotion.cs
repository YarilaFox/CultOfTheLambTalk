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
    private static string currentSave = "";
    private static float nextScan;
    private static float nextRefresh;
    private static bool missingMouthLogged;

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
            Animation? talk = data.FindAnimation("talk");
            if (talk == null) return false;
            var mouthTimelines = new ExposedList<Timeline>();
            for (int i = 0; i < talk.Timelines.Count; i++)
            {
                Timeline timeline = talk.Timelines.Items[i];
                if (IsMouthTimeline(timeline, data)) mouthTimelines.Add(timeline);
            }
            if (mouthTimelines.Count == 0)
            {
                if (!missingMouthLogged)
                {
                    missingMouthLogged = true;
                    Plugin.Instance.LogActionError("mouth animation", new InvalidOperationException(
                        "No separate mouth timelines in follower talk animation; using game talk animation."));
                }
                return false;
            }
            state.SetAnimation(DialogueMouthTrack,
                new Animation("FollowerTalk/mouth", mouthTimelines, talk.Duration), true);
            return true;
        }
        catch (Exception error)
        {
            Plugin.Instance.LogActionError("mouth animation", error);
            return false;
        }
    }

    private static bool IsMouthTimeline(Timeline timeline, SkeletonData data)
    {
        int slot = timeline switch
        {
            AttachmentTimeline attachment => attachment.SlotIndex,
            DeformTimeline deform => deform.SlotIndex,
            ColorTimeline color => color.SlotIndex,
            TwoColorTimeline twoColor => twoColor.SlotIndex,
            _ => -1
        };
        if (slot >= 0 && slot < data.Slots.Count && IsMouthName(data.Slots.Items[slot].Name))
            return true;
        int bone = timeline switch
        {
            RotateTimeline rotate => rotate.BoneIndex,
            TranslateTimeline translate => translate.BoneIndex,
            _ => -1
        };
        return bone >= 0 && bone < data.Bones.Count &&
            IsMouthName(data.Bones.Items[bone].Name);
    }

    private static bool IsMouthName(string name) =>
        name.IndexOf("mouth", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("jaw", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("lip", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("tongue", StringComparison.OrdinalIgnoreCase) >= 0;

    public static void StopSpeakingMouth(Follower? follower)
    {
        if (follower != null && follower.Spine != null && follower.Spine.AnimationState != null)
            follower.Spine.AnimationState.ClearTrack(DialogueMouthTrack);
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
            follower.OverridingEmotions = true;
            spine.AnimationState.SetAnimation(DialogueEmotionTrack, face, true);
            Plugin.Instance.LogEmotion(emotion, face);
        }
        catch (Exception error)
        {
            follower.OverridingEmotions = false;
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
            if (follower.Spine != null && follower.Spine.AnimationState != null)
                follower.Spine.AnimationState.ClearTrack(DialogueEmotionTrack);
            follower.OverridingEmotions = false;
            follower.SetEmotionAnimation();
        }
        catch (Exception error)
        {
            Plugin.Instance.LogActionError("restore emotion", error);
        }
    }
}
