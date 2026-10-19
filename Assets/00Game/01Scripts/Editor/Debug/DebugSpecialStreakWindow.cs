#if UNITY_EDITOR
using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class DebugSpecialStreakWindow
{
    public static void Open() => LoopMasterDebugWindow.Open(LoopMasterDebugWindow.Tab.SpecialStreak);
}

[Serializable]
public sealed class DebugSpecialStreakPanel
{
    [SerializeField] private int _multiplierIndex = 1;
    [SerializeField] private bool _showTutorial;
    private Vector2 _scroll;
    private string _status = "Enter Play Mode to test SpecialStreak.";
    private bool _presenting;
    private static readonly string[] MultiplierLabels = { "x1", "x5", "x10", "x25", "x100" };

    private static bool Running => EditorApplication.isPlaying && !EditorApplication.isPaused &&
        !EditorApplication.isCompiling && !EditorApplication.isUpdating && Time.timeScale > 0f;

    public void DrawGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.HelpBox("Test on a development save. Presets, wins and rewards persist in GamePrefs. Presets keep claimed stages and inventory intact; completed events cannot be reseeded here.", MessageType.Info);
        var manager = EditorApplication.isPlaying ? SpecialStreakManager.Current : null;
        var controller = EditorApplication.isPlaying ? GameController.Current : null;
        var home = EditorApplication.isPlaying ? HomeController.Current : null;
        var progress = home == null ? null : home.GetComponentInChildren<SpecialStreakProgress>(true);
        DrawState(manager, controller, progress);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prepare scenario", EditorStyles.boldLabel);
        _multiplierIndex = EditorGUILayout.Popup("Multiplier", _multiplierIndex, MultiplierLabels);
        _showTutorial = EditorGUILayout.Toggle("Show tutorial", _showTutorial);
        bool busy = _presenting || (progress != null && (progress.IsAnimating || progress.HasDialogOpen));
        using (new EditorGUI.DisabledScope(!Running || manager == null || manager.IsCompleted() ||
            manager.ShouldSettleCollectionResult() || busy || (controller != null && controller.EndState != GameEndState.Playing)))
        {
            if (GUILayout.Button("Prepare Win / Lose / Revive"))
                Prepare(manager, false);
            if (GUILayout.Button("Prepare One Win To Next Reward"))
                Prepare(manager, true);
            using (new EditorGUI.DisabledScope(home == null || progress == null))
            {
                if (GUILayout.Button("Home: Play Collection + Reward Popup"))
                {
                    if (manager.TryDebugPrepare(_multiplierIndex, true, _showTutorial, true))
                    {
                        var canvas = progress.GetComponentInParent<Canvas>();
                        PresentCollection(canvas == null ? null : canvas.rootCanvas.transform).Forget();
                        SetStatus("Seeded one pending win; playing collection and milestone reward popup.");
                    }
                    else SetStatus("Could not prepare home animation.");
                }
            }
        }
        EditorGUILayout.HelpBox("Prepare activates the event even below level 21. Play a fresh level: replaying a completed level never adds a streak win. Settle pending collection and close the home dialog before changing a preset.", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Play through game flow", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!Running || controller == null || manager == null ||
            !manager.ShouldSettleCollectionResult() || _presenting))
        {
            if (GUILayout.Button("Play Pending Win Collection / Reward Popup"))
                PresentCollection(controller.GetComponent<GameResultUI>()?.DialogParent).Forget();
        }

        using (new EditorGUI.DisabledScope(!Running || controller == null || manager == null || !controller.CanRevive))
        {
            if (GUILayout.Button("Revive - Check Multiplier Kept"))
            {
                int before = manager.MultiplierIndex;
                bool revived = controller.Revive();
                SetStatus(!revived ? "Revive unavailable." : manager.MultiplierIndex == before ?
                    "PASS: Revive kept multiplier." : "FAIL: Revive changed multiplier.");
            }
        }
        using (new EditorGUI.DisabledScope(!Running || controller == null || manager == null || !controller.CanReplayLevel))
        {
            if (GUILayout.Button("Retry - Check Multiplier Reset"))
            {
                bool ongoing = manager.IsOngoing;
                bool replayed = controller.ReplayLevel();
                SetStatus(!replayed ? "Retry unavailable." : !ongoing ? "Retry loaded; event is inactive." :
                    manager.MultiplierIndex == 0 ? "PASS: Retry reset multiplier to x1." : "FAIL: Retry did not reset multiplier.");
            }
        }
        using (new EditorGUI.DisabledScope(!Running || controller == null || !controller.CanLoadNextLevel))
        {
            if (GUILayout.Button("Next Level / Settle Collection"))
                SetStatus(controller.LoadNextLevel() ? "Next level loaded; collection settled." : "Could not advance; inspect event error.");
        }
        using (new EditorGUI.DisabledScope(!Running || controller == null))
        {
            if (GUILayout.Button("Return Home / Show Collection"))
                SetStatus(controller.ReturnHome() ? "Returning home; pending collection will be shown on arrival." : "Could not return home; inspect event error.");
        }
        using (new EditorGUI.DisabledScope(!Running || home == null || !home.CanOpenSpecialStreak || progress == null || manager == null || manager.IsLocked))
        {
            if (GUILayout.Button("Open Home SpecialStreak Dialog"))
            {
                progress.OnClick();
                SetStatus("Requested dialog through the home widget.");
            }
        }
        EditorGUILayout.HelpBox("Force Win, then press CLAIM in the Win dialog: coins, return Home, SpecialStreak collection and milestone reward popup. Direct Next settles without presentation; Return Home presents pending collection on arrival. Lose keeps multiplier for Revive; Retry/Quit resets it.", MessageType.Info);
        EditorGUILayout.HelpBox(_status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private static void DrawState(SpecialStreakManager manager, GameController controller, SpecialStreakProgress progress)
    {
        if (manager == null) { EditorGUILayout.LabelField("Event", "Waiting for Play Mode"); return; }
        EditorGUILayout.LabelField("Event", manager.IsLocked ? "Locked" : manager.IsCompleted() ? "Completed" : manager.IsOngoing ? "Ongoing" : "Invalid");
        var reward = manager.GetCurrRewardInfo();
        EditorGUILayout.LabelField("Stage / claimed", manager.ClaimedIndex + " / " + (manager.GetRewardInfosConfig()?.Count ?? 0));
        EditorGUILayout.LabelField("Count / goal", manager.CurrCount + " / " + (reward == null ? "-" : reward.Goal.ToString()));
        EditorGUILayout.LabelField("Pending wins", manager.TempCount.ToString());
        EditorGUILayout.LabelField("Multiplier / previous", "x" + manager.GetCurrentMultiplier() + " / x" + manager.GetMultiplier(manager.LastMultiplierIndex));
        EditorGUILayout.LabelField("Pending reward", manager.WillPendingCollectionClaimReward() ? "Yes" : "No");
        EditorGUILayout.LabelField("Tutorial / settle", manager.ShouldShowTutorial() + " / " + manager.ShouldSettleCollectionResult());
        EditorGUILayout.LabelField("Lose warning / fail banner", manager.ShouldShowLoseItem() + " / " + manager.ShouldShowFailBanner());
        EditorGUILayout.LabelField("Home animation / dialog", progress == null ? "-" : progress.IsAnimating + " / " + progress.HasDialogOpen);
        EditorGUILayout.LabelField("Level / result", controller == null ? "Home" : controller.CurrentLevelNumber + " / " + controller.EndState);
        if (controller != null && GameProgressStore.IsLevelCompleted(controller.CurrentLevelNumber))
            EditorGUILayout.HelpBox("This level was already completed. Its win will not count toward SpecialStreak.", MessageType.Warning);
        if (!string.IsNullOrEmpty(manager.LastError)) EditorGUILayout.HelpBox(manager.LastError, MessageType.Error);
    }

    private void Prepare(SpecialStreakManager manager, bool nearReward)
    {
        SetStatus(manager.TryDebugPrepare(_multiplierIndex, nearReward, _showTutorial) ?
            "Prepared " + (nearReward ? "one win to reward" : "streak scenario") + "." : "Could not prepare; finish pending collection first.");
    }

    private async UniTaskVoid PresentCollection(Transform parent)
    {
        _presenting = true;
        try
        {
            // bool shown = await SpecialStreakProgress.ShowWinCollection(parent);
            SetStatus( "Collection settled; reached milestones showed reward popup.");
        }
        catch (OperationCanceledException) { SetStatus("Presentation cancelled by scene change."); }
        finally { _presenting = false; }
    }

    internal void NotifyResult(GameController controller, SpecialStreakManager manager, bool win)
    {
        if (manager == null || controller == null) return;
        int before = manager.TempCount;
        int multiplier = manager.MultiplierIndex;
        bool eligible = manager.IsOngoing && !GameProgressStore.IsLevelCompleted(controller.CurrentLevelNumber);
        if (!win) SetStatus(manager.MultiplierIndex == multiplier ? "PASS: Lose kept multiplier pending Revive/Retry/Quit." : "FAIL: Lose changed multiplier early.");
        else if (!eligible) SetStatus("Win completed; event inactive or level already completed, no streak credit expected.");
        else SetStatus(manager.TempCount == before + 1 ? "PASS: Win added one pending collection." : "FAIL: Win did not add expected pending collection.");
    }

    private void SetStatus(string message) => _status = message;
}
#endif
