#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

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

        DrawNavigation(controller);
        DrawProgressControls();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(_status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void DrawNavigation(GameController controller)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Navigation", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!IsRunning || controller == null || !controller.CanReplayLevel))
        {
            if (GUILayout.Button("Replay Level"))
            {
                _status = controller.TryReplayLevel() ? "Level replayed. Ready to test again." : "Could not replay this level.";
            }
        }
        using (new EditorGUI.DisabledScope(!IsRunning || controller == null || !controller.CanLoadNextLevel))
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

    internal void ForceResult(GameController controller, GameEndState state)
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
