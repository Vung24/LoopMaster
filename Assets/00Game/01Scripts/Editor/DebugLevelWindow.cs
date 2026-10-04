#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>Plays solutions and checks or forces results through the real gameplay flow.</summary>
public sealed class DebugLevelWindow : EditorWindow
{
    [SerializeField] private float _stepDelay = 0.4f;
    [SerializeField] private float _nextDelay = 2f;
    [SerializeField] private bool _autoNext;
    [SerializeField] private int _maxStates = 10000;
    private bool _autoPlay, _oneStep;
    private GameController _controller;
    private GameData _data;
    private DebugLevelSolver _solver;
    private IEnumerator _search;
    private List<DebugLevelSolver.Step> _solution;
    private int _stepIndex, _playedSteps;
    private string _signature, _status = "Ready. Enter Play Mode and open a level.";
    private double _stableSince, _nextAt;
    private Vector2 _scrollPosition;

    [MenuItem("Tools/LoopMaster/Debug Level")]
    public static void Open() => GetWindow<DebugLevelWindow>("Debug Level").Show();

    private void OnEnable()
    {
        minSize = new Vector2(380f, 520f);
        _autoPlay = _oneStep = false;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        StopPlayback();
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

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

    private void OnGUI()
    {
        GameController controller = Controller;
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EditorGUILayout.HelpBox("Auto Play solves the board, clicks boxes and waits for the conveyor/animations. The game shows Win only after all boxes are completed and the road is empty. Wins save normal level progress.", MessageType.Info);
        EditorGUILayout.LabelField("Current level", controller == null ? "Waiting for gameplay" : controller.CurrentLevelNumber.ToString());
        EditorGUILayout.LabelField("State", controller == null ? "-" : controller.EndState.ToString());
        EditorGUILayout.LabelField("Played steps", _playedSteps.ToString());

        DrawResultChecks(controller);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!IsRunning || controller == null ||
            controller.EndState != GameEndState.Playing || _autoPlay || _oneStep))
        {
            if (GUILayout.Button("Play One Step", GUILayout.Height(30f)))
            {
                _oneStep = true;
                ResetStability();
            }
        }
        using (new EditorGUI.DisabledScope(!IsRunning || controller == null || !controller.CanLoadNextLevel))
        {
            if (GUILayout.Button("Next Level")) controller.TryLoadNextLevel();
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            bool autoPlay = EditorGUILayout.Toggle("Auto Play", _autoPlay);
            if (autoPlay != _autoPlay)
            {
                if (!autoPlay) StopPlayback();
                else { _autoPlay = true; _oneStep = false; ResetStability(); }
            }
            _autoNext = EditorGUILayout.Toggle("Auto Next Level", _autoNext);
            _stepDelay = EditorGUILayout.Slider("Step delay (seconds)", _stepDelay, 0.1f, 3f);
            _nextDelay = EditorGUILayout.Slider("Next delay (seconds)", _nextDelay, 0.1f, 10f);
            _maxStates = EditorGUILayout.IntSlider("Max search states", _maxStates, 1000, 50000);
        }
        EditorGUILayout.HelpBox(_status, MessageType.None);
        if ((_autoPlay || _oneStep) && GUILayout.Button("Stop Auto Play"))
        {
            StopPlayback();
            _status = "Stopped. Any move already launched finishes normally.";
        }
        EditorGUILayout.HelpBox("Keep this window open. Pause suspends playback. Closing it or leaving Play Mode stops playback. Manual moves cause the remaining solution to be recalculated.", MessageType.None);
        EditorGUILayout.EndScrollView();
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
                if (controller.EndState != GameEndState.Playing) StopPlayback();
                _status = "Win/Lose checked. State: " + controller.EndState + ".";
                Debug.Log("[Debug Level] " + _status, controller);
            }
            EditorGUILayout.HelpBox("Force Win/Lose runs the normal result flow and opens its dialog. Force Win saves level progress; Force Lose increments the fail count. Auto Play stops when forcing a result.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Force Win")) ForceResult(controller, GameEndState.Won);
                if (GUILayout.Button("Force Lose")) ForceResult(controller, GameEndState.Failed);
            }
        }
        using (new EditorGUI.DisabledScope(!IsRunning || !controller.CanReplayLevel))
        {
            if (GUILayout.Button("Replay Level"))
            {
                StopPlayback();
                _status = controller.TryReplayLevel() ? "Level replayed. Ready to test again." : "Could not replay this level.";
            }
        }
    }

    private void ForceResult(GameController controller, GameEndState state)
    {
        StopPlayback();
        _status = controller.TryDebugForceEndState(state) ?
            "Forced " + state + " through the normal result flow." : "Could not force the result. Wait for gameplay to be active.";
        Debug.Log("[Debug Level] " + _status, controller);
    }

    private void OnInspectorUpdate() => Repaint();

    private void Tick()
    {
        if (!_autoPlay && !_oneStep) return;
        GameController controller = Controller;
        if (!IsRunning || controller == null)
        {
            ResetStability();
            return;
        }
        if (_controller != controller || _data != controller.Data)
        {
            _controller = controller;
            _data = controller.Data;
            _playedSteps = 0;
            ClearPlan();
            ResetStability();
            _nextAt = 0d;
        }
        if (controller.EndState == GameEndState.Failed)
        {
            StopWithMessage("Level failed. Replay before starting Auto Play again.");
            return;
        }
        if (controller.EndState == GameEndState.Won)
        {
            _status = "Level completed through gameplay (" + _playedSteps + " clicks).";
            if (!_autoPlay || !_autoNext) { StopPlayback(); return; }
            if (_nextAt == 0d) _nextAt = EditorApplication.timeSinceStartup + _nextDelay;
            if (EditorApplication.timeSinceStartup < _nextAt) return;
            if (!controller.CanLoadNextLevel || !controller.TryLoadNextLevel())
                StopWithMessage("All available levels completed; Auto Play stopped.");
            return;
        }
        if (!controller.IsDebugAutoPlayReady)
        {
            _status = "Waiting for box/jump/unlock animations...";
            ResetStability();
            return;
        }
        string signature = DebugLevelSolver.Signature(controller.Data);
        if (_signature != signature)
        {
            _signature = signature;
            _stableSince = EditorApplication.timeSinceStartup;
            if (_search != null) ClearPlan();
        }
        double settleDelay = _stepDelay + (controller.Data.TransferSlice.Count > 0 ?
            controller.DebugConveyorLoopDuration * 2f + 0.6f : 0f);
        if (EditorApplication.timeSinceStartup - _stableSince < settleDelay)
        {
            _status = "Waiting for the conveyor to settle...";
            return;
        }

        if (_solution != null && (_stepIndex >= _solution.Count ||
            _solution[_stepIndex].Before != signature)) ClearPlan();
        if (_solution == null)
        {
            if (_search == null)
            {
                _solver = new DebugLevelSolver(controller.Data, _maxStates);
                _search = _solver.Search();
            }
            var budget = Stopwatch.StartNew();
            while (budget.ElapsedMilliseconds < 8)
            {
                if (_search.MoveNext()) continue;
                _search = null;
                _solution = _solver.Solution;
                _stepIndex = 0;
                if (_solution == null || _solution.Count == 0)
                {
                    StopWithMessage(_solver.Failure ?? "No playable step remains; waiting for the game's result.");
                    return;
                }
                break;
            }
            if (_solution == null)
            {
                _status = "Finding a solution... " + _solver.Explored + " states explored.";
                return;
            }
        }
        DebugLevelSolver.Step step = _solution[_stepIndex];
        if (!controller.TryDebugPlayBox(step.BoxIndex))
        {
            StopWithMessage("Box " + (step.BoxIndex + 1) + " could not be clicked. Wait for animations or replay.");
            return;
        }
        _stepIndex++;
        _playedSteps++;
        _status = "Playing step " + _playedSteps + ": click box " + (step.BoxIndex + 1) + ".";
        Debug.Log("[Debug Level] " + _status, controller);
        ResetStability();
        if (_oneStep) { _oneStep = false; _status += " Click Play One Step to continue."; }
    }

    private void StopWithMessage(string message)
    {
        StopPlayback();
        _status = message;
        Debug.Log("[Debug Level] " + message);
    }

    private void ClearPlan()
    {
        (_search as System.IDisposable)?.Dispose();
        _search = null;
        _solution = null;
        _stepIndex = 0;
    }

    private void ResetStability()
    {
        _signature = null;
        _stableSince = EditorApplication.timeSinceStartup;
    }

    private void StopPlayback()
    {
        _autoPlay = _oneStep = false;
        ClearPlan();
        ResetStability();
        _nextAt = 0d;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        StopPlayback();
        _controller = null;
        _data = null;
        _playedSteps = 0;
        _status = "Ready. Enter Play Mode and open a level.";
    }
}
#endif
