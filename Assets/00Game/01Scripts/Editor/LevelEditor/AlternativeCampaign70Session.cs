#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
internal static class AlternativeCampaign70Session
{
    private const string Pending = "LoopMaster.AlternativeCampaign70.Pending";
    private const string PreviousScene = "LoopMaster.AlternativeCampaign70.PreviousScene";
    private const string RestoreScene = "LoopMaster.AlternativeCampaign70.RestoreScene";
    private const string Running = "LoopMaster.AlternativeCampaign70.Running";
    private static bool _ownsSession;
    private static bool _previousSandbox;
    private static bool _previousIsolated;

    static AlternativeCampaign70Session()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (EditorApplication.isPlaying && SessionState.GetBool(Running, false)) InstallSource();
    }

    [MenuItem("Tools/LoopMaster/Alternative Campaign 70/Start campaign from level 1")]
    internal static void StartCampaign()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play Mode before starting a fresh campaign session.");
        if (!File.Exists(Path.Combine(AlternativeCampaign70Builder.LevelFolder, "Level 1.json")))
            throw new FileNotFoundException("Build the campaign samples before starting a session.");
        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LoopLevelPlaytest.ScenePath);
        if (scene == null) throw new FileNotFoundException(LoopLevelPlaytest.ScenePath);
        SessionState.SetString(PreviousScene, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(RestoreScene, true);
        EditorSceneManager.playModeStartScene = scene;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void InitializeSession()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        _previousSandbox = GamePrefs.EditorSandbox;
        _previousIsolated = GamePrefs.EditorIsolatedProfile;
        SessionState.SetBool(Pending + ".Sandbox", _previousSandbox);
        SessionState.SetBool(Pending + ".Isolated", _previousIsolated);
        SessionState.SetBool(Running, true);
        _ownsSession = true;
        GamePrefs.EditorSandbox = true;
        GamePrefs.EditorIsolatedProfile = true;
        GamePrefs.ResetEditorSandbox();
        InstallSource();
        GameProgressStore.SetProgress(1);
        GamePrefs.SetInt(GamePrefKeys.Undo, 3);
        GamePrefs.SetInt(GamePrefKeys.Shuffle, 3);
        GamePrefs.SetInt(GamePrefKeys.Path, 3);
        GamePrefs.SetInt(GamePrefKeys.Coins, 1000);
        GamePrefs.SetInt(GamePrefKeys.SkipTutorial, 0);
        GameData.ClearCurrent();
    }

    private static void InstallSource()
    {
        GamePrefs.EditorSandbox = true;
        GamePrefs.EditorIsolatedProfile = true;
        GamePrefs.EditorCampaignLastLevel = 70;
        GamePrefs.EditorCampaignLoader = Load;
        GamePrefs.EditorCampaignAvailability = HasLevel;
    }

    internal static bool HasLevel(int level)
    {
        return level >= 1 && level <= 70 && File.Exists(Path.Combine(AlternativeCampaign70Builder.LevelFolder, "Level " + level + ".json"));
    }

    private static GameData Load(int level)
    {
        if (!HasLevel(level))
        {
            Debug.LogError("Alternative campaign: missing level " + level + ". No fallback is allowed.");
            return null;
        }
        try
        {
            string json = File.ReadAllText(Path.Combine(AlternativeCampaign70Builder.LevelFolder, "Level " + level + ".json"));
            ExportedPuzzleLevel exported = ExportedPuzzleJsonDecoder.DecodeLevel(json);
            if (exported.LevelNumber != level || exported.PuzzleId != level)
                throw new FormatException("Filename, level_number and puzzle_id must match.");
            return GameLevelDataConverter.FromJson(json, level);
        }
        catch (Exception error)
        {
            Debug.LogError("Alternative campaign: cannot load level " + level + ": " + error.Message);
            return null;
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (_ownsSession || SessionState.GetBool(Running, false))
        {
            GamePrefs.EditorCampaignLoader = null;
            GamePrefs.EditorCampaignAvailability = null;
            GamePrefs.EditorCampaignLastLevel = 0;
            GamePrefs.ResetEditorSandbox();
            GamePrefs.EditorIsolatedProfile = SessionState.GetBool(Pending + ".Isolated", false);
            GamePrefs.EditorSandbox = SessionState.GetBool(Pending + ".Sandbox", false);
            GameData.ClearCurrent();
            _ownsSession = false;
        }
        SessionState.EraseBool(Pending);
        SessionState.EraseBool(Running);
        if (!SessionState.GetBool(RestoreScene, false)) return;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousScene, ""));
        SessionState.EraseBool(RestoreScene);
        SessionState.EraseString(PreviousScene);
    }
}
#endif
