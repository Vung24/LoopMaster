using System;

internal static class Program
{
    private static int _checks;

    private static void Main()
    {
        VerifyMilestones();
        VerifyFreeUse(InventoryKeys.TOOL_UNDO, 4);
        VerifyFreeUse(InventoryKeys.TOOL_SHUFFLE, 7);
        VerifyFreeUse(InventoryKeys.TOOL_PATH, 9);
        VerifyFailedAndReentrantActions();
        Console.WriteLine("Booster unlock verification passed: " + _checks + " checks.");
    }

#if UNITY_EDITOR
    public static void Run()
    {
        try
        {
            Main();
            UnityEngine.Debug.Log("Booster unlock verification passed: " + _checks + " checks.");
            UnityEditor.EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            UnityEditor.EditorApplication.Exit(1);
        }
    }
#endif

    private static InventoryManager CreateInventory()
    {
#if UNITY_EDITOR
        return new UnityEngine.GameObject("TestInventory").AddComponent<InventoryManager>();
#else
        return new InventoryManager();
#endif
    }

    private static void VerifyMilestones()
    {
        GamePrefs.Clear();
        Expect(BoosterUnlockProgress.GetPendingUnlockKey(3) == null, "no early unlock");
        Expect(BoosterUnlockProgress.GetPendingUnlockKey(4) == InventoryKeys.TOOL_UNDO, "Undo level 4");
        Expect(BoosterUnlockProgress.GetPendingUnlockKey(7) == InventoryKeys.TOOL_SHUFFLE, "Shuffle level 7");
        Expect(BoosterUnlockProgress.GetPendingUnlockKey(9) == InventoryKeys.TOOL_PATH, "Path level 9");
        Expect(BoosterUnlockProgress.GetPendingUnlockKey(10) == null, "no late popup for existing users");
        Expect(!BoosterUnlockProgress.IsFirstUseFree(InventoryKeys.TOOL_UNDO), "free use requires intro");
        BoosterUnlockProgress.MarkIntroSeen("unknown");
        Expect(!BoosterUnlockProgress.HasSeenIntro("unknown"), "unknown key rejected");
    }

    private static void VerifyFreeUse(string key, int level)
    {
        GamePrefs.Clear();
        InventoryManager inventory = CreateInventory();
        GameProgressStore.HighestUnlockedLevel = level - 1;
        BoosterUnlockProgress.MarkIntroSeen(key);
        Expect(!inventory.CanUseBooster(key), "free flag cannot bypass level gate");
        GameProgressStore.HighestUnlockedLevel = level;
        Expect(BoosterUnlockProgress.GetPendingUnlockKey(level) == null, "confirmed intro does not repeat");
        Expect(inventory.GetAmount(key) == 0 && inventory.CanUseBooster(key), "free action allowed with empty inventory");
        Expect(!inventory.TryUseBooster(key, Reject), "failed action rejected");
        Expect(BoosterUnlockProgress.IsFirstUseFree(key), "failure retains free action");
        // Shuffle selection is an operation outside the transaction; only the target commits.
        Expect(inventory.TryUseBooster(key, Accept), "successful free action");
        Expect(inventory.GetAmount(key) == 0, "no negative inventory");
        Expect(!inventory.TryUseBooster(key, Accept), "second empty action rejected");
        BoosterUnlockProgress.MarkIntroSeen(key);
        Expect(!BoosterUnlockProgress.IsFirstUseFree(key), "restart cannot replenish free use");
        inventory.Add(key, 3);
        Expect(inventory.TryUseBooster(key, Accept) && inventory.GetAmount(key) == 2, "ordinary use debits inventory");

        GamePrefs.Clear();
        inventory.Add(key, 3);
        BoosterUnlockProgress.MarkIntroSeen(key);
        Expect(inventory.TryUseBooster(key, Accept) && inventory.GetAmount(key) == 3, "first use preserves existing stock");
    }

    private static void VerifyFailedAndReentrantActions()
    {
        GamePrefs.Clear();
        GameProgressStore.HighestUnlockedLevel = 9;
        InventoryManager inventory = CreateInventory();
        BoosterUnlockProgress.MarkIntroSeen(InventoryKeys.TOOL_SHUFFLE);
        bool nestedAccepted = true;
        bool ApplyWithNestedUse()
        {
            nestedAccepted = inventory.TryUseBooster(InventoryKeys.TOOL_SHUFFLE, Accept);
            return true;
        }
        Expect(inventory.TryUseBooster(InventoryKeys.TOOL_SHUFFLE, ApplyWithNestedUse), "outer action succeeds");
        Expect(!nestedAccepted, "reentrant action rejected");
        BoosterUnlockProgress.MarkIntroSeen(InventoryKeys.TOOL_UNDO);
        try { inventory.TryUseBooster(InventoryKeys.TOOL_UNDO, Throw); }
        catch (InvalidOperationException) { }
        Expect(BoosterUnlockProgress.IsFirstUseFree(InventoryKeys.TOOL_UNDO), "exception retains free action");
        Expect(inventory.TryUseBooster(InventoryKeys.TOOL_UNDO, Accept), "transaction guard restored after exception");
        inventory.ResetBoosters();
        Expect(!BoosterUnlockProgress.IsFirstUseFree(InventoryKeys.TOOL_UNDO), "inventory reset does not grant another intro use");
    }

    private static bool Accept() => true;
    private static bool Reject() => false;
    private static bool Throw() => throw new InvalidOperationException("Expected test interruption");

    private static void Expect(bool condition, string scenario)
    {
        if (!condition) throw new Exception(scenario);
        _checks++;
    }
}
