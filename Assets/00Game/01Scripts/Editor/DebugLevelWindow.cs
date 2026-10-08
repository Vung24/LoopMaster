#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>Checks and forces Win/Lose results through the real gameplay flow.</summary>
public static class DebugLevelWindow
{
    public static void Open() => LoopMasterDebugWindow.Open(LoopMasterDebugWindow.Tab.Level);

    [MenuItem("Tools/LoopMaster/Reset Player Progress (Level 1)")]
    public static void ResetPlayerProgress()
    {
        GameProgressStore.DeleteProgress();
        Debug.Log("[LoopMaster] Player progress reset to Level 1.");
    }
}

[Serializable]
public sealed class DebugLevelPanel
{
    private string _status = "Ready. Enter Play Mode and open a level.";
    private Vector2 _scrollPosition;
    [SerializeField] private int _targetLevelInput = 1;

    private static GameController Controller
    {
        get
        {
            GameController controller = GameController.Current;
            return EditorApplication.isPlaying && controller != null &&
                controller.isActiveAndEnabled && controller.Data != null ? controller : null;
        }
    }

    private static bool IsRunning => EditorApplication.isPlaying &&
        !EditorApplication.isPaused && !EditorApplication.isCompiling &&
        !EditorApplication.isUpdating && Time.timeScale > 0f;

    public void DrawGUI()
    {
        GameController controller = Controller;
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EditorGUILayout.LabelField("Current level", controller == null ? "Waiting for gameplay" : controller.CurrentLevelNumber.ToString());
        EditorGUILayout.LabelField("State", controller == null ? "-" : controller.EndState.ToString());

        DrawForceResults(controller);
        DrawResultChecks(controller);
        DrawProgressControls();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(_status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void DrawForceResults(GameController controller)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Force Win / Lose", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!IsRunning || controller == null || !controller.IsGameplayActive))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Force Win", GUILayout.Height(32f))) ForceResult(controller, GameEndState.Won);
            if (GUILayout.Button("Force Lose", GUILayout.Height(32f))) ForceResult(controller, GameEndState.Failed);
        }
        EditorGUILayout.HelpBox("Force Win/Lose runs the normal result flow and opens its dialog. Force Win saves level progress; Force Lose increments the fail count.", MessageType.Info);
    }

    private void DrawResultChecks(GameController controller)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Check Win / Lose", EditorStyles.boldLabel);
        if (controller == null)
        {
            EditorGUILayout.HelpBox("Enter Play Mode and open a level to check or test its result.", MessageType.Info);
            return;
        }

        GameData data = controller.Data;
        CoreMoveResult result = controller.DebugEndResult;
        bool settled = controller.IsDebugGameplaySettled;
        EditorGUILayout.LabelField("Finished boxes", data.GetFinishBoxCount() + " / " + data.Boxes.Count);
        EditorGUILayout.LabelField("Road slices", data.TransferSlice.Count.ToString());
        EditorGUILayout.LabelField("Road capacity (groups)", controller.RoadUsedCapacity + " / " + controller.RoadMaxCapacity);
        EditorGUILayout.LabelField("Gameplay settled", settled ? "Yes" : "Waiting for jumps/unlocks/box moves");
        EditorGUILayout.LabelField("Win condition", result == CoreMoveResult.GameWon ? "Met" : "Not met");
        EditorGUILayout.LabelField("Lose condition", result == CoreMoveResult.GameFailed ? "Met" : "Not met");

        string message = controller.EndState != GameEndState.Playing ?
            "Level ended: " + controller.EndState + ". Replay to test again." :
            result == CoreMoveResult.None ? "Playing: no Win/Lose condition is met." :
            !settled ? "Result pending: waiting for gameplay to settle." :
            result == CoreMoveResult.GameWon ? "Ready for Win." : "Ready for Lose: the road is full and no box can accept a slice.";
        EditorGUILayout.HelpBox(message, MessageType.Info);

        using (new EditorGUI.DisabledScope(!IsRunning || !controller.IsGameplayActive))
        {
            if (GUILayout.Button("Check Win / Lose Now") && controller.TryDebugCheckEndState())
            {
                _status = "Win/Lose checked. State: " + controller.EndState + ".";
                Debug.Log("[Debug Level] " + _status, controller);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Navigation", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!IsRunning || !controller.CanReplayLevel))
        {
            if (GUILayout.Button("Replay Level"))
            {
                _status = controller.TryReplayLevel() ? "Level replayed. Ready to test again." : "Could not replay this level.";
            }
        }
        using (new EditorGUI.DisabledScope(!IsRunning || !controller.CanLoadNextLevel))
        {
            if (GUILayout.Button("Next Level"))
            {
                _status = controller.TryLoadNextLevel() ? "Next level loaded." : "Could not load next level.";
            }
        }
    }

    private void DrawProgressControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Progress & Level Save", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Highest Unlocked", GameProgressStore.HighestUnlockedLevel.ToString());
        EditorGUILayout.LabelField("Highest Completed", GameProgressStore.HighestCompletedLevel.ToString());
        EditorGUILayout.LabelField("Next Playable", GameProgressStore.NextPlayableLevel.ToString());
        EditorGUILayout.LabelField("Campaign Complete", GameProgressStore.IsCampaignComplete ? "Yes" : "No");

        using (new EditorGUILayout.HorizontalScope())
        {
            _targetLevelInput = EditorGUILayout.IntField("Set Level", _targetLevelInput);
            if (GUILayout.Button("Apply", GUILayout.Width(70f)))
            {
                int lvl = Mathf.Max(1, _targetLevelInput);
                GameProgressStore.SetProgress(lvl);
                _status = "Saved progress set to Level " + lvl + ".";
                Debug.Log("[Debug Level] " + _status);
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reset Progress (Level 1)"))
            {
                GameProgressStore.DeleteProgress();
                _targetLevelInput = 1;
                _status = "Progress reset to Level 1.";
                Debug.Log("[Debug Level] " + _status);
            }
            if (GUILayout.Button("Refresh Catalog"))
            {
                BuiltinLevelCatalogBuild.RefreshCatalog();
                _status = "BuiltinLevelCatalog refreshed.";
            }
        }
    }

    private void ForceResult(GameController controller, GameEndState state)
    {
        _status = controller.TryDebugForceEndState(state) ?
            "Forced " + state + " through the normal result flow." : "Could not force the result. Wait for gameplay to be active.";
        Debug.Log("[Debug Level] " + _status, controller);
    }

    public void ResetStatus()
    {
        _status = "Ready. Enter Play Mode and open a level.";
    }
}
#endif
