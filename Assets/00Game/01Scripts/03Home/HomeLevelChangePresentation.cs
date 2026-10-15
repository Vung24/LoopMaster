public static class HomeLevelChangePresentation
{
    public static int PreviousLevel => PendingLevelChangeStore.PreviousLevel;
    public static int TargetLevel => PendingLevelChangeStore.TargetLevel;
    public static HardLevel PreviousHardLevel => PendingLevelChangeStore.PreviousHardLevel;
    public static bool HasPending => PendingLevelChangeStore.HasPending;

    public static void Record(int previous, int target, HardLevel hardLevel) =>
        PendingLevelChangeStore.Record(previous, target, hardLevel);

    public static void Consume(int target) => PendingLevelChangeStore.Consume(target);

    public static void Clear() => PendingLevelChangeStore.Clear();
}
