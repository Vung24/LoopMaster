using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;
using UnityEngine.UI;

public class PassDetailDialog : MonoBehaviour
{
    public static PassDetailDialog Current;
    public static bool ShouldShowUnlockVipAnim;
    public AudioClip ClaimJumpSound, ExpSingleSound, ExpTargetSound, ExpParticleDropSound;
    public Sprite VipActiviteSprite;
    public PassTicketCell TicketCellAsset;
    public PassRewardsCell RewardsCellAsset;
    public PassFinalBundleCell FinalBundleCellAsset;
    [SerializeField] private PassScrollAdapter _adapter;
    private ScrollRect _scroll;
    public PassScrollAdapter ScrollAdapter => _adapter;
    private PassStarProgressBtn _star;
    private BasePassManager _manager;
    private bool _closed;
    private bool _ready, _presenting, _vipQueued;
    private int _displayPoints;
    public bool IsAnimating => _presenting;
    public static UniTask<PassDetailDialog> Show(Transform parent) => EventPrefabUI.Show<PassDetailDialog>(parent, Managers.Pass.ThemeAddress("PassDetailDialog"));
    public UniTask WaitForCloseAsync() => UniTask.WaitUntil(() => this == null || _closed);
    private void Awake()
    {
        Current = this;
        _manager = Managers.Pass;
        _scroll = PassUI.Node<ScrollRect>(transform, "TableView");
        if (_adapter == null) _adapter = GetComponentInChildren<PassScrollAdapter>(true);
        if (_adapter == null && _scroll != null) _adapter = _scroll.gameObject.AddComponent<PassScrollAdapter>();
        _star = GetComponentInChildren<PassStarProgressBtn>(true);
        PassUI.Bind(transform, "CloseButton", Close);
        PassUI.Bind(transform, "InfoButton", () => PassInfoDialog.Show(transform.parent).Forget());
        PassUI.Bind(transform, "ActivateBtn", () => PassPurchaseDialogContent.Show(transform.parent, "pass_detail").Forget());
        PassUI.Active(transform, "BlockMask", false);
        Snui_Text.EnsureFontAssets(transform);
    }
    private void Start()
    {
        if (_manager == null || !_manager.CheckActive()) { Close(); return; }
        _manager.OnProgressChanged.AddListener(Refresh);
        _manager.OnVipChanged.AddListener(OnVipChanged);
        _manager.PurchaseSuccessAnimationEvent.AddListener(OnPurchaseConfirmed);
        var timer = PassUI.Node<Snui.Timer>(transform, "Timer");
        if (timer != null) { timer.CountdownUntil(_manager.GetEndTime()); timer.OnStopped?.AddListener(Refresh); }
        if (_adapter == null || _scroll == null || TicketCellAsset == null || RewardsCellAsset == null || FinalBundleCellAsset == null)
            throw new System.InvalidOperationException("Battle Pass table prefab references are missing.");
        _adapter.Configure(_scroll, _manager, TicketCellAsset, RewardsCellAsset, FinalBundleCellAsset);
        _displayPoints = _manager.GetLastTotalPoints();
        Refresh();
        _adapter.FocusCurrentReward();
        _ready = true; PresentProgress().Forget();
    }
    private void OnVipChanged() => Refresh();
    private void OnPurchaseConfirmed() { if (!_closed) PresentProgress(true).Forget(); }
    public async UniTask PlayUnlockVipAnimIfNeed()
    {
        if (!ShouldShowUnlockVipAnim || !_manager.IsVip()) return;
        ShouldShowUnlockVipAnim = false;
        if (_adapter != null) await _adapter.PlayVipUnlock();
    }
    public void Refresh()
    {
        if (_manager == null) return;
        _adapter?.RefreshRewards();
        if (!_presenting) _star?.SetPresentation(_displayPoints);
        PassUI.Active(transform, "NoVip", !_manager.IsVip());
        PassUI.Active(transform, "Vip", _manager.IsVip());
        var activate = PassUI.Node<Button>(transform, "ActivateBtn");
        if (activate != null) activate.interactable = _manager.CanPurchaseVIP();
        var lockLine = GetComponentInChildren<PassLockLine>(true);
        lockLine?.Refresh();
        if (_ready && !_presenting && (_vipQueued || _manager.GetTotalPoints() != _displayPoints)) PresentProgress().Forget();
    }
    private async UniTask PresentProgress(bool playVip = false)
    {
        if (playVip) _vipQueued = true;
        if (!_ready || _closed || _presenting) return;
        _presenting = true;
        PassUI.Active(transform, "BlockMask", true);
        try
        {
            if (_vipQueued) { _vipQueued = false; await PlayUnlockVipAnimIfNeed(); }
            while (!_closed && _displayPoints < _manager.GetTotalPoints())
            {
                int target = _manager.GetTotalPoints();
                int fromStage = _manager.GetStageAtPoints(_displayPoints), targetStage = _manager.GetStageAtPoints(target);
                _star?.PlayCollectAnim(); PassUI.Sound(ExpSingleSound);
                // Native bar segments take .4 sec; large restored-save jumps use one summary fill.
                if (targetStage - fromStage <= 8 && _star != null)
                {
                    int cursor = _displayPoints;
                    while (cursor < target)
                    {
                        int stage = _manager.GetStageAtPoints(cursor), count = _manager.GetPointsAtTotal(cursor);
                        int goal = _manager.GetRequirePoints(stage);
                        int step = Mathf.Min(target - cursor, goal - count);
                        _star.SetPresentation(cursor);
                        await _star.AnimateCountTo(count + step);
                        cursor += step;
                        if (_manager.GetStageAtPoints(cursor) > stage) { _star.PlayProgressEffect(); PassUI.Sound(ExpTargetSound); }
                    }
                }
                else if (_star != null) await _star.AnimateCountTo(_manager.GetRequirePoints(fromStage));
                if (targetStage != fromStage) await _adapter.AnimatePathTo(targetStage);
                if (_closed) return;
                if (targetStage != fromStage) { _star?.PlayTrailTo(_adapter.PathEndPosition); PassUI.Sound(ExpParticleDropSound); }
                _displayPoints = target; _adapter.SetDisplayStage(targetStage); _adapter.SetPathStage(targetStage);
                _star?.SetPresentation(target);
                _manager.AcknowledgePresentedPoints(target);
            }
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            _presenting = false;
            if (this != null && !_closed) { PassUI.Active(transform, "BlockMask", false); Refresh(); }
        }
    }
    private void Close() { if (_closed) return; _closed = true; gameObject.SetActive(false); Destroy(gameObject); }
    private void OnDestroy()
    {
        _closed = true;
        _manager?.OnProgressChanged.RemoveListener(Refresh);
        _manager?.OnVipChanged.RemoveListener(OnVipChanged);
        _manager?.PurchaseSuccessAnimationEvent.RemoveListener(OnPurchaseConfirmed);
        if (Current == this) Current = null;
    }
}
