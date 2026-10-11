using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SpecialStreakManager : MonoBehaviour
{
    public const string DATA_PATH = GamePrefKeys.SpecialStreak;
    private static readonly int[] Multipliers = { 1, 5, 10, 25, 100 };
    public static SpecialStreakManager Current { get; private set; }
    public event Action Changed;
    private SpecialStreakData _data;
    private List<SpecialStreakRewardInfo> _rewardInfoConfigs;
    private bool _invalid, _settling;
    public string LastError { get; private set; }
    public bool IsLocked => _data == null || !_data.Activated;
    public bool IsOngoing => !IsLocked && !IsCompleted() && !_invalid;
    public bool IsClosed => IsCompleted();
    public int CurrCount => _data?.CurrCount ?? 0;
    public int TempCount => _data?.TempCount ?? 0;
    public int ClaimedIndex => _data?.ClaimedIndex ?? 0;
    public int MultiplierIndex => _data?.MultiplierIndex ?? 0;
    public int LastMultiplierIndex => _data?.LastMultiplierIndex ?? 0;
    public bool IsTutorialFinished
    {
        get => _data?.TutorialFinished ?? false;
        set { if (_data != null) { _data.TutorialFinished = value; SaveData(); } }
    }
    public bool PendingPopDialog
    {
        get => _data?.PendingPopDialog ?? false;
        set { if (_data != null) { _data.PendingPopDialog = value; SaveData(); } }
    }
    [Serializable] private sealed class Config { public SpecialStreakRewardInfo[] Items; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeBeforeScene() => Ensure();
    public static SpecialStreakManager Ensure()
    {
        if (Current == null) new GameObject("OfflineSpecialStreak").AddComponent<SpecialStreakManager>();
        Current.CheckStartEvent();
        return Current;
    }
    private void Awake()
    {
        if (Current != null && Current != this) { Destroy(this); return; }
        Current = this;
        DontDestroyOnLoad(gameObject);
        Startup();
    }
    public void Startup()
    {
        try
        {
            var asset = Resources.Load<TextAsset>("configjson/SpecialStreakConfig");
            if (asset == null) throw new InvalidOperationException("SpecialStreakConfig is missing.");
            var config = JsonUtility.FromJson<Config>("{\"Items\":" + asset.text + "}");
            if (config?.Items == null || config.Items.Length == 0) throw new InvalidOperationException("Invalid SpecialStreak config.");
            _rewardInfoConfigs = new List<SpecialStreakRewardInfo>(config.Items);
            foreach (var stage in _rewardInfoConfigs)
            {
                if (stage == null || stage.Goal <= 0 || stage.Rewards == null || stage.Rewards.Count == 0)
                    throw new InvalidOperationException("Invalid SpecialStreak reward stage.");
                foreach (var reward in stage.Rewards)
                    if (reward == null || reward.count < 0 || RewardType.FromString(reward.type) == RewardType.Unknown)
                        throw new InvalidOperationException("Invalid SpecialStreak reward.");
            }
            _data = GamePrefs.HasKey(DATA_PATH) ? JsonUtility.FromJson<SpecialStreakData>(GamePrefs.GetString(DATA_PATH)) : new SpecialStreakData();
            if (_data == null || _data.Version != 1 || _data.CurrCount < 0 || _data.TempCount < 0 ||
                _data.ClaimedIndex < 0 || _data.ClaimedIndex > _rewardInfoConfigs.Count ||
                _data.MultiplierIndex < 0 || _data.MultiplierIndex >= Multipliers.Length ||
                _data.LastMultiplierIndex < 0 || _data.LastMultiplierIndex >= Multipliers.Length ||
                _data.TargetCount < 0 || _data.TargetMultiplier < 0 || _data.TargetMultiplier >= Multipliers.Length)
                throw new InvalidOperationException("Invalid saved SpecialStreak data; original retained.");
            _invalid = false;
            CheckStartEvent();
        }
        catch (Exception error) { _invalid = true; LastError = error.Message; Debug.LogWarning(LastError); }
    }
    private void CheckStartEvent()
    {
        if (_invalid || _data == null || _data.Activated || GameProgressStore.HighestUnlockedLevel < GetUnlockLevel()) return;
        _data.Activated = true;
        _data.LastWonLevel = GameProgressStore.HighestCompletedLevel;
        SaveData();
    }
    public int GetUnlockLevel() => 21;
    public int GetPreviewLevel() => 1;
    public bool IsExpired() => false;
    public bool IsCompleted() => IsCompleted(ClaimedIndex);
    public bool IsCompleted(int index) => _rewardInfoConfigs != null && index >= _rewardInfoConfigs.Count;
    public IReadOnlyList<SpecialStreakRewardInfo> GetRewardInfosConfig() => _rewardInfoConfigs?.AsReadOnly();
    public IReadOnlyList<int> GetMultipliers() => Array.AsReadOnly(Multipliers);
    public int GetMultiplier(int index) => Multipliers[Mathf.Clamp(index, 0, Multipliers.Length - 1)];
    public int GetCurrentMultiplier() => GetMultiplier(MultiplierIndex);
    public int GetNextMultiplierIndex(int index) => Math.Min(index + 1, Multipliers.Length - 1);
    public bool IsMaxMultiplierIndex(int index) => index >= Multipliers.Length - 1;
    public int GetMultipliedCount(int count, int index) => checked(count * GetMultiplier(index));
    public SpecialStreakRewardInfo GetRewardInfo(int index) => _rewardInfoConfigs != null && index >= 0 && index < _rewardInfoConfigs.Count ? _rewardInfoConfigs[index] : null;
    public SpecialStreakRewardInfo GetCurrRewardInfo() => GetRewardInfo(ClaimedIndex);
    public int GetLastGoal()
    {
        if (_rewardInfoConfigs != null && _rewardInfoConfigs.Count > 0)
            return _rewardInfoConfigs[_rewardInfoConfigs.Count - 1].Goal;
        return 0;
    }
    public bool ShouldSettleCollectionResult() => !_invalid && (_data?.Settling == true || TempCount > 0 || (GetCurrRewardInfo() != null && CurrCount >= GetCurrRewardInfo().Goal));
    public bool ShouldShowLoseItem() => IsOngoing && !IsCompleted() && !IsExpired() && MultiplierIndex > 0;
    public bool ShouldShowFailBanner() => IsOngoing && !IsCompleted() && !IsExpired() && LastMultiplierIndex > 0;
    public bool ShouldShowTutorial() => IsOngoing && !IsTutorialFinished;
    public bool ShouldPopDialog() => IsOngoing && !IsExpired() && !IsCompleted() && PendingPopDialog;
    public void SetDialogPopped() => PendingPopDialog = false;
    public bool WillPendingCollectionClaimReward()
    {
        var curr = GetCurrRewardInfo();
        if (curr == null) return false;
        return curr.Goal <= CurrCount + GetMultiplier(MultiplierIndex) * TempCount;
    }
    public void SetNextMultiplierIndex()
    {
        if (_invalid || _data == null) return;
        int mult = GetMultiplier(MultiplierIndex);
        int nextMultIdx = MultiplierIndex;
        if (!IsExpired() && !IsCompleted(ClaimedIndex))
        {
            nextMultIdx = GetNextMultiplierIndex(MultiplierIndex);
        }
        CommitCollectionResult(checked(CurrCount + mult * TempCount), ClaimedIndex, nextMultIdx);
    }
    public void CommitCollectionResult(int currCount, int claimedIndex, int multiplierIndex)
    {
        if (_invalid || _data == null) return;
        _data.TempCount = 0;
        _data.CurrCount = IsCompleted(claimedIndex) ? 0 : currCount;
        _data.ClaimedIndex = claimedIndex;
        _data.LastWonLevel = GameProgressStore.HighestCompletedLevel;
        _data.LastMultiplierIndex = _data.MultiplierIndex;
        _data.MultiplierIndex = multiplierIndex;
        _data.Settling = false;
        SaveData();
        Changed?.Invoke();
    }
    public void SetTutorialShown() { if (_invalid || _data == null) return; _data.TutorialFinished = true; SaveData(); }
    public void OnGameWon(int level)
    {
        if (!IsOngoing || level <= _data.LastWonLevel || GameProgressStore.IsLevelCompleted(level) || _settling) return;
        _data.TempCount = checked(_data.TempCount + 1);
        _data.LastWonLevel = level;
        SaveData(); Changed?.Invoke();
    }
    public void OnGameFailed()
    {
        if (!IsOngoing || _settling) return;
        _data.LastMultiplierIndex = _data.MultiplierIndex;
        _data.MultiplierIndex = 0;
        SaveData(); Changed?.Invoke();
    }
    public static string ClaimId(int index) => "SpecialStreak/v1/" + index;
    public bool SettlePending()
    {
        if (_invalid || _settling || _data == null) return false;
        if (!ShouldSettleCollectionResult()) return true;
        _settling = true;
        try
        {
            if (!_data.Settling)
            {
                _data.TargetCount = checked(CurrCount + GetMultipliedCount(TempCount, MultiplierIndex));
                _data.TargetMultiplier = TempCount > 0 && !IsCompleted() ? GetNextMultiplierIndex(MultiplierIndex) : MultiplierIndex;
                _data.Settling = true;
                SaveData();
            }
            var inventory = InventoryManager.Ensure();
            if (!inventory.ReplayPendingRewardJournal()) return false;
            SpecialStreakRewardInfo stage;
            while ((stage = GetCurrRewardInfo()) != null && _data.TargetCount >= stage.Goal)
            {
                if (!inventory.TryClaimRewards(ClaimId(ClaimedIndex), stage.Items(), out _))
                { LastError = "COULD NOT ADD REWARD — TRY AGAIN"; return false; }
                _data.TargetCount -= stage.Goal;
                _data.ClaimedIndex++;
                SaveData();
            }
            _data.CurrCount = IsCompleted() ? 0 : _data.TargetCount;
            _data.TempCount = 0;
            _data.LastMultiplierIndex = _data.MultiplierIndex;
            if (!IsCompleted()) _data.MultiplierIndex = _data.TargetMultiplier;
            _data.Settling = false;
            SaveData(); LastError = null;
        }
        catch (Exception error) { LastError = error.Message; Debug.LogWarning("SpecialStreak: " + LastError); return false; }
        finally { _settling = false; }
        Changed?.Invoke();
        return true;
    }
#if UNITY_EDITOR
    public long DebugPendingCollectionCount => _data?.Settling == true ? _data.TargetCount :
        (long)CurrCount + (long)GetCurrentMultiplier() * TempCount;

    public bool TryDebugPrepare(int multiplierIndex, bool nearReward, bool showTutorial, bool pendingWin = false)
    {
        if (!Application.isPlaying || _invalid || _settling || _data == null ||
            _data.Settling || TempCount > 0 || IsCompleted()) return false;
        _data.Activated = true;
        _data.MultiplierIndex = Mathf.Clamp(multiplierIndex, 0, Multipliers.Length - 1);
        _data.LastMultiplierIndex = _data.MultiplierIndex;
        _data.CurrCount = nearReward ? Mathf.Max(0, GetCurrRewardInfo().Goal - GetCurrentMultiplier()) : 0;
        _data.TempCount = pendingWin ? 1 : 0;
        _data.TutorialFinished = !showTutorial;
        _data.PendingPopDialog = false;
        _data.LastWonLevel = GameProgressStore.HighestCompletedLevel;
        LastError = null;
        SaveData();
        Changed?.Invoke();
        return true;
    }
#endif
    private void SaveData() { GamePrefs.SetString(DATA_PATH, JsonUtility.ToJson(_data)); GamePrefs.Save(); }
    public static void ClearOfflineData()
    {
        GamePrefs.DeleteKey(DATA_PATH);
        if (Current != null) { Current._data = new SpecialStreakData(); Current._invalid = false; }
        GamePrefs.Save();
    }
    private void OnDestroy() { if (Current == this) Current = null; }
}

[Serializable]
public sealed class SpecialStreakData
{
    public int Version = 1;
    public bool Activated, TutorialFinished, Settling, PendingPopDialog;
    public int CurrCount, TempCount, ClaimedIndex, MultiplierIndex, LastMultiplierIndex, LastWonLevel;
    public int TargetCount, TargetMultiplier;
}
[Serializable]
public sealed class SpecialStreakRewardInfo
{
    public int Goal;
    public List<SpecialStreakReward> Rewards;
    public List<RewardItem> Items()
    {
        var items = new List<RewardItem>();
        foreach (var reward in Rewards) items.Add(new RewardItem(RewardType.FromString(reward.type), reward.count));
        return items;
    }
}
[Serializable]
public sealed class SpecialStreakReward { public string type; public int count; }
