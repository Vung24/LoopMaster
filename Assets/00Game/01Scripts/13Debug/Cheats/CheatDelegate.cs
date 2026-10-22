using UnityEngine;
using Cysharp.Threading.Tasks;
using Snui;
using System.Collections.Generic;

public class CheatDelegate : MonoBehaviour
{
    private bool _loadingBoosterTest;
    public static readonly RewardType[] InventoryTypes =
        { RewardType.Coin, RewardType.Undo, RewardType.Shuffle, RewardType.Path,
          RewardType.Heart, RewardType.InfinityHeart };

    public bool ForceEnd(bool win)
    {
        var game = GameController.Current;
        if (game == null || game.Data == null || game.EndState != GameEndState.Playing || GameSceneManager.OnChangeScene) return false;
        if (win) game.CheatWin(); else game.GameFailed();
        return game.EndState == (win ? GameEndState.Won : GameEndState.Failed);
    }

    public async UniTask<bool> LoadLevel(int level)
    {
        bool enteredFromHome = false;
        if (level < 1 || GameSceneManager.OnChangeScene ||
            BuiltinLevelCatalog.Current?.HasLevel(level) != true) return false;
        if (GameController.Current == null)
        {
            if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().IsValid() ||
                !Application.CanStreamedLevelBeLoaded("GameScene")) return false;
            CheatManager.RequestedLevel = level;
            try { await GameSceneManager.EnterGame(GameEnterRoute.Deeplink, false); }
            finally { CheatManager.RequestedLevel = 0; }
            enteredFromHome = true;
        }
        var game = GameController.Current;
        return game != null && ((enteredFromHome && game.CurrentLevelNumber == level) || game.CheatLoadLevel(level));
    }

    /// <summary>Resets one booster introduction and enters its real milestone level for testing.</summary>
    public async UniTask TestBoosterUnlock(string key)
    {
        if (_loadingBoosterTest || GameSceneManager.OnChangeScene) return;
        if (!BoosterUnlockConfig.TryGetInfo(key, out BoosterUnlockInfo info)) return;
        BuiltinLevelCatalog catalog = BuiltinLevelCatalog.Current;
        if (catalog == null || !catalog.HasLevel(info.UnlockLevel))
        {
            Debug.LogWarning("[Booster Debug] Unlock level is unavailable: " + info.UnlockLevel);
            return;
        }

        _loadingBoosterTest = true;
        try
        {
            GameController game = GameController.Current;
            if (game != null) game.CancelCheatTutorials();
            if (SRDebug.Instance != null) SRDebug.Instance.HideDebugPanel();
            GamePrefs.DeleteKey(info.IntroPreferenceKey);
            GamePrefs.DeleteKey(info.FreeUsePreferenceKey);
            SetSkipTutorial(false);
            bool loaded = await LoadLevel(info.UnlockLevel);
            if (!loaded)
            {
                Debug.LogWarning("[Booster Debug] Could not enter the unlock level. Start gameplay and try again.");
                return;
            }
            Debug.Log("[Booster Debug] Testing " + info.Title + " at level " + info.UnlockLevel +
                ". Intro reset; Skip Tutorial disabled; inventory count preserved.");
        }
        finally
        {
            _loadingBoosterTest = false;
        }
    }

    public bool ChangeInventory(RewardType type, int amount, bool add)
    {
        if (type == null || amount <= 0) return false;
        if (type == RewardType.InfinityHeart)
        {
            var heart = HeartManager.EnsureInitialized();
            if (!add)
            {
                heart.InfiniteHeartEndTime = (int)TimeUtils.GetUnixTimestamp();
                heart.OnInfinityHeartChanged.Invoke();
                return true;
            }
            long end = System.Math.Max(heart.InfiniteHeartEndTime, TimeUtils.GetUnixTimestamp());
            if (end + amount > int.MaxValue) return false;
            heart.AddInfinite(amount);
            return true;
        }
        if (type == RewardType.Heart)
        {
            var heart = HeartManager.EnsureInitialized();
            if (add)
            {
                int count = Mathf.Min(amount, Mathf.Max(0, heart.MAX_HEART - heart.GetHeartCount()));
                for (int i = 0; i < count; i++) heart.AddHeart();
                return count > 0;
            }
            if (heart.IsInfinite() || amount > heart.GetHeartCount()) return false;
            for (int i = 0; i < amount; i++) heart.UseHeart();
            return true;
        }
        var inventory = InventoryManager.Ensure();
        return add ? inventory.Add(type.ToInventoryKey(), amount, "cheat") :
            inventory.Use(type.ToInventoryKey(), amount, "cheat");
    }

    public int GetAmount(RewardType type)
    {
        if (type == RewardType.Heart) return HeartManager.EnsureInitialized().GetHeartCount();
        if (type == RewardType.InfinityHeart) return HeartManager.EnsureInitialized().InfiniteLeftTime;
        return type == null ? 0 : InventoryManager.Ensure().GetAmount(type.ToInventoryKey());
    }

    public bool AddEventItems(int eventIndex, int amount)
    {
        return false;
    }

    public void SetSkipTutorial(bool enabled)
    {
        CheatManager.SkipTutorial = enabled;
        if (enabled && GameController.Current != null) GameController.Current.CancelCheatTutorials();
    }

    public void SetVipHearts(bool enabled)
    {
        var heart = HeartManager.EnsureInitialized();
        if (enabled) heart.OnPassVipActivated(); else heart.OnPassVipExpired();
    }

    public static void ResetAllData(bool reloadLevelIfPlaying = true)
    {
        // 1. Level progression & navigation
        GameProgressStore.DeleteProgress();
        GamePrefs.DeleteKey(GamePrefKeys.LevelPlayCount);
        GamePrefs.DeleteKey(GamePrefKeys.CompletedToday);
        GamePrefs.DeleteKey(GamePrefKeys.TestFailCount);
        GamePrefs.DeleteKey(GamePrefKeys.LastRoute);
        GamePrefs.DeleteKey(GamePrefKeys.FreeRevive);
        GamePrefs.DeleteKey(GamePrefKeys.SpeedUp);
        GamePrefs.DeleteKey(GamePrefKeys.RateUs);
        GamePrefs.DeleteKey(GamePrefKeys.HomePreviousLevel);
        GamePrefs.DeleteKey(GamePrefKeys.HomeTargetLevel);
        GamePrefs.DeleteKey(GamePrefKeys.HomeHardLevel);

        // 2. Tutorials
        GamePrefs.DeleteKey(GamePrefKeys.FeatureTutorial);
        GamePrefs.DeleteKey(GamePrefKeys.ProfileTutorial);
        GamePrefs.DeleteKey(GamePrefKeys.SpeedTutorial);
        GamePrefs.DeleteKey(GamePrefKeys.FailTutorial);
        GamePrefs.DeleteKey(GamePrefKeys.PassTutorial);
        CheatManager.SkipTutorial = false;

        // 3. Booster introductions & free uses
        BoosterUnlockProgress.ResetAll();
        GamePrefs.DeleteKey("BoosterIntro_" + InventoryKeys.TOOL_UNDO);
        GamePrefs.DeleteKey("BoosterFirstUseFree_" + InventoryKeys.TOOL_UNDO);
        GamePrefs.DeleteKey("BoosterIntro_" + InventoryKeys.TOOL_SHUFFLE);
        GamePrefs.DeleteKey("BoosterFirstUseFree_" + InventoryKeys.TOOL_SHUFFLE);
        GamePrefs.DeleteKey("BoosterIntro_" + InventoryKeys.TOOL_PATH);
        GamePrefs.DeleteKey("BoosterFirstUseFree_" + InventoryKeys.TOOL_PATH);

        // 4. Inventory & Rewards
        if (Application.isPlaying && InventoryManager.Current != null)
        {
            InventoryManager.Current.ClearRewardData();
            InventoryManager.Current.ResetInventory();
        }
        else
        {
            GamePrefs.DeleteKey(GamePrefKeys.Coins);
            GamePrefs.DeleteKey(GamePrefKeys.Undo);
            GamePrefs.DeleteKey(GamePrefKeys.Shuffle);
            GamePrefs.DeleteKey(GamePrefKeys.Path);
            GamePrefs.DeleteKey(GamePrefKeys.RewardJournal);
            GamePrefs.DeleteKey(GamePrefKeys.RewardReceipts);
        }

        // 5. Hearts & Lives
        if (Application.isPlaying && Managers.Heart != null)
        {
            Managers.Heart.ClearData();
            Managers.Heart.InfiniteHeartEndTime = (int)TimeUtils.GetUnixTimestamp();
            if (Managers.Heart.OnInfinityHeartChanged != null)
            {
                Managers.Heart.OnInfinityHeartChanged.Invoke();
            }
            if (Managers.Heart.OnHeartChanged != null)
            {
                Managers.Heart.OnHeartChanged.Invoke();
            }
            Managers.Heart.OnPassVipExpired();
        }
        else
        {
            GamePrefs.DeleteKey(GamePrefKeys.HeartCount);
            GamePrefs.DeleteKey(GamePrefKeys.HeartRefillTime);
            GamePrefs.DeleteKey(GamePrefKeys.InfiniteHeartEnd);
            GamePrefs.DeleteKey(GamePrefKeys.FreeHearts);
            GamePrefs.DeleteKey(GamePrefKeys.BonusHeartCount);
        }

        // 6. Events
        SpecialStreakManager.ClearOfflineData();
        LevelBoxManager.ClearOfflineData();
        if (Managers.StreakLine != null)
        {
            Managers.StreakLine.ClearData();
        }
        else
        {
            GamePrefs.DeleteKey(GamePrefKeys.StreakLine);
        }

        if (Managers.Pass != null)
        {
            Managers.Pass.ClearData();
        }
        else
        {
            GamePrefs.DeleteKey(GamePrefKeys.PassState);
            GamePrefs.DeleteKey(GamePrefKeys.PassTutorial);
        }

        if (DailyCheckInManager.Instance != null)
        {
            DailyCheckInManager.Instance.ClearData();
        }
        GamePrefs.DeleteKey(GamePrefKeys.DailyCheckIn);
        IoUtils.DeleteJsonFile("lava_data");
        IoUtils.DeleteJsonFile("lava_status");

        // 7. Flush persistence
        GamePrefs.SaveImmediate();
        PlayerPrefs.Save();

        // 8. Runtime reload Level 1 if playing
        if (reloadLevelIfPlaying && Application.isPlaying)
        {
            if (GameController.Current != null)
            {
                GameController.Current.CancelCheatTutorials();
            }
            CheatManager cheat = CheatManager.Ensure();
            if (cheat != null && cheat.Commands != null)
            {
                cheat.Commands.LoadLevel(1).Forget();
            }
        }
    }

    public async UniTask<bool> ResetAllProgress()
    {
        ResetAllData(false);
        if (GameController.Current != null)
        {
            GameController.Current.CancelCheatTutorials();
        }
        return await LoadLevel(1);
    }
}
