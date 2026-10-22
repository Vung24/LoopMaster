#if UNITY_EDITOR
/// <summary>Identifies teaching and recovery beats in the authored campaign.</summary>
public static class CampaignLearningCurve
{
    /// <summary>Returns whether this level introduces a mechanic on a small board.</summary>
    public static bool IsIntroduction(int level)
    {
        return level == 6 || level == 15 || level == 21 || level == 23 || level == 29 ||
            level == 37 || level == 41 || level == 49 || level == 53 || level == 56;
    }

    /// <summary>Returns whether this level follows an authored Hard or Expert level.</summary>
    public static bool IsRecovery(int level)
    {
        return level == 11 || level == 21 || level == 26 || level == 31 || level == 36 ||
            level == 41 || level == 46 || level == 56 || level == 61 || level == 63 ||
            level == 65 || level == 67 || level == 69;
    }

    /// <summary>Returns whether the reference solution should stay short and forgiving.</summary>
    public static bool IsGentle(int level)
    {
        if (IsIntroduction(level) || IsRecovery(level)) return true;
        return level == 16 || level == 17 || level == 22 || level == 24 || level == 38 ||
            level == 42 || level == 50 || level == 54 || level == 57;
    }
}
#endif
