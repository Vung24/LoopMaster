#if UNITY_EDITOR
using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class DebugLevelWindow
{
    public static void Open() => LoopMasterDebugWindow.Open(LoopMasterDebugWindow.Tab.Level);

    [MenuItem("Tools/LoopMaster/Reset Player Progress (Level 1)")]
    public static void ResetPlayerProgress()
    {
        CheatDelegate.ResetAllData(EditorApplication.isPlaying);
        Debug.Log("[LoopMaster] All player progress, inventory, events, and tutorials reset to Level 1.");
    }
}

[Serializable]
public sealed class DebugLevelPanel
{
    private string _status = "Ready. Enter Play Mode and open a level.";
    private Vector2 _scrollPosition;
    [SerializeField] private int _targetLevelInput = 1;
    [SerializeField] private int _undoCount = 3;
    [SerializeField] private int _shuffleCount = 3;
    [SerializeField] private int _pathCount = 3;
    private bool _applyingLevel;

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
        DrawBoosterControls();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(_status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void DrawNavigation(GameController controller)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Navigation", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(_applyingLevel || !IsRunning || controller == null || !controller.CanReplayLevel))
        {
            if (GUILayout.Button("Replay Level"))
            {
                _status = controller.ReplayLevel() ? "Level replayed. Ready to test again." : "Could not replay this level.";
            }
        }
        using (new EditorGUI.DisabledScope(_applyingLevel || !IsRunning || controller == null || !controller.CanLoadNextLevel))
        {
            if (GUILayout.Button("Next Level"))
            {
                _status = controller.LoadNextLevel() ? "Next level loaded." : "Could not load next level.";
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
            using (new EditorGUI.DisabledScope(_applyingLevel || EditorApplication.isPlayingOrWillChangePlaymode && !IsRunning || GameSceneManager.OnChangeScene))
            if (GUILayout.Button("Apply", GUILayout.Width(70f)))
            {
                ApplyLevel().Forget();
            }
        }
        EditorGUILayout.LabelField("Apply saves progress; in Play Mode it also starts the selected level.", EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUI.DisabledScope(_applyingLevel || GameSceneManager.OnChangeScene))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reset Progress (Level 1)"))
            {
                CheatDelegate.ResetAllData(EditorApplication.isPlaying);
                _targetLevelInput = 1;
                _status = "All progress, inventory, events, and tutorials reset to Level 1.";
                Debug.Log("[Debug Level] " + _status);
            }
            if (GUILayout.Button("Refresh Catalog"))
            {
                BuiltinLevelCatalogBuild.RefreshCatalog();
                _status = "BuiltinLevelCatalog refreshed.";
            }
        }
    }

    private async UniTask ApplyLevel()
    {
        if (_applyingLevel) return;
        int level = Mathf.Max(1, _targetLevelInput);
        _targetLevelInput = level;
        if (BuiltinLevelCatalog.Current?.HasLevel(level) != true)
        {
            _status = "Level " + level + " is unavailable. Check the level catalog.";
            return;
        }
        _applyingLevel = true;
        try
        {
            if (EditorApplication.isPlaying)
            {
                _status = "Loading Level " + level + "...";
                if (!await Snui.CheatManager.Ensure().Commands.LoadLevel(level))
                {
                    _status = "Could not load Level " + level + ". Wait for the current gameplay action to finish.";
                    return;
                }
            }
            if (level == 1) CheatDelegate.ResetAllData(false); else GameProgressStore.SetProgress(level);
            _status = EditorApplication.isPlaying ? "Playing Level " + level + "; progress saved." :
                "Saved progress set to Level " + level + ".";
            Debug.Log("[Debug Level] " + _status);
        }
        catch (OperationCanceledException) { _status = "Level loading cancelled."; }
        catch (Exception exception)
        {
            _status = "Level loading failed: " + exception.Message;
            Debug.LogException(exception);
        }
        finally { _applyingLevel = false; }
    }

    private void DrawBoosterControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Booster Inventory", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Set the total count (0 or higher). Changes are saved immediately.", EditorStyles.wordWrappedMiniLabel);
        var inventory = EditorApplication.isPlaying ? InventoryManager.Current : null;
        using (new EditorGUI.DisabledScope(!IsRunning || _applyingLevel || GameSceneManager.OnChangeScene || inventory == null))
        {
            DrawBooster(inventory, "Undo", InventoryKeys.TOOL_UNDO, ref _undoCount);
            DrawBooster(inventory, "Shuffle", InventoryKeys.TOOL_SHUFFLE, ref _shuffleCount);
            DrawBooster(inventory, "Path", InventoryKeys.TOOL_PATH, ref _pathCount);
        }
        if (inventory == null) EditorGUILayout.HelpBox("Enter Play Mode and wait for inventory to initialize.", MessageType.Info);
    }

    private void DrawBooster(InventoryManager inventory, string label, string key, ref int target)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            int current = inventory == null ? 0 : inventory.GetAmount(key);
            target = EditorGUILayout.IntField(label + " (current: " + current + ")", target);
            using (new EditorGUI.DisabledScope(target < 0))
            if (GUILayout.Button("Set", GUILayout.Width(70f)))
            {
                bool changed = target >= current ? inventory.Add(key, target - current, "debug") :
                    inventory.Use(key, current - target, "debug");
                _status = changed ? label + " count set to " + target + "." : "Inventory is busy; try again.";
            }
        }
    }

    internal void ForceResult(GameController controller, GameEndState state)
    {
        _status = controller.DebugForceEndState(state) ?
            "Forced " + state + " through the normal result flow." : "Could not force the result. Wait for gameplay to be active.";
        Debug.Log("[Debug Level] " + _status, controller);
    }

    public void ResetStatus()
    {
        _status = "Ready. Enter Play Mode and open a level.";
    }
}
#endif
