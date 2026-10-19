using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class GameNavigationEffects : IGameNavigationEffects
{
    public bool IsSceneChanging => GameSceneManager.OnChangeScene;
    public bool ReturnHomeAfterWin
    {
        get
        {
#if UNITY_EDITOR
            if (GamePrefs.EditorCampaignLastLevel > 0) return false;
#endif
            return GameSceneRoutingRules.ReturnHomeAfterWin(GameProgressStore.CurrentPuzzleIndex);
        }
    }
    public bool IsLevelCompleted(int level) => GameProgressStore.IsLevelCompleted(level);
    public bool TrySettlePendingWins() => SpecialStreakManager.Ensure().SettlePending();
    public void SynchronizeStreakLevel() => Managers.StreakLine?.SynchronizeTempLevel();

    public void OnLevelAbandoned()
    {
        SpecialStreakManager.Ensure().OnGameFailed();
        Managers.Event?.DispatchEvent("OnGameFail");
    }

    public async UniTask<bool> EnterHomeAsync(bool withAnimation, bool showFailDialog, CancellationToken token)
    {
        GameData previous = GameData.Current;
        bool clearedCurrent = false;
        Time.timeScale = 1f;
        try
        {
            token.ThrowIfCancellationRequested();
            // HomePage presents pending Lava results after the scene transition and loading dismissal.
            if (IsSceneChanging) return false;
            GameData.ClearCurrent();
            clearedCurrent = true;
            await GameSceneManager.EnterHome(withAnimation);
            return true;
        }
        catch
        {
            if (clearedCurrent && !token.IsCancellationRequested && GameData.Current == null)
                GameData.SetCurrent(previous);
            throw;
        }
    }

    public UniTask DismissLoadingAsync() => LoadingPanel.DismissCurrent();
    public void ReportError(Exception error) => Debug.LogException(error);
}
