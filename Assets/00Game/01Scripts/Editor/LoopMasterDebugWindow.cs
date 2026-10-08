#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>One entry point for gameplay and event debugging.</summary>
public sealed class LoopMasterDebugWindow : EditorWindow
{
    public enum Tab { Level, SpecialStreak, StreakLine, Lava }

    private static readonly string[] TabLabels = { "Level", "SpecialStreak", "StreakLine", "Lava" };
    [SerializeField] private Tab _tab;
    [SerializeField] private DebugLevelPanel _level = new DebugLevelPanel();
    [SerializeField] private DebugSpecialStreakPanel _specialStreak = new DebugSpecialStreakPanel();
    private Vector2 _eventScroll;

    [MenuItem("Tools/LoopMaster/Debug")]
    public static void Open() => GetWindow<LoopMasterDebugWindow>("LoopMaster Debug").Show();

    public static void Open(Tab tab)
    {
        var window = GetWindow<LoopMasterDebugWindow>("LoopMaster Debug");
        window._tab = tab;
        window.Show();
    }

    private static bool Running => EditorApplication.isPlaying && !EditorApplication.isPaused &&
        !EditorApplication.isCompiling && !EditorApplication.isUpdating && Time.timeScale > 0f;

    private void OnEnable()
    {
        minSize = new Vector2(440f, 560f);
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    private void OnInspectorUpdate() => Repaint();
    private void OnPlayModeChanged(PlayModeStateChange state) => _level.ResetStatus();

    private void OnGUI()
    {
        _tab = (Tab)GUILayout.Toolbar((int)_tab, TabLabels);
        EditorGUILayout.Space();
        switch (_tab)
        {
            case Tab.Level: _level.DrawGUI(); break;
            case Tab.SpecialStreak: _specialStreak.DrawGUI(); break;
            default:
                _eventScroll = EditorGUILayout.BeginScrollView(_eventScroll);
                EditorGUILayout.HelpBox("Event actions use the current save and persist progress. Use Level to test wins and losses through the gameplay flow.", MessageType.Info);
                if (_tab == Tab.StreakLine) DrawStreakLine();
                else DrawLava();
                EditorGUILayout.EndScrollView();
                break;
        }
    }

    private static void DrawStreakLine()
    {
        var manager = EditorApplication.isPlaying ? Managers.StreakLine : null;
        if (manager == null)
        {
            EditorGUILayout.HelpBox("Waiting for StreakLine to initialize in Play Mode.", MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField("State", manager.Data == null ? "Uninitialized" : manager.Data.State.ToString());
        EditorGUILayout.LabelField("Unlock level", manager.GetUnlockLevel().ToString());
        EditorGUILayout.LabelField("Progress / goal", manager.GetCurrLevel() + " / " + manager.GetFinalGoal());
        EditorGUILayout.LabelField("Pending levels / view", manager.GetTempLevel() + " / " + manager.GetTempViewLevel());
        EditorGUILayout.LabelField("Highest level", manager.GetMaxReachLevel().ToString());
        EditorGUILayout.LabelField("Claimable chests", manager.GetClaimableChestCount().ToString());
        EditorGUILayout.LabelField("Remaining seconds", manager.GetRemainingTime().ToString());
        using (new EditorGUI.DisabledScope(!Running))
        {
            if (GUILayout.Button("Check Event State")) manager.CheckEventState();
            using (new EditorGUI.DisabledScope(manager.IsLock || !manager.CanStartEvent()))
                if (GUILayout.Button("Start Event")) manager.StartEvent();
        }
        EditorGUILayout.HelpBox("Start follows the event's weekly schedule. Pending progress settles through the normal Home/Game flow.", MessageType.None);
        if (GUILayout.Button("Test Win / Lose")) Open(Tab.Level);
    }

    private static void DrawLava()
    {
        var manager = EditorApplication.isPlaying ? Managers.Lava : null;
        if (manager == null)
        {
            EditorGUILayout.HelpBox("Waiting for Lava to initialize in Play Mode.", MessageType.Info);
            return;
        }
        string state = !manager.IsUnlocked() ? "Locked" : manager.IsWin() ? "Win" :
            manager.IsInCd() ? "Cooldown" : manager.IsFail() ? "Fail" : manager.IsExpired() ? "Expired" :
            manager.IsOngoing() ? "Ongoing" : manager.IsWaitingStart() ? "WaitingStart" : "Closed";
        EditorGUILayout.LabelField("State", state);
        EditorGUILayout.LabelField("Unlock level", manager.GetUnlockLevel().ToString());
        EditorGUILayout.LabelField("Progress / goal", manager.GetRecord() + " / " + manager.GetLevelGoal());
        EditorGUILayout.LabelField("Players remaining", manager.GetRemainingPlayers().ToString());
        EditorGUILayout.LabelField("Pending player animation", manager.ShouldPlayJumpAnim() ? "Yes" : "No");
        EditorGUILayout.LabelField("Pending reward", manager.ShouldClaimRewards() ? "Yes" : "No");
        EditorGUILayout.LabelField("Remaining seconds", manager.GetRemainTime().ToString());
        EditorGUILayout.LabelField("Retry seconds", manager.GetRemainRetryTime().ToString());
        using (new EditorGUI.DisabledScope(!Running))
        {
            if (GUILayout.Button("Check Event State")) manager.CheckEventState();
            using (new EditorGUI.DisabledScope(!manager.IsUnlocked() || !manager.IsWaitingStart()))
                if (GUILayout.Button("Start Event")) manager.StartEvent();
        }
        if (GUILayout.Button("Test Win / Lose")) Open(Tab.Level);
    }
}
#endif
