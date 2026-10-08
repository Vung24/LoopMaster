using UnityEngine;
using Cysharp.Threading.Tasks;
using Snui;
using System.Collections.Generic;

public class CheatDelegate : MonoBehaviour
{
    // These adapters are rebuilt from the surviving prefab controls. Only
    // CheatItem, CheatItemSelector and GameController.CheatWin survive in IL2CPP.
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
            Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName)?.GetLevel(level) == null) return false;
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
        return game != null && ((enteredFromHome && game.CurrentLevelNumber == level) || game.TryCheatLoadLevel(level));
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
        if (amount <= 0) return false;
        if (eventIndex == 0)
        {
            var manager = Managers.SpecialCol;
            if (manager == null || !manager.IsOngoing || manager.IsExpired || manager.ShouldShowTutorial()) return false;
            long added = (long)amount * (manager.IsDouble ? 2 : 1);
            if ((long)manager.CurrCount + manager.TempCount + added > int.MaxValue) return false;
            int before = manager.TempCount;
            manager.OnGameWon(new Dictionary<string, int> { { GameWinArgs.SpecialCollectible, amount } });
            return manager.TempCount > before;
        }
        if (eventIndex == 1)
        {
            var manager = Managers.SpaceRace;
            if (manager == null || !manager.IsOngoing || manager.IsWin || manager.IsFail ||
                (long)manager.Score + manager.TempScore + amount > int.MaxValue) return false;
            int before = manager.TempScore;
            manager.OnGameWin(new Dictionary<string, int> { { GameWinArgs.SpaceRaceStar, amount } });
            return manager.TempScore > before;
        }
        return false;
    }

    public void SetSkipTutorial(bool enabled)
    {
        CheatManager.SkipTutorial = enabled;
        if (enabled) GameController.Current?.CancelCheatTutorials();
    }

    public void SetVipHearts(bool enabled)
    {
        var heart = HeartManager.EnsureInitialized();
        if (enabled) heart.OnPassVipActivated(); else heart.OnPassVipExpired();
    }
}
