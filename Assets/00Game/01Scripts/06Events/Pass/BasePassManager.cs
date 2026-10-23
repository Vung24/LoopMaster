using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct PassStageConfig
{
    public int Points;
    public List<RewardItem> FreeRewards, VipRewards;
}
public enum PassType { Default, Christmas }
[Serializable]
public class PassUserState
{
    public long StartTime;
    public int TotalPoints, LastUnlockStage, LastPoints, PendingPoints;
    public bool IsVip, VipUploaded, IsFinalCoinClaimed;
    public PassType Type;
    public string ClaimId;
    public List<int> ClaimedFreeStages = new List<int>();
    public List<int> ClaimedVipStages = new List<int>();
    public List<string> ClaimedTeamRewards = new List<string>();
}

public abstract class BasePassManager : BaseEventManager
{
    public const string STATE_PATH = GamePrefKeys.PassState;
    private const string TutorialKey = GamePrefKeys.PassTutorial;
    public PassSettings PassSettings { get; protected set; }
    public int TEAM_REWARD_COINS { get; protected set; } = 50;
    public int FinalStagePoint { get; protected set; } = 10;
    public int FinalStageCoin { get; protected set; } = 10;
    public int FinalTotalCoinCount { get; protected set; } = 500;
    public int FinalStageCount => FinalTotalCoinCount / FinalStageCoin;
    public int FinalTotalPointCount => FinalStageCount * FinalStagePoint;
    protected PassUserState _state;
    protected int _currentStage;
    protected readonly List<int> _accumulatedStagePoints = new List<int>();
    protected List<PassStageConfig> _stages;
    private EventManager _eventOwner;
    private bool _invalidState, _claiming;
    public UnityEvent PurchaseSuccessAnimationEvent = new UnityEvent();
    public UnityEvent OnVipChanged = new UnityEvent();
    public UnityEvent OnProgressChanged = new UnityEvent();
    public int LastCollectPoints
    {
        get => _state?.PendingPoints ?? 0;
        set { if (_state == null || _invalidState) return; _state.PendingPoints = Math.Max(0, value); Save(); }
    }
    public abstract int GetUnlockLevel();
    public abstract bool IsLocked();
    public abstract string GetProductId();
    public abstract void ShowCoinAnimation(int amount, Vector2 startPos, UnityAction callback);
    protected abstract List<PassStageConfig> GetStageRewardsConfig();
    public virtual bool IsTeamEnable() => false;
    public virtual bool IsHeartEnable() => true;

    public override void Startup()
    {
        if (status == ManagerStatus.Started) return;
        base.Startup();
        PassSettings = Resources.Load<PassSettings>("PassSettings");
        _stages = GetStageRewardsConfig();
        if (_stages == null || _stages.Count == 0) throw new InvalidOperationException("Pass rewards config is missing.");
        _accumulatedStagePoints.Clear();
        int sum = 0;
        foreach (var stage in _stages) { sum = checked(sum + stage.Points); _accumulatedStagePoints.Add(sum); }
        try
        {
            _state = GamePrefs.HasKey(STATE_PATH) ? JsonUtility.FromJson<PassUserState>(GamePrefs.GetString(STATE_PATH)) : new PassUserState();
            if (_state == null || _state.StartTime < 0 || _state.TotalPoints < 0 || _state.PendingPoints < 0 ||
                _state.ClaimedFreeStages == null || _state.ClaimedVipStages == null || _state.ClaimedTeamRewards == null)
                throw new InvalidOperationException("Invalid pass state; original save retained.");
            if (string.IsNullOrEmpty(_state.ClaimId)) { _state.ClaimId = Guid.NewGuid().ToString("N"); Save(); }
        }
        catch (Exception error) { _invalidState = true; Debug.LogError("Battle Pass: " + error.Message); }
        ComputeCurrentStage();
        if (_state != null)
        {
            _state.LastPoints = Math.Max(0, Math.Min(_state.LastPoints, _state.TotalPoints));
            _state.LastUnlockStage = StageAt(_state.LastPoints);
        }
        _eventOwner = Managers.Event;
        _eventOwner?.RegisterEvent("OnGameWin", OnGameWin);
        CheckActive();
        ApplyVipBenefits(false);
    }
    protected virtual void OnDestroy() => _eventOwner?.UnregisterEvent("OnGameWin", OnGameWin);
    private void Save() { if (_invalidState || _state == null) return; GamePrefs.SetString(STATE_PATH, JsonUtility.ToJson(_state)); GamePrefs.Save(); }
    private DateTime Now => DateTimeOffset.FromUnixTimeSeconds(TimeUtils.GetUnixTimestamp()).LocalDateTime;
    private int NormalGoal => _accumulatedStagePoints.Count == 0 ? 0 : _accumulatedStagePoints[_accumulatedStagePoints.Count - 1];
    private void ComputeCurrentStage()
    {
        _currentStage = StageAt(_state?.TotalPoints ?? 0);
    }
    private int StageAt(int points)
    {
        for (int i = 0; i < _accumulatedStagePoints.Count; i++) if (points < _accumulatedStagePoints[i]) return i;
        return GetNormalStageCount() + Math.Min(FinalStageCount, Math.Max(0, points - NormalGoal) / FinalStagePoint);
    }
    public bool CheckActive()
    {
        if (_invalidState || IsLocked() || _state == null) return false;
        if (_state.StartTime == 0) StartNewSession();
        return true;
    }
    public override void EventBeforeEnterHome() { CheckActive(); ComputeCurrentStage(); ApplyVipBenefits(false); }
    public bool IsFinished()
    {
        if (_state == null || _state.StartTime == 0) return false;
        var start = DateTimeOffset.FromUnixTimeSeconds(_state.StartTime).LocalDateTime;
        return start.Year != Now.Year || start.Month != Now.Month;
    }
    public bool ShouldShowTutorial() => CheckActive() && GamePrefs.GetInt(TutorialKey, 0) == 0;
    public void SetTutorialShown() { GamePrefs.SetInt(TutorialKey, 1); GamePrefs.Save(); }
    public bool CanPurchaseVIP() => CheckActive() && !IsFinished() && !IsVip();
    public bool IsVip() => _state != null && _state.IsVip;
    public bool HasActiveVip => IsVip() && !IsFinished();
    public void SetVip()
    {
        if (!CanPurchaseVIP()) return;
        _state.IsVip = true; Save(); ApplyVipBenefits(true);
        PassDetailDialog.ShouldShowUnlockVipAnim = true;
        OnVipChanged.Invoke(); OnProgressChanged.Invoke();
    }
    private void ApplyVipBenefits(bool refill)
    {
        if (Managers.Heart == null) return;
        if (HasActiveVip)
        {
            if (refill) Managers.Heart.OnPassVipActivated();
            else Managers.Heart.SetPassVipActive(true);
        }
        else Managers.Heart.SetPassVipActive(false);
    }
    public void NotifyPurchaseSuccessAnimationEnded() => PurchaseSuccessAnimationEvent.Invoke();
    private void OnGameWin(object args)
    {
        if (!GameWinArgs.Get(args, GameWinArgs.PassPoints, out int count) || count <= 0 || !CanCollectPoints()) return;
        int before = _state.TotalPoints;
        _state.TotalPoints = (int)Math.Min((long)NormalGoal + FinalTotalPointCount, (long)before + count);
        _state.PendingPoints = (int)Math.Min(int.MaxValue, (long)_state.PendingPoints + _state.TotalPoints - before);
        ComputeCurrentStage(); Save(); OnProgressChanged.Invoke();
    }
    public bool CanCollectPoints() => CheckActive() && !IsFinished() && !ShouldShowTutorial() && !IsFinalBundleFinished();
    public void AddPoint(int count)
    {
        if (count <= 0 || !CheckActive() || IsFinished()) return;
        _state.TotalPoints = (int)Math.Min((long)NormalGoal + FinalTotalPointCount, (long)_state.TotalPoints + count);
        ComputeCurrentStage(); Save(); OnProgressChanged.Invoke();
    }
    public long GetEndTime()
    {
        var month = _state != null && _state.StartTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(_state.StartTime).LocalDateTime : Now;
        return new DateTimeOffset(new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(1)).ToUnixTimeSeconds();
    }
    public int GetLeftSeconds() => (int)Math.Max(0, Math.Min(int.MaxValue, GetEndTime() - TimeUtils.GetUnixTimestamp()));
    public int GetRequirePoints(int stageIndex) => stageIndex >= 0 && stageIndex < GetNormalStageCount() ? _stages[stageIndex].Points : FinalStagePoint;
    public int GetNextRequirePoints() => GetRequirePoints(_currentStage);
    public int GetCurrentPoints() => PointsAt(_state?.TotalPoints ?? 0, _currentStage);
    private int PointsAt(int total, int stage)
    {
        if (stage >= GetNormalStageCount()) return Math.Max(0, total - NormalGoal) % FinalStagePoint;
        return total - (stage > 0 ? _accumulatedStagePoints[stage - 1] : 0);
    }
    public int GetCurrentStageProgress() => GetNextRequirePoints() > 0 ? GetCurrentPoints() * 100 / GetNextRequirePoints() : 0;
    public int GetCurrentStage() => _currentStage;
    public int GetTotalPoints() => _state?.TotalPoints ?? 0;
    public int GetLastTotalPoints() => _state?.LastPoints ?? 0;
    public int GetStageAtPoints(int points) => StageAt(Math.Max(0, points));
    public int GetPointsAtTotal(int points) => PointsAt(Math.Max(0, points), GetStageAtPoints(points));
    public int GetLastUnlockStage() => _state?.LastUnlockStage ?? 0;
    public int GetLastPoints() => PointsAt(_state?.LastPoints ?? 0, GetLastUnlockStage());
    public int GetLastFinalBundleCoin() => IsVip() && _state != null && !_state.IsFinalCoinClaimed ? Math.Min(FinalTotalCoinCount, Math.Max(0, GetLastUnlockStage() - GetNormalStageCount()) * FinalStageCoin) : 0;
    public int GetCurrentFinalBundleCoin() => IsVip() && _state != null && !_state.IsFinalCoinClaimed ? Math.Min(FinalTotalCoinCount, Math.Max(0, _currentStage - GetNormalStageCount()) * FinalStageCoin) : 0;
    public void UpdateLastStageAndPoints()
    {
        AcknowledgePresentedPoints(GetTotalPoints());
    }
    public void AcknowledgePresentedPoints(int totalPoints)
    {
        if (_state == null || _invalidState) return;
        int presented = Math.Max(_state.LastPoints, Math.Min(totalPoints, _state.TotalPoints));
        _state.PendingPoints = Math.Max(0, _state.PendingPoints - (presented - _state.LastPoints));
        _state.LastPoints = presented; _state.LastUnlockStage = StageAt(presented); Save();
    }
    public int GetNormalStageCount() => _stages?.Count ?? 0;
    public int GetTotalStageCount() => GetNormalStageCount() + FinalStageCount;
    public bool WillUnlockAllNormalStages(int additionalPoints) => _state != null && (long)_state.TotalPoints + additionalPoints >= NormalGoal;
    public PassStageConfig GetStageConfigByIndex(int index) => _stages[index];
    public bool IsStageUnlocked(int index) => _state != null && index >= 0 && index < _currentStage;
    public bool IsStageUnlockedLastTime(int index) => index >= 0 && index < GetLastUnlockStage();
    private string Receipt(string track, int index) => "pass/" + _state.ClaimId + "/" + track + "/" + index;
    public bool IsFreeRewardClaimed(int index) => _state != null && (_state.ClaimedFreeStages.Contains(index) || InventoryManager.Ensure().IsRewardClaimed(Receipt("free", index)));
    public bool IsVipRewardClaimed(int index) => index == 0 || (_state != null && (_state.ClaimedVipStages.Contains(index) || InventoryManager.Ensure().IsRewardClaimed(Receipt("vip", index))));
    public bool IsFinalRewardUnlocked() => _state != null && _currentStage >= GetNormalStageCount();
    public bool IsFinalRewardUnlockedLastTime() => _state != null && GetLastUnlockStage() >= GetNormalStageCount();
    public bool IsFinalBundleFinished() => _state != null && _currentStage >= GetTotalStageCount();
    public bool IsFinalBundleFinishedLastTime() => _state != null && GetLastUnlockStage() >= GetTotalStageCount();
    public int GetUnclaimedRewardCount()
    {
        if (_state == null) return 0;
        int count = 0;
        for (int i = 0; i < Math.Min(_currentStage, GetNormalStageCount()); i++)
        { if (!IsFreeRewardClaimed(i)) count++; if (IsVip() && !IsVipRewardClaimed(i)) count++; }
        return count;
    }
    public List<RewardItem> ClaimFreeRewards(int index) => Claim(index, false);
    public List<RewardItem> ClaimVipRewards(int index) => Claim(index, true);
    private List<RewardItem> Claim(int index, bool vip)
    {
        if (_invalidState || _claiming || !IsStageUnlocked(index) || index >= GetNormalStageCount() ||
            (vip ? !IsVip() || IsVipRewardClaimed(index) : IsFreeRewardClaimed(index))) return null;
        _claiming = true;
        try
        {
            var rewards = vip ? _stages[index].VipRewards : _stages[index].FreeRewards;
            if (!InventoryManager.Ensure().TryClaimRewards(Receipt(vip ? "vip" : "free", index), rewards, out var result)) return null;
            (vip ? _state.ClaimedVipStages : _state.ClaimedFreeStages).Add(index); Save(); OnProgressChanged.Invoke();
            return result == RewardClaimResult.Claimed ? new List<RewardItem>(rewards) : null;
        }
        finally { _claiming = false; }
    }
    public List<RewardItem> GetAllUnclaimedRewards()
    {
        var rewards = new List<RewardItem>();
        for (int i = 0; i < Math.Min(_currentStage, GetNormalStageCount()); i++)
        { if (!IsFreeRewardClaimed(i)) rewards.AddRange(_stages[i].FreeRewards); if (IsVip() && !IsVipRewardClaimed(i)) rewards.AddRange(_stages[i].VipRewards); }
        return rewards;
    }
    public List<RewardItem> ClaimAllCurrentRewards()
    {
        var rewards = new List<RewardItem>();
        for (int i = 0; i < Math.Min(_currentStage, GetNormalStageCount()); i++)
        { var free = ClaimFreeRewards(i); if (free != null) rewards.AddRange(free); var vip = ClaimVipRewards(i); if (vip != null) rewards.AddRange(vip); }
        return rewards;
    }
    public void SetAllCurrentRewardsClaimed() => ClaimAllCurrentRewards();
    public List<RewardItem> ClaimFinalCoins()
    {
        int coins = GetCurrentFinalBundleCoin();
        if (_invalidState || _claiming || coins <= 0 || (!IsFinished() && !IsFinalBundleFinished())) return null;
        _claiming = true;
        try
        {
            var rewards = new List<RewardItem> { RewardItem.Coin(coins) };
            if (!InventoryManager.Ensure().TryClaimRewards(Receipt("final", 0), rewards, out var result)) return null;
            _state.IsFinalCoinClaimed = true; Save(); OnProgressChanged.Invoke();
            return result == RewardClaimResult.Claimed ? rewards : null;
        }
        finally { _claiming = false; }
    }
    public void SetFinalCoinClaimed() => ClaimFinalCoins();
    public bool IsTeamMemberRewardClaimed(string id) => _state != null && _state.ClaimedTeamRewards.Contains(id);
    public void ClaimTeamMemberReward(string id)
    {
        if (!IsTeamEnable() || string.IsNullOrEmpty(id) || IsTeamMemberRewardClaimed(id)) return;
        if (InventoryManager.Ensure().TryClaimRewards(Receipt("team/" + id, 0), new List<RewardItem> { RewardItem.Coin(TEAM_REWARD_COINS) }, out _))
        { _state.ClaimedTeamRewards.Add(id); Save(); }
    }
    public bool TryStartNextSession()
    {
        if (!IsFinished() || GetUnclaimedRewardCount() > 0 || GetCurrentFinalBundleCoin() > 0) return false;
        StartNewSession(); return true;
    }
    public void StartNewSession()
    {
        if (_invalidState || (_state != null && _state.StartTime > 0 && (!IsFinished() || GetUnclaimedRewardCount() > 0 || GetCurrentFinalBundleCoin() > 0))) return;
        bool wasVip = IsVip();
        _state = new PassUserState { StartTime = TimeUtils.GetUnixTimestamp(), ClaimId = Guid.NewGuid().ToString("N"), Type = Now.Month == 12 ? PassType.Christmas : PassType.Default };
        ComputeCurrentStage(); Save(); ApplyVipBenefits(false);
        if (wasVip) OnVipChanged.Invoke(); OnProgressChanged.Invoke();
    }
    public PassType GetOnGoingPassType() => _state?.Type ?? PassType.Default;
    public string ThemeAddress(string address) => GetOnGoingPassType() == PassType.Christmas ? "Christmas" + address : address;
    public UniTask<string> GetOngoingPassName(bool isCaps)
    {
        string name = GetOnGoingPassType() == PassType.Christmas ? "Christmas Pass" : "Golden Pass";
        return UniTask.FromResult(isCaps ? name.ToUpperInvariant() : name);
    }
    public UniTask<string> GetNowTimePassName(bool isCaps) => GetOngoingPassName(isCaps);
    public override UniTask<HomeEventBase> CreateIcon(Transform parent)
    {
        var prefab = EventPrefabUI.Load(ThemeAddress("HomeEventPass"));
        return UniTask.FromResult<HomeEventBase>(prefab == null ? null : Instantiate(prefab, parent, false).GetComponent<HomeEventPass>());
    }
    public override void ClearData()
    {
        GamePrefs.DeleteKey(STATE_PATH); GamePrefs.DeleteKey(TutorialKey); GamePrefs.Save();
        _invalidState = false; _state = new PassUserState(); CheckActive();
    }
}
