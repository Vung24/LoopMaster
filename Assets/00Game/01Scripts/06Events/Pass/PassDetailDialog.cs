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
        var timer = PassUI.Node<Snui.Timer>(transform, "Timer");
        if (timer != null) { timer.CountdownUntil(_manager.GetEndTime()); timer.OnStopped?.AddListener(Refresh); }
        if (_adapter == null || _scroll == null || TicketCellAsset == null || RewardsCellAsset == null || FinalBundleCellAsset == null)
            throw new System.InvalidOperationException("Battle Pass table prefab references are missing.");
        _adapter.Configure(_scroll, _manager, TicketCellAsset, RewardsCellAsset, FinalBundleCellAsset);
        Refresh();
        _adapter.FocusCurrentReward();
        if (_manager.LastCollectPoints > 0) { _star?.PlayCollectAnim(); PassUI.Sound(ExpTargetSound); }
        _manager.UpdateLastStageAndPoints();
        PlayUnlockVipAnimIfNeed().Forget();
    }
    private void OnVipChanged() { Refresh(); PlayUnlockVipAnimIfNeed().Forget(); }
    public UniTask PlayUnlockVipAnimIfNeed()
    {
        if (!ShouldShowUnlockVipAnim || !_manager.IsVip()) return UniTask.CompletedTask;
        ShouldShowUnlockVipAnim = false;
        _adapter?.PlayVipUnlock();
        _manager.NotifyPurchaseSuccessAnimationEnded();
        return UniTask.CompletedTask;
    }
    public void Refresh()
    {
        if (_manager == null) return;
        _adapter?.RefreshRewards(); _star?.Refresh();
        PassUI.Active(transform, "NoVip", !_manager.IsVip());
        PassUI.Active(transform, "Vip", _manager.IsVip());
        var activate = PassUI.Node<Button>(transform, "ActivateBtn");
        if (activate != null) activate.interactable = _manager.CanPurchaseVIP();
        var lockLine = GetComponentInChildren<PassLockLine>(true);
        lockLine?.Refresh();
    }
    private void Close() { if (_closed) return; _closed = true; _manager?.UpdateLastStageAndPoints(); gameObject.SetActive(false); Destroy(gameObject); }
    private void OnDestroy()
    {
        _closed = true;
        _manager?.OnProgressChanged.RemoveListener(Refresh);
        _manager?.OnVipChanged.RemoveListener(OnVipChanged);
        if (Current == this) Current = null;
    }
}
