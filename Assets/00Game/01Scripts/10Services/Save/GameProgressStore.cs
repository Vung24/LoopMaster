using UnityEngine;
public static class GameProgressStore
{
    private const string UnlockedKey = GamePrefKeys.UnlockedLevel;
    private const string CompletedKey = GamePrefKeys.CompletedLevel;
    public static int CurrentPuzzleIndex => Mathf.Max(0, GamePrefs.GetInt(GamePrefKeys.CurrentPuzzle, 0));
    public static int HighestUnlockedLevel => Mathf.Max(1, Mathf.Max(GamePrefs.GetInt(UnlockedKey, 1), LegacyLevel()));
    public static int HighestCompletedLevel => Mathf.Max(0, GamePrefs.HasKey(CompletedKey)
        ? GamePrefs.GetInt(CompletedKey) : HighestUnlockedLevel - 1);
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
        int index = Mathf.Max(0, GamePrefs.GetInt(GamePrefKeys.NormalPuzzle, 0));
        return index == int.MaxValue ? int.MaxValue : index + 1;
    }
    public static void RecordStarted(int level)
    {
        if (level < 1) return;
        GamePrefs.SetInt(GamePrefKeys.CurrentPuzzle, level - 1);
        GamePrefs.Save();
    }
    public static void RecordWin(int level, bool hasNextLevel, HardLevel previousHardLevel = HardLevel.Normal)
    {
        if (level < 1) return;
        bool newlyCompleted = !IsLevelCompleted(level);
        int next = hasNextLevel && level < int.MaxValue ? level + 1 : level;
        int unlocked = Mathf.Max(HighestUnlockedLevel, next);
        GamePrefs.SetInt(CompletedKey, Mathf.Max(HighestCompletedLevel, level));
        GamePrefs.SetInt(UnlockedKey, unlocked);
        GamePrefs.SetInt(GamePrefKeys.NormalPuzzle, unlocked - 1);
        GamePrefs.SetInt(GamePrefKeys.CurrentPuzzle, unlocked - 1);
        if (newlyCompleted) HomeLevelChangePresentation.Record(level, unlocked, previousHardLevel);
        GamePrefs.Save();
    }
    public static void SetProgress(int level)
    {
        if (level < 1) level = 1;
        HomeLevelChangePresentation.Clear();
        GamePrefs.SetInt(UnlockedKey, level);
        GamePrefs.SetInt(CompletedKey, level - 1);
        GamePrefs.SetInt(GamePrefKeys.NormalPuzzle, level - 1);
        GamePrefs.SetInt(GamePrefKeys.CurrentPuzzle, level - 1);
        GamePrefs.Save();
    }
    public static void DeleteProgress()
    {
        HomeLevelChangePresentation.Clear();
        GamePrefs.DeleteKey(UnlockedKey);
        GamePrefs.DeleteKey(CompletedKey);
        GamePrefs.DeleteKey(GamePrefKeys.NormalPuzzle);
        GamePrefs.DeleteKey(GamePrefKeys.CurrentPuzzle);
        GamePrefs.Save();
    }
}
