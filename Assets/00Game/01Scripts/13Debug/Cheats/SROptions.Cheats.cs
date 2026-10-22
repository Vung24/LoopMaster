using System.ComponentModel;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Snui;

public partial class SROptions
{
    private int _targetLevel = 1;

    #region 01. Gameplay & Levels

    [Category("01. Gameplay & Levels"), DisplayName("Target Level"), Sort(1)]
    public int TargetLevel
    {
        get
        {
            if (_targetLevel < 1)
            {
                int current = GameController.Current != null && GameController.Current.CurrentLevelNumber > 0
                    ? GameController.Current.CurrentLevelNumber
                    : GameProgressStore.NextPlayableLevel;
                _targetLevel = Mathf.Max(1, current);
            }
            return _targetLevel;
        }
        set
        {
            _targetLevel = Mathf.Max(1, value);
            OnPropertyChanged(nameof(TargetLevel));
        }
    }

    [Category("01. Gameplay & Levels"), DisplayName("Jump to Target Level"), Sort(2)]
    public void JumpToTargetLevel()
    {
        CheatManager.Ensure().Commands.LoadLevel(TargetLevel).Forget();
        OnPropertyChanged(nameof(CurrentLevelStatus));
    }

    [Category("01. Gameplay & Levels"), DisplayName("Win Current Level"), Sort(3)]
    public void WinCurrentLevel()
    {
        var game = GameController.Current;
        if (game == null || game.Data == null || game.EndState != GameEndState.Playing)
        {
            Debug.LogWarning("[SRDebug Cheat] Cannot win: game is not in active playing state.");
            return;
        }
        CheatManager.Ensure().Commands.ForceEnd(true);
    }

    [Category("01. Gameplay & Levels"), DisplayName("Fail Current Level"), Sort(4)]
    public void FailCurrentLevel()
    {
        var game = GameController.Current;
        if (game == null || game.Data == null || game.EndState != GameEndState.Playing)
        {
            Debug.LogWarning("[SRDebug Cheat] Cannot fail: game is not in active playing state.");
            return;
        }
        CheatManager.Ensure().Commands.ForceEnd(false);
    }

    [Category("01. Gameplay & Levels"), DisplayName("Next Level (+1)"), Sort(5)]
    public void NextLevel()
    {
        int current = GameController.Current != null && GameController.Current.CurrentLevelNumber > 0
            ? GameController.Current.CurrentLevelNumber
            : GameProgressStore.NextPlayableLevel;
        int next = current + 1;
        TargetLevel = next;
        CheatManager.Ensure().Commands.LoadLevel(next).Forget();
        OnPropertyChanged(nameof(CurrentLevelStatus));
    }

    [Category("01. Gameplay & Levels"), DisplayName("Previous Level (-1)"), Sort(6)]
    public void PrevLevel()
    {
        int current = GameController.Current != null && GameController.Current.CurrentLevelNumber > 0
            ? GameController.Current.CurrentLevelNumber
            : GameProgressStore.NextPlayableLevel;
        if (current <= 1) return;
        int prev = current - 1;
        TargetLevel = prev;
        CheatManager.Ensure().Commands.LoadLevel(prev).Forget();
        OnPropertyChanged(nameof(CurrentLevelStatus));
    }

    [Category("01. Gameplay & Levels"), DisplayName("Restart Current Level"), Sort(7)]
    public void RestartLevel()
    {
        int current = GameController.Current != null && GameController.Current.CurrentLevelNumber > 0
            ? GameController.Current.CurrentLevelNumber
            : GameProgressStore.NextPlayableLevel;
        TargetLevel = current;
        CheatManager.Ensure().Commands.LoadLevel(current).Forget();
    }

    [Category("01. Gameplay & Levels"), DisplayName("Current Level Status"), Sort(8)]
    public string CurrentLevelStatus
    {
        get
        {
            int current = GameController.Current != null && GameController.Current.CurrentLevelNumber > 0
                ? GameController.Current.CurrentLevelNumber
                : GameProgressStore.NextPlayableLevel;
            int total = BuiltinLevelCatalog.Current != null ? BuiltinLevelCatalog.Current.HighestLevel : 0;
            return total > 0 ? $"Level {current} / {total}" : $"Level {current}";
        }
    }

    [Category("01. Gameplay & Levels"), DisplayName("Reset All Progress (Level 1)"), Sort(9)]
    public void ResetAllProgress()
    {
        CheatManager.Ensure().Commands.ResetAllProgress().Forget();
        TargetLevel = 1;
        OnPropertyChanged(nameof(TargetLevel));
        OnPropertyChanged(nameof(CurrentLevelStatus));
        OnPropertyChanged(nameof(CurrentCoins));
        OnPropertyChanged(nameof(BoostersStatus));
        OnPropertyChanged(nameof(HeartsStatus));
        OnPropertyChanged(nameof(SkipTutorial));
    }

    #endregion

    #region Booster Unlock Test

    [Category("05. Booster Unlock Test"), DisplayName("Test Undo Unlock"), Sort(40)]
    public void TestUndoUnlock()
    {
        StartBoosterUnlockTest(InventoryKeys.TOOL_UNDO);
    }

    [Category("05. Booster Unlock Test"), DisplayName("Test Shuffle Unlock"), Sort(41)]
    public void TestShuffleUnlock()
    {
        StartBoosterUnlockTest(InventoryKeys.TOOL_SHUFFLE);
    }

    [Category("05. Booster Unlock Test"), DisplayName("Test Extra Slot Unlock"), Sort(42)]
    public void TestPathUnlock()
    {
        StartBoosterUnlockTest(InventoryKeys.TOOL_PATH);
    }

    private void StartBoosterUnlockTest(string key)
    {
        CheatManager.Ensure().Commands.TestBoosterUnlock(key).Forget();
        OnPropertyChanged(nameof(SkipTutorial));
    }

    #endregion

    #region 02. Currencies & Boosters

    [Category("02. Currencies & Boosters"), DisplayName("+1,000 Coins"), Sort(10)]
    public void AddCoins1000()
    {
        CheatManager.Ensure().Commands.ChangeInventory(RewardType.Coin, 1000, true);
        OnPropertyChanged(nameof(CurrentCoins));
    }

    [Category("02. Currencies & Boosters"), DisplayName("+10,000 Coins"), Sort(11)]
    public void AddCoins10000()
    {
        CheatManager.Ensure().Commands.ChangeInventory(RewardType.Coin, 10000, true);
        OnPropertyChanged(nameof(CurrentCoins));
    }

    [Category("02. Currencies & Boosters"), DisplayName("Current Coins"), Sort(12)]
    public int CurrentCoins => CheatManager.Ensure().Commands.GetAmount(RewardType.Coin);

    [Category("02. Currencies & Boosters"), DisplayName("+5 Undo Boosters"), Sort(13)]
    public void AddUndo5()
    {
        CheatManager.Ensure().Commands.ChangeInventory(RewardType.Undo, 5, true);
        OnPropertyChanged(nameof(BoostersStatus));
    }

    [Category("02. Currencies & Boosters"), DisplayName("+5 Shuffle Boosters"), Sort(14)]
    public void AddShuffle5()
    {
        CheatManager.Ensure().Commands.ChangeInventory(RewardType.Shuffle, 5, true);
        OnPropertyChanged(nameof(BoostersStatus));
    }

    [Category("02. Currencies & Boosters"), DisplayName("+5 Path Boosters"), Sort(15)]
    public void AddPath5()
    {
        CheatManager.Ensure().Commands.ChangeInventory(RewardType.Path, 5, true);
        OnPropertyChanged(nameof(BoostersStatus));
    }

    [Category("02. Currencies & Boosters"), DisplayName("+5 All Boosters"), Sort(16)]
    public void AddAllBoosters5()
    {
        var cmd = CheatManager.Ensure().Commands;
        cmd.ChangeInventory(RewardType.Undo, 5, true);
        cmd.ChangeInventory(RewardType.Shuffle, 5, true);
        cmd.ChangeInventory(RewardType.Path, 5, true);
        OnPropertyChanged(nameof(BoostersStatus));
    }

    [Category("02. Currencies & Boosters"), DisplayName("Boosters Status"), Sort(17)]
    public string BoostersStatus
    {
        get
        {
            var cmd = CheatManager.Ensure().Commands;
            int undo = cmd.GetAmount(RewardType.Undo);
            int shuffle = cmd.GetAmount(RewardType.Shuffle);
            int path = cmd.GetAmount(RewardType.Path);
            return $"Undo: {undo} | Shuffle: {shuffle} | Path: {path}";
        }
    }

    #endregion

    #region 03. Lives & Hearts

    [Category("03. Lives & Hearts"), DisplayName("Refill Hearts to Full"), Sort(20)]
    public void RefillHearts()
    {
        var heart = HeartManager.EnsureInitialized();
        int needed = heart.MAX_HEART - heart.GetHeartCount();
        if (needed > 0)
        {
            CheatManager.Ensure().Commands.ChangeInventory(RewardType.Heart, needed, true);
        }
        OnPropertyChanged(nameof(HeartsStatus));
    }

    [Category("03. Lives & Hearts"), DisplayName("+1 Hour Infinite Hearts"), Sort(21)]
    public void AddInfiniteHearts1Hour()
    {
        CheatManager.Ensure().Commands.ChangeInventory(RewardType.InfinityHeart, 3600, true);
        OnPropertyChanged(nameof(HeartsStatus));
    }

    [Category("03. Lives & Hearts"), DisplayName("Clear Infinite Hearts"), Sort(22)]
    public void ClearInfiniteHearts()
    {
        CheatManager.Ensure().Commands.ChangeInventory(RewardType.InfinityHeart, 0, false);
        OnPropertyChanged(nameof(HeartsStatus));
    }

    [Category("03. Lives & Hearts"), DisplayName("VIP Hearts (8 Max)"), Sort(23)]
    public bool VipHearts
    {
        get => HeartManager.EnsureInitialized().MAX_HEART == BaseHeartManager.PASS_VIP_MAX_HEART;
        set
        {
            CheatManager.Ensure().Commands.SetVipHearts(value);
            OnPropertyChanged(nameof(VipHearts));
            OnPropertyChanged(nameof(HeartsStatus));
        }
    }

    [Category("03. Lives & Hearts"), DisplayName("Hearts Status"), Sort(24)]
    public string HeartsStatus
    {
        get
        {
            var heart = HeartManager.EnsureInitialized();
            if (heart.IsInfinite())
            {
                int leftSec = heart.InfiniteLeftTime;
                return $"Infinite ({leftSec / 60}m {leftSec % 60}s)";
            }
            return $"{heart.GetHeartCount()} / {heart.MAX_HEART}";
        }
    }

    #endregion

    #region 04. Game Settings & Debug

    [Category("04. Game Settings & Debug"), DisplayName("Skip Tutorial"), Sort(30)]
    public bool SkipTutorial
    {
        get => CheatManager.SkipTutorial;
        set
        {
            CheatManager.Ensure().Commands.SetSkipTutorial(value);
            OnPropertyChanged(nameof(SkipTutorial));
        }
    }

    [Category("04. Game Settings & Debug"), DisplayName("Log Game State"), Sort(31)]
    public void LogGameState()
    {
        int lvl = GameController.Current != null && GameController.Current.CurrentLevelNumber > 0
            ? GameController.Current.CurrentLevelNumber
            : GameProgressStore.NextPlayableLevel;
        var heart = HeartManager.EnsureInitialized();
        int coins = InventoryManager.Ensure().GetAmount(RewardType.Coin.ToInventoryKey());
        Debug.Log($"[CheatManager] Level: {lvl}, Coins: {coins}, Hearts: {heart.GetHeartCount()}/{heart.MAX_HEART}, Infinite: {heart.IsInfinite()}, SkipTutorial: {CheatManager.SkipTutorial}");
    }

    #endregion
}
