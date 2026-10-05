#if UNITY_EDITOR
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class LoopLevelPlaytest
{
    internal const string ScenePath = "Assets/00Game/00Scene/GameScene.unity";
    private const string Prefix = "LoopMaster.LevelPlaytest.";
    private const string PendingKey = Prefix + "Json";
    private static GameData _requestedData;
    private static JObject _requestedRoot;
    private static double _deadline;
    private static bool _waiting;
    internal static string Status => SessionState.GetString(Prefix + "Status", "");
    internal static bool Busy => _waiting || !string.IsNullOrEmpty(SessionState.GetString(PendingKey, ""));

    static LoopLevelPlaytest()
    {
        GameController.EditorPlaytestSource = TakePendingDraft;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Update;
        if (!string.IsNullOrEmpty(SessionState.GetString(PendingKey, ""))) BeginWaiting();
    }

    internal static bool Play(JObject root, out string error)
    {
        error = null;
        if (Busy || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
        { error = "Unity is busy. Wait for the current Play Mode transition."; return false; }
        var errors = LoopLevelAuthoring.Validate(root, out _);
        if (errors.Count > 0) { error = string.Join("\n", errors); return false; }
        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (scene == null) { error = "Gameplay scene not found: " + ScenePath; return false; }
        // SessionState survives assembly reloads; neither the draft nor its file is replaced.
        SessionState.SetString(PendingKey, root.ToString(Formatting.None));
        SetStatus("Opening Game View with the preview level…");
        BeginWaiting();
        try
        {
            if (EditorApplication.isPlaying)
            {
                if (GameController.Current != null && GameController.Current.Data != null)
                    GameController.Current.InitializeEditorPlaytest(TakePendingDraft());
                else EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            }
            else
            {
                SessionState.SetString(Prefix + "PreviousScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool(Prefix + "RestoreScene", true);
                EditorSceneManager.playModeStartScene = scene;
                EditorApplication.isPlaying = true;
            }
            FocusGameView();
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message; Fail(error);
            if (!EditorApplication.isPlayingOrWillChangePlaymode) RestoreStartScene();
            return false;
        }
    }

    private static GameData TakePendingDraft()
    {
        string json = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(json)) return null;
        SessionState.EraseString(PendingKey);
        try { _requestedRoot = LoopLevelAuthoring.Parse(json); return _requestedData = GameLevelDataConverter.FromJson(json); }
        catch (Exception exception) { Fail(exception.Message); return null; }
    }
    private static void BeginWaiting() { _waiting = true; _deadline = EditorApplication.timeSinceStartup + 60; }
    private static void Update()
    {
        if (!_waiting) return;
        if (EditorApplication.isPlaying && _requestedData != null && GameController.Current != null &&
            GameController.Current.Data == _requestedData)
        {
            GameController.Current.UpdateCamera();
            _waiting = false; _requestedData = null;
            Time.timeScale = 1;
            SetStatus("Preview level is running in Game View. Stop Play Mode to return to editing.");
            FocusGameView();
        }
        else if (EditorApplication.timeSinceStartup > _deadline)
            Fail("The gameplay controller did not load the preview. Check the Unity Console.");
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            if (_waiting) SetStatus("Playtest cancelled.");
            else if (state == PlayModeStateChange.EnteredEditMode && Status.StartsWith("Preview level is running", StringComparison.Ordinal))
                SetStatus("Playtest stopped. The editor draft and generated preview are ready.");
            _waiting = false; _requestedData = null; SessionState.EraseString(PendingKey);
            RestoreStartScene();
        }
        else if (state == PlayModeStateChange.EnteredPlayMode && Busy) BeginWaiting();
    }
    private static void RestoreStartScene()
    {
        if (!SessionState.GetBool(Prefix + "RestoreScene", false)) return;
        // Avoid replacing a start-scene preference changed by another tool during the test.
        if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == ScenePath)
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Prefix + "PreviousScene", ""));
        SessionState.EraseBool(Prefix + "RestoreScene"); SessionState.EraseString(Prefix + "PreviousScene");
    }
    private static void Fail(string error)
    {
        _waiting = false; _requestedData = null; SessionState.EraseString(PendingKey);
        SetStatus("Playtest failed: " + error);
        Debug.LogError(Status);
    }
    private static void SetStatus(string status)
    {
        SessionState.SetString(Prefix + "Status", status);
        foreach (var window in Resources.FindObjectsOfTypeAll<LoopLevelEditorWindow>()) window.Repaint();
    }
    private static void FocusGameView()
    {
        Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameView == null) return;
        EditorWindow window = EditorWindow.GetWindow(gameView);
        window.Show(); window.Focus();
    }
}
#endif
