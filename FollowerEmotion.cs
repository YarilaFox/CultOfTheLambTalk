using System;
using System.Collections.Generic;
using Spine;
using UnityEngine;

namespace FollowerTalk;

internal static class FollowerEmotion
{
    private const int NativeFaceTrack = 0;
    private const int DialogueMouthTrack = 3;
    private const string SpeakingMouthAnimation = "FollowerTalk/mouth";
    private const string ClosedMouthAnimation = "FollowerTalk/mouth-closed";
    private static readonly Dictionary<int, (Follower Follower, string Emotion)> DayMoods = new();
    private static readonly Dictionary<int, (Follower Follower, string Animation,
        bool PreviousOverride, string PreviousFace)>
        DialogueEmotions = new();
    private static string currentSave = "";
    private static float nextScan;
    private static float nextRefresh;
    private static bool mouthTimelinesLogged;
    private static bool closedMouthLogged;

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
                ClearDayMood(follower, emotion);
                DayMoods.Remove(id);
                continue;
            }
            if (NativeDialogue.IsTalkingTo(follower) || HasProtectedTask(follower))
            {
                ClearDayMood(follower, emotion);
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
            task == FollowerTaskType.Drinking || task == FollowerTaskType.AssistRitual ||
            task == FollowerTaskType.RunFromSomething || task == FollowerTaskType.ChaseFollower;
    }

    private static void ShowDayMood(Follower follower, string emotion)
    {
        try
        {
            if (follower.OverridingEmotions || follower.Brain.Info.CursedState != Thought.None ||
                follower.Brain.Info.HasTrait(FollowerTrait.TraitType.Mutated)) return;
            string current = CurrentFace(follower);
            if (!IsOrdinaryFace(current)) return;
            follower.SetFaceAnimation(FaceFor(emotion), true);
        }
        catch (Exception error) { Plugin.Instance.LogActionError("day mood", error); }
    }

    private static void ClearDayMood(Follower follower, string emotion)
    {
        try
        {
            if (!follower.OverridingEmotions && CurrentFace(follower) == FaceFor(emotion))
                follower.SetEmotionAnimation();
        }
        catch (Exception error) { Plugin.Instance.LogActionError("clear day mood", error); }
    }

    private static bool IsOrdinaryFace(string name) =>
        name == "Emotions/emotion-normal" || name == "Emotions/emotion-happy" ||
        name == "Emotions/emotion-unhappy" || name == "Emotions/emotion-angry" ||
        name == "Emotions/emotion-scared";

    private static string CurrentFace(Follower follower) =>
        follower.Spine?.AnimationState?.GetCurrent(NativeFaceTrack)?.Animation?.Name ?? "";

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
                if (FollowerAnimationParts.IsMouth(TimelineTarget(timeline, data)))
                    mouthTimelines.Add(timeline);
            }
            if (!mouthTimelinesLogged)
            {
                Plugin.Instance.LogMouthSetup(talk.Timelines.Count, mouthTimelines.Count);
                mouthTimelinesLogged = true;
            }
            if (mouthTimelines.Count == 0) return false;
            var speaking = state.SetAnimation(DialogueMouthTrack,
                new Animation(SpeakingMouthAnimation, mouthTimelines, talk.Duration), false);
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
            if (speaking?.Animation?.Name != SpeakingMouthAnimation) return;
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
            if (speaking?.Animation?.Name != SpeakingMouthAnimation) return;
            var timelines = speaking.Animation.Timelines;
            var skeleton = follower!.Spine.Skeleton;
            var closedTimelines = new ExposedList<Timeline>();
            var mouthSlots = new HashSet<int>();
            foreach (Timeline timeline in timelines)
            {
                if (timeline is ISlotTimeline slot && slot.SlotIndex >= 0 &&
                    slot.SlotIndex < skeleton.Slots.Count)
                    mouthSlots.Add(slot.SlotIndex);
                if (timeline is IBoneTimeline bone && bone.BoneIndex >= 0 &&
                    bone.BoneIndex < skeleton.Bones.Count)
                    skeleton.Bones.Items[bone.BoneIndex].SetToSetupPose();
            }
            if (mouthSlots.Count == 0)
                for (int index = 0; index < skeleton.Data.Slots.Count; index++)
                    if (FollowerAnimationParts.IsMouth(skeleton.Data.Slots.Items[index].Name))
                        mouthSlots.Add(index);
            foreach (int slotIndex in mouthSlots)
            {
                var closed = new AttachmentTimeline(1) { SlotIndex = slotIndex };
                closed.SetFrame(0, 0f, skeleton.Data.Slots.Items[slotIndex].AttachmentName);
                closedTimelines.Add(closed);
            }
            if (!closedMouthLogged)
            {
                Plugin.Instance.LogMouthClosed(mouthSlots.Count);
                closedMouthLogged = true;
            }
            if (closedTimelines.Count == 0)
            {
                state!.ClearTrack(DialogueMouthTrack);
                return;
            }
            var closedMouth = state!.SetAnimation(DialogueMouthTrack,
                new Animation(ClosedMouthAnimation, closedTimelines, 1f), true);
            closedMouth.MixDuration = 0f;
            closedMouth.TimeScale = 0f;
        }
        catch (Exception error) { Plugin.Instance.LogActionError("reset mouth pose", error); }
    }

    public static void ReleaseSpeakingMouth(Follower? follower)
    {
        try
        {
            var state = follower?.Spine?.AnimationState;
            string? name = state?.GetCurrent(DialogueMouthTrack)?.Animation?.Name;
            if (name == SpeakingMouthAnimation || name == ClosedMouthAnimation)
                state!.ClearTrack(DialogueMouthTrack);
        }
        catch (Exception error) { Plugin.Instance.LogActionError("release mouth pose", error); }
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
                DialogueEmotions[id] = (follower, face, follower.OverridingEmotions,
                    CurrentFace(follower));
            else DialogueEmotions[id] = (follower, face, previous.PreviousOverride,
                previous.PreviousFace);
            follower.OverridingEmotions = true;
            follower.SetFaceAnimation(face, true);
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

    private static string TimelineTarget(Timeline timeline, SkeletonData data) => timeline switch
    {
        ISlotTimeline slot when slot.SlotIndex >= 0 &&
            slot.SlotIndex < data.Slots.Count => data.Slots.Items[slot.SlotIndex].Name,
        IBoneTimeline bone when bone.BoneIndex >= 0 &&
            bone.BoneIndex < data.Bones.Count => data.Bones.Items[bone.BoneIndex].Name,
        _ => ""
    };

    public static void Restore(Follower? follower)
    {
        if (follower == null || follower.Brain == null) return;
        try
        {
            int id = follower.Brain.Info.ID;
            if (!DialogueEmotions.TryGetValue(id, out var owned) || owned.Follower != follower)
                return;
            DialogueEmotions.Remove(id);
            follower.OverridingEmotions = owned.PreviousOverride;
            if (owned.PreviousOverride)
            {
                if (owned.PreviousFace.Length > 0)
                    follower.SetFaceAnimation(owned.PreviousFace, true);
            }
            else follower.SetEmotionAnimation();
        }
        catch (Exception error)
        {
            Plugin.Instance.LogActionError("restore emotion", error);
        }
    }
}
