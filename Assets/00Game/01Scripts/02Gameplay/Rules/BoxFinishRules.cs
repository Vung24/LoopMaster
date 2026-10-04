using System;

public static class BoxFinishRules
{
    public const float FinishDelay = 0.5f;
    public static bool ShouldShowCombo(float interval, int finished) => finished > 1 && interval <= GameData.BoxComboTime;
    public static bool UseEmoji(float interval, int finished) => interval <= GameData.BoxEmojiComboTime && finished >= 3;
    public static int GetEmojiVariant(int combo) => Math.Max(0, combo) % 3 + 1;
    public static int GetSpriteIndex(int combo, int count) => count <= 0 ? -1 : Math.Max(0, Math.Min(combo - 1, count - 1));
    public static void UpdateCombo(GameData data, float interval)
    {
        if (data == null) return;
        if (interval <= GameData.BoxEmojiComboTime) data.BoxEmojiCombo++;
        else data.BoxCombo = interval <= GameData.BoxComboTime ? data.BoxCombo + 1 : 0;
    }
}
