using UnityEngine;
public static class GameProgressStore
{
    private const string UnlockedKey = "offline_HighestUnlockedLevel";
    private const string CompletedKey = "offline_HighestCompletedLevel";
    public static int HighestUnlockedLevel => Mathf.Max(1, Mathf.Max(PlayerPrefs.GetInt(UnlockedKey, 1), LegacyLevel()));
    public static int HighestCompletedLevel => Mathf.Max(0, PlayerPrefs.HasKey(CompletedKey)
        ? PlayerPrefs.GetInt(CompletedKey) : HighestUnlockedLevel - 1);
    public static bool IsLevelCompleted(int level) => level > 0 && level <= HighestCompletedLevel;
    public static int NextPlayableLevel => Mathf.Max(HighestUnlockedLevel, HighestCompletedLevel == int.MaxValue ? int.MaxValue : HighestCompletedLevel + 1);
    public static bool IsCampaignComplete
    {
        get
        {
            var catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
            int last = catalog == null ? 0 : catalog.GetHighestLevelAtOrBelow(int.MaxValue);
            return last > 0 && HighestCompletedLevel == last && HighestUnlockedLevel <= last;
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
    public static void RecordWin(int level, bool hasNextLevel)
    {
        if (level < 1) return;
        int next = hasNextLevel && level < int.MaxValue ? level + 1 : level;
        int unlocked = Mathf.Max(HighestUnlockedLevel, next);
        PlayerPrefs.SetInt(CompletedKey, Mathf.Max(HighestCompletedLevel, level));
        PlayerPrefs.SetInt(UnlockedKey, unlocked);
        PlayerPrefs.SetInt("offline_NormalPuzzleIndex", unlocked - 1);
        PlayerPrefs.SetInt("offline_CurrentPuzzleIndex", unlocked - 1);
        PlayerPrefs.Save();
    }
    public static void DeleteProgress()
    {
        PlayerPrefs.DeleteKey(UnlockedKey);
        PlayerPrefs.DeleteKey(CompletedKey);
        PlayerPrefs.DeleteKey("offline_NormalPuzzleIndex");
        PlayerPrefs.DeleteKey("offline_CurrentPuzzleIndex");
        PlayerPrefs.Save();
    }
}
