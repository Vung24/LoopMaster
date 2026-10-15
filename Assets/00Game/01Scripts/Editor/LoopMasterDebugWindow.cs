#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public sealed class LoopMasterDebugWindow : EditorWindow
{
    public enum Tab { Level, SpecialStreak, StreakLine, Lava }

    private static readonly string[] TabLabels = { "Level", "SpecialStreak", "StreakLine", "Lava" };
    [SerializeField] private Tab _tab;
    [SerializeField] private DebugLevelPanel _level = new DebugLevelPanel();
    [SerializeField] private DebugSpecialStreakPanel _specialStreak = new DebugSpecialStreakPanel();
    private Vector2 _eventScroll;
    private bool _streakRewardsExpanded;
    private bool _streakPresenting;
    private string _streakStatus;

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
        DrawForceWinLose();
        EditorGUILayout.Space(2f);
        _tab = (Tab)GUILayout.Toolbar((int)_tab, TabLabels);
        EditorGUILayout.Space();
        switch (_tab)
        {
            case Tab.Level: _level.DrawGUI(); break;
            case Tab.SpecialStreak: _specialStreak.DrawGUI(); break;
            default:
                _eventScroll = EditorGUILayout.BeginScrollView(_eventScroll);
                EditorGUILayout.HelpBox("Event actions use the current save and persist progress.", MessageType.Info);
                if (_tab == Tab.StreakLine) DrawStreakLine();
                else DrawLava();
                EditorGUILayout.EndScrollView();
                break;
        }
    }

    private void DrawForceWinLose()
    {
        var controller = EditorApplication.isPlaying ? GameController.Current : null;
        bool canForce = Running && controller != null && controller.IsGameplayActive;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canForce))
                {
                    var prevColor = GUI.backgroundColor;
                    GUI.backgroundColor = canForce ? new Color(0.45f, 0.9f, 0.45f) : Color.white;
                    if (GUILayout.Button("Force Win", GUILayout.Height(28f)))
                    {
                        ForceResult(controller, GameEndState.Won);
                    }

                    GUI.backgroundColor = canForce ? new Color(0.95f, 0.45f, 0.45f) : Color.white;
                    if (GUILayout.Button("Force Lose", GUILayout.Height(28f)))
                    {
                        ForceResult(controller, GameEndState.Failed);
                    }
                    GUI.backgroundColor = prevColor;
                }
            }

            if (!Running)
            {
                EditorGUILayout.LabelField("Enter Play Mode to test gameplay.", EditorStyles.miniLabel);
            }
            else if (controller == null || !controller.IsGameplayActive)
            {
                EditorGUILayout.LabelField("Gameplay inactive (open a level to test Win / Lose).", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"Active Level: {controller.CurrentLevelNumber} | State: {controller.EndState}", EditorStyles.miniLabel);
            }
        }
    }

    private void ForceResult(GameController controller, GameEndState state)
    {
        if (controller == null) return;
        _level.ForceResult(controller, state);
        var specialStreakManager = SpecialStreakManager.Current;
        if (specialStreakManager != null)
        {
            _specialStreak.NotifyResult(controller, specialStreakManager, state == GameEndState.Won);
        }
    }

    private void DrawStreakLine()
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
        var parent = Snui.Dialog.DefaultParent ?? SnuiManager.GetCurrentUiCanvas()?.transform;
        var dialog = parent == null ? null : parent.GetComponentInChildren<Snui.StreakLine.StreakLineDialog>();
        bool busy = _streakPresenting || (dialog != null && !dialog.CanPlayPendingProgress);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Test progress", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!Running || busy))
        {
            using (new EditorGUI.DisabledScope(manager.IsOngoing && !manager.IsExpired))
                if (GUILayout.Button("Activate For Debug (24 hours)"))
                {
                    manager.Data.State = Snui.EventState.Ongoing;
                    manager.Data.EndTime = TimeUtils.GetUnixTimestamp() + 86400;
                    manager.SaveData();
                    _streakStatus = "Event activated; existing progress and claimed chests kept.";
                }
            using (new EditorGUI.DisabledScope(!manager.IsOngoing || manager.IsExpired ||
                manager.GetCurrLevel() + manager.GetTempLevel() >= manager.GetFinalGoal()))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+1 Level")) AdvanceStreakLine(manager, 1, parent, dialog);
                    if (GUILayout.Button("+5 Levels")) AdvanceStreakLine(manager, 5, parent, dialog);
                }
                if (GUILayout.Button("Advance To Next Chest"))
                {
                    int current = manager.GetCurrLevel() + manager.GetTempLevel();
                    foreach (var chest in manager.GetChests())
                        if (chest != null && chest.Level > current)
                        {
                            AdvanceStreakLine(manager, chest.Level - current, parent, dialog);
                            break;
                        }
                }
            }
            using (new EditorGUI.DisabledScope(parent == null || !manager.IsOngoing || manager.IsExpired))
                if (GUILayout.Button("Open StreakLine / Play Pending Rise"))
                    PresentStreakLine(parent, dialog).Forget();
            if (GUILayout.Button("Check Event State")) manager.CheckEventState();
            using (new EditorGUI.DisabledScope(manager.IsLock || !manager.CanStartEvent()))
                if (GUILayout.Button("Start Event")) manager.StartEvent();
        }
        EditorGUILayout.HelpBox("Increase uses StreakLine's win logic, settles pending levels and plays the rise in its popup. Activate bypasses the weekly schedule for debugging.", MessageType.None);
        if (!string.IsNullOrEmpty(_streakStatus)) EditorGUILayout.HelpBox(_streakStatus, MessageType.None);
        _streakRewardsExpanded = EditorGUILayout.Foldout(_streakRewardsExpanded, "Reward milestones", true);
        if (_streakRewardsExpanded) DebugEventRewards.DrawStreakLine(manager);
    }

    private void AdvanceStreakLine(Snui.StreakLine.BaseStreakLineManager manager, int levels,
        Transform parent, Snui.StreakLine.StreakLineDialog dialog)
    {
        int before = manager.GetCurrLevel() + manager.GetTempLevel();
        int count = Mathf.Min(levels, manager.GetFinalGoal() - before);
        for (int i = 0; i < count; i++) manager.OnGameWon();
        manager.SynchronizeTempLevel();
        _streakStatus = "Progress: " + before + " -> " + manager.GetCurrLevel() + ".";
        if (parent != null) PresentStreakLine(parent, dialog).Forget();
    }

    private async UniTask PresentStreakLine(Transform parent, Snui.StreakLine.StreakLineDialog dialog)
    {
        _streakPresenting = true;
        try
        {
            Managers.StreakLine.SynchronizeTempLevel();
            if (dialog != null) await dialog.PlayPendingProgress();
            else await Snui.StreakLine.StreakLineDialog.Show(parent);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { _streakStatus = exception.Message; Debug.LogException(exception); }
        finally { _streakPresenting = false; }
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
        DebugEventRewards.DrawLava(manager);
        using (new EditorGUI.DisabledScope(!Running))
        {
            if (GUILayout.Button("Check Event State")) manager.CheckEventState();
            using (new EditorGUI.DisabledScope(!manager.IsUnlocked() || !manager.IsWaitingStart()))
                if (GUILayout.Button("Start Event")) manager.StartEvent();
        }
    }
}

internal static class DebugEventRewards
{
    private static HashSet<string> _receipts;

    private static void Header(string note)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Reward milestones", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(note + " Receipt = reward transaction saved to inventory. Updates automatically.", MessageType.None);
        if (!InventoryManager.TryDebugReadRewardReceipts(out _receipts, out string error));
    }

    internal static void DrawStreakLine(Snui.StreakLine.BaseStreakLineManager manager)
    {
        Header("Claimed chests belong to this event cycle. Pending wins must settle before claiming.");
        EditorGUILayout.LabelField("Cycle end", manager.EndTime.ToString());
        var chests = manager.GetChests();
        if (chests == null) return;
        for (int i = 0; i < chests.Count; i++)
        {
            var chest = chests[i];
            if (chest == null) continue;
            bool claimed = manager.IsChestClaimed(chest.Level);
            string status = claimed ? "Claimed" : manager.CanClaimChest(chest.Level) ? "Ready to claim" :
                manager.IsLock ? "Locked" : manager.IsExpired || !manager.IsOngoing ? "Unavailable" :
                (long)manager.GetCurrLevel() + manager.GetTempLevel() >= chest.Level ? "Pending wins" : "Not reached";
            DrawRow(i + 1, chest.Level, chest.Items(), status, claimed,
                "StreakLine/" + manager.EndTime + "/" + chest.Level);
        }
    }

    internal static void DrawLava(Snui.Lava.BaseLavaManager manager)
    {
        Header("Lava has one final reward per run. Coins are shared between the surviving players.");
        bool claimed = manager.DebugHasClaimedRewards;
        string status = claimed ? "Claimed" : manager.ShouldClaimRewards() ? "Ready to claim" :
            !manager.IsUnlocked() ? "Locked" : manager.IsFail() || manager.IsExpired() ? "Unavailable" : "Not reached";
        string id = manager.DebugRewardClaimId;
        DrawRow(1, manager.GetLevelGoal(), new[] { RewardItem.Coin(manager.GetSharedCoinRewardCount()) },
            status, claimed, string.IsNullOrEmpty(id) ? null : "lava:" + id);
        EditorGUILayout.HelpBox("Coin amount is the current run's calculated share. A new run resets its milestone status.", MessageType.None);
    }

    private static void DrawRow(int number, int goal, IReadOnlyList<RewardItem> rewards,
        string status, bool claimed, string claimId)
    {
        bool? receipt = _receipts == null ? (bool?)null :
            !string.IsNullOrEmpty(claimId) && _receipts.Contains(claimId);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Milestone " + number + " | Goal " + goal, status, EditorStyles.boldLabel);
            var description = new StringBuilder();
            if (rewards != null)
                foreach (var item in rewards)
                {
                    if (item == null) continue;
                    if (description.Length > 0) description.Append(", ");
                    description.Append(item.Type?.Key ?? "unknown").Append(" x").Append(item.Count);
                    if (item.Type == RewardType.InfinityHeart) description.Append(" seconds");
                }
            EditorGUILayout.LabelField(description.Length == 0 ? "No rewards configured" : description.ToString(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Inventory receipt", receipt == null ? "Cannot verify" : receipt.Value ? "Saved" : "Not saved");
            if (!string.IsNullOrEmpty(claimId))
                EditorGUILayout.SelectableLabel(claimId, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (receipt == true && !claimed)
                EditorGUILayout.HelpBox("Reward granted; event claim state has not synchronized yet.", MessageType.Warning);
            else if (receipt == false && claimed)
                EditorGUILayout.HelpBox("Event marks this reward claimed, but its inventory receipt is missing.", MessageType.Warning);
        }
    }
}
#endif
