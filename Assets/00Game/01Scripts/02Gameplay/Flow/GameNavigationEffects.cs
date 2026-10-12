using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// Unity/service boundary for navigation; it never holds a GameController.
public sealed class GameNavigationEffects : IGameNavigationEffects
{
    public Transform DialogParent { get; set; }
    public bool IsSceneChanging => GameSceneManager.OnChangeScene;
    public bool ReturnHomeAfterWin => GameSceneRoutingRules.ReturnHomeAfterWin(GameProgressStore.CurrentPuzzleIndex);
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
            if (showFailDialog && Managers.Lava != null && Managers.Lava.ShouldShowFailDialog())
            {
                var dialog = await Snui.Lava.LavaDetailDialog.Show(DialogParent);
                if (token.IsCancellationRequested)
                {
                    if (dialog != null) UnityEngine.Object.Destroy(dialog.gameObject);
                    token.ThrowIfCancellationRequested();
                }
                if (dialog != null)
                {
                    Snui.RewardDialog.PrepareOverlay(dialog.gameObject, 1400);
                    try { await dialog.WaitForCloseAsync().AttachExternalCancellation(token); }
                    finally
                    {
                        if (token.IsCancellationRequested && dialog != null)
                            UnityEngine.Object.Destroy(dialog.gameObject);
                    }
                }
            }
            token.ThrowIfCancellationRequested();
            if (IsSceneChanging) return false;
            GameData.ClearCurrent();
            clearedCurrent = true;
            // Scene destruction is expected here; don't cancel the scene load with the old scene's token.
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

    public UniTask DismissLoadingAsync() => LoadingPanel.TryDismiss();
    public void ReportError(Exception error) => Debug.LogException(error);
}
