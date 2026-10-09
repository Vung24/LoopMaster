using UnityEngine;
public static class GameProgressStore
{
    private const string UnlockedKey = "offline_HighestUnlockedLevel";
    private const string CompletedKey = "offline_HighestCompletedLevel";
    public static int CurrentPuzzleIndex => Mathf.Max(0, PlayerPrefs.GetInt("offline_CurrentPuzzleIndex", 0));
    public static int HighestUnlockedLevel => Mathf.Max(1, Mathf.Max(PlayerPrefs.GetInt(UnlockedKey, 1), LegacyLevel()));
    public static int HighestCompletedLevel => Mathf.Max(0, PlayerPrefs.HasKey(CompletedKey)
        ? PlayerPrefs.GetInt(CompletedKey) : HighestUnlockedLevel - 1);
    public static bool IsLevelCompleted(int level) => level > 0 && level <= HighestCompletedLevel;
    public static int NextPlayableLevel
    {
        get
        {
            int next = Mathf.Max(HighestUnlockedLevel, HighestCompletedLevel == int.MaxValue ? int.MaxValue : HighestCompletedLevel + 1);
            var catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
            int last = catalog == null ? 0 : catalog.GetHighestLevelAtOrBelow(int.MaxValue);
            return last > 0 && next > last ? last : next;
        }
    }
    public static bool IsCampaignComplete
    {
        get
        {
            var catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
            int last = catalog == null ? 0 : catalog.GetHighestLevelAtOrBelow(int.MaxValue);
            return last > 0 && HighestCompletedLevel >= last;
        }
    }
    private static int LegacyLevel()
    {
        int index = Mathf.Max(0, PlayerPrefs.GetInt("offline_NormalPuzzleIndex", 0));
        return index == int.MaxValue ? int.MaxValue : index + 1;
    }
    public static void RecordStarted(int level)
    {
        if (level < 1) return;
        PlayerPrefs.SetInt("offline_CurrentPuzzleIndex", level - 1);
        PlayerPrefs.Save();
    }
    public static void RecordWin(int level, bool hasNextLevel, HardLevel previousHardLevel = HardLevel.Normal)
    {
        if (level < 1) return;
        bool newlyCompleted = !IsLevelCompleted(level);
        int next = hasNextLevel && level < int.MaxValue ? level + 1 : level;
        int unlocked = Mathf.Max(HighestUnlockedLevel, next);
        PlayerPrefs.SetInt(CompletedKey, Mathf.Max(HighestCompletedLevel, level));
        PlayerPrefs.SetInt(UnlockedKey, unlocked);
        PlayerPrefs.SetInt("offline_NormalPuzzleIndex", unlocked - 1);
        PlayerPrefs.SetInt("offline_CurrentPuzzleIndex", unlocked - 1);
        if (newlyCompleted) HomeLevelChangePresentation.Record(level, unlocked, previousHardLevel);
        PlayerPrefs.Save();
    }
    public static void SetProgress(int level)
    {
        if (level < 1) level = 1;
        HomeLevelChangePresentation.Clear();
        PlayerPrefs.SetInt(UnlockedKey, level);
        PlayerPrefs.SetInt(CompletedKey, level - 1);
        PlayerPrefs.SetInt("offline_NormalPuzzleIndex", level - 1);
        PlayerPrefs.SetInt("offline_CurrentPuzzleIndex", level - 1);
        PlayerPrefs.Save();
    }
    public static void DeleteProgress()
    {
        HomeLevelChangePresentation.Clear();
        PlayerPrefs.DeleteKey(UnlockedKey);
        PlayerPrefs.DeleteKey(CompletedKey);
        PlayerPrefs.DeleteKey("offline_NormalPuzzleIndex");
        PlayerPrefs.DeleteKey("offline_CurrentPuzzleIndex");
        PlayerPrefs.Save();
    }
}
