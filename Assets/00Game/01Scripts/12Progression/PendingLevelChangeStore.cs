// Keeps pending level presentation data with progression so saving progress does not depend on Home UI.
public static class PendingLevelChangeStore
{
    private const string PreviousKey = GamePrefKeys.HomePreviousLevel;
    private const string TargetKey = GamePrefKeys.HomeTargetLevel;
    private const string HardKey = GamePrefKeys.HomeHardLevel;

    public static int PreviousLevel => GamePrefs.GetInt(PreviousKey, 0);
    public static int TargetLevel => GamePrefs.GetInt(TargetKey, 0);
    public static HardLevel PreviousHardLevel => (HardLevel)GamePrefs.GetInt(HardKey, 0);
    public static bool HasPending => PreviousLevel > 0 && TargetLevel > PreviousLevel &&
        TargetLevel == GameProgressStore.NextPlayableLevel && !GameProgressStore.IsCampaignComplete;

    public static void Record(int previous, int target, HardLevel hardLevel)
    {
        Clear();
        if (previous < 1 || target <= previous) return;
        GamePrefs.SetInt(PreviousKey, previous);
        GamePrefs.SetInt(TargetKey, target);
        GamePrefs.SetInt(HardKey, (int)hardLevel);
    }

    public static void Consume(int target)
    {
        if (TargetLevel != target) return;
        Clear();
        GamePrefs.Save();
    }

    public static void Clear()
    {
        GamePrefs.DeleteKey(PreviousKey);
        GamePrefs.DeleteKey(TargetKey);
        GamePrefs.DeleteKey(HardKey);
    }
}
