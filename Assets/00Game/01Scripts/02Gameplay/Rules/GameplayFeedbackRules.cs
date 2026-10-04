using System;

// Native combo window/pitch come from GameController.UpdateCombo and Box.PlayPopInAudio.
// Conveyor spacing and offline audio fallbacks retain the recovery's gameplay settings.
public static class GameplayFeedbackRules
{
    public const float SliceComboWindow = 0.6f;
    public const float SliceJumpDuration = 0.4f;
    public const float SliceJumpHeight = 2.2f;
    public static float GetTransferTimeScale(bool speedUp) => speedUp ? 2f : 1f;
    public static float GetConveyorInterval(float normal, bool speedUp, float spacingScale = 1f) =>
		Math.Max(0.02f, normal) * Math.Max(0.4f, Math.Min(1f, spacingScale)) / (speedUp ? 2f : 1f);

    public static void RegisterArrival(ref int count, ref float remaining)
    {
        count++;
        remaining = SliceComboWindow;
    }

    public static void AdvanceCombo(ref int count, ref float remaining, float delta)
    {
        if (count <= 0 || remaining <= 0f || delta <= 0f) return;
        remaining = Math.Max(0f, remaining - delta);
        if (remaining <= 0f) count = 0;
    }

    public static float GetPopInPitch(int combo) => Math.Min(3f, 0.9f + Math.Max(0, combo) * 0.015f);
    public static bool IsSpeedUp(bool automatic, bool pressed) => automatic || pressed;
    public static bool ShouldPlaySpeedTransition(int level, bool previous, bool next) => level > 2 && previous != next;
    public static T SelectMatch<T>(bool groupB, T match, T match2) where T : class =>
        groupB ? match2 ?? match : match ?? match2;
}
