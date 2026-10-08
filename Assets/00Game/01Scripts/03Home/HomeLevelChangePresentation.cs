using UnityEngine;

// Visual state corresponding to native PendingLevelChangeAnim/LastLevelHardTag.
// This journal never grants rewards or advances gameplay progress.
public static class HomeLevelChangePresentation
{
    private const string PreviousKey = "offline_HomePreviousLevel";
    private const string TargetKey = "offline_HomeTargetLevel";
    private const string HardKey = "offline_HomePreviousHardLevel";
    public static int PreviousLevel => PlayerPrefs.GetInt(PreviousKey, 0);
    public static int TargetLevel => PlayerPrefs.GetInt(TargetKey, 0);
    public static HardLevel PreviousHardLevel => (HardLevel)PlayerPrefs.GetInt(HardKey, 0);
    public static bool HasPending => PreviousLevel > 0 && TargetLevel > PreviousLevel &&
        TargetLevel == GameProgressStore.NextPlayableLevel && !GameProgressStore.IsCampaignComplete;

    public static void Record(int previous, int target, HardLevel hardLevel)
    {
        Clear();
        if (previous < 1 || target <= previous) return;
        PlayerPrefs.SetInt(PreviousKey, previous);
        PlayerPrefs.SetInt(TargetKey, target);
        PlayerPrefs.SetInt(HardKey, (int)hardLevel);
    }

    public static void Consume(int target)
    {
        if (TargetLevel != target) return;
        Clear();
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(PreviousKey);
        PlayerPrefs.DeleteKey(TargetKey);
        PlayerPrefs.DeleteKey(HardKey);
    }
}
