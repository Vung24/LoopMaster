using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Snui;
using Sugar.UI;
using UnityEngine;
using UnityEngine.UI;

public class PassDetailDialog : MonoBehaviour, ITableViewDataSource
{
    public static PassDetailDialog Current;
    public static bool ShouldShowUnlockVipAnim;
    public AudioClip ClaimJumpSound, ExpSingleSound, ExpTargetSound, ExpParticleDropSound;
    public Sprite VipActiviteSprite;
    public PassTicketCell TicketCellAsset;
    public PassRewardsCell RewardsCellAsset;
    public PassFinalBundleCell FinalBundleCellAsset;
    private readonly List<PassRewardsCell> _cells = new List<PassRewardsCell>();
    private TableView _table;
    private PassFinalBundleCell _final;
    private PassStarProgressBtn _star;
    private BasePassManager _manager;
    private bool _closed;
    public static UniTask<PassDetailDialog> Show(Transform parent) => EventPrefabUI.Show<PassDetailDialog>(parent, Managers.Pass.ThemeAddress("PassDetailDialog"));
    public UniTask WaitForCloseAsync() => UniTask.WaitUntil(() => this == null || _closed);
    private void Awake()
    {
        Current = this;
        _manager = Managers.Pass;
        _table = GetComponentInChildren<TableView>(true);
        if (_table == null)
        {
            var scroll = PassUI.Node<ScrollRect>(transform, "TableView");
            if (scroll != null) _table = scroll.gameObject.AddComponent<TableView>();
        }
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
        if (_table == null || TicketCellAsset == null || RewardsCellAsset == null || FinalBundleCellAsset == null)
            throw new System.InvalidOperationException("Battle Pass table prefab references are missing.");
        _table.DataSource = this;
        _table.ReloadData();
        var content = _table.ScrollRect.content;
        var bg = HierarchyReferenceCache.ChildComponent<RectTransform>(content, "RewardsBg");
        if (bg != null) bg.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _manager.GetNormalStageCount() * PassRewardsCell.Height);
        Refresh();
        // Scroll to the first unclaimed/free milestone, keeping the ticket visible for beginners.
        int row = Mathf.Clamp(_manager.GetCurrentStage() - 1, 0, _manager.GetNormalStageCount() - 1);
        float travel = Mathf.Max(1, content.rect.height - _table.ScrollRect.viewport.rect.height);
        _table.ScrollRect.verticalNormalizedPosition = 1 - Mathf.Clamp01(row * PassRewardsCell.Height / travel);
        if (_manager.LastCollectPoints > 0) { _star?.PlayCollectAnim(); PassUI.Sound(ExpTargetSound); }
        _manager.UpdateLastStageAndPoints();
        PlayUnlockVipAnimIfNeed().Forget();
    }
    private void OnVipChanged() { Refresh(); PlayUnlockVipAnimIfNeed().Forget(); }
    public async UniTask PlayUnlockVipAnimIfNeed()
    {
        if (!ShouldShowUnlockVipAnim || !_manager.IsVip()) return;
        ShouldShowUnlockVipAnim = false;
        foreach (var cell in _cells) if (cell != null) await cell.PlayUnlockVipAnim();
        _manager.NotifyPurchaseSuccessAnimationEnded();
    }
    public void Refresh()
    {
        if (_manager == null) return;
        foreach (var cell in _cells) if (cell != null) cell.Refresh();
        _final?.Init(); _star?.Refresh();
        PassUI.Active(transform, "NoVip", !_manager.IsVip());
        PassUI.Active(transform, "Vip", _manager.IsVip());
        var activate = PassUI.Node<Button>(transform, "ActivateBtn");
        if (activate != null) activate.interactable = _manager.CanPurchaseVIP();
        var lockLine = GetComponentInChildren<PassLockLine>(true);
        lockLine?.Refresh();
        var barNode = HierarchyReferenceCache.Path(_table?.transform, "Viewport/Content/Progress/Bar");
        var bar = barNode == null ? null : barNode.GetComponent<Image>();
        if (bar != null) bar.fillAmount = Mathf.Clamp01((float)_manager.GetCurrentStage() / _manager.GetNormalStageCount());
    }
    public int GetNumOfSections(TableView tableView) => 3;
    public int GetNumOfCellsInSection(TableView tableView, int section) => section == 1 ? _manager.GetNormalStageCount() : 1;
    public float GetHeightOfCell(TableView tableView, int section, int row) => section == 0 ? PassTicketCell.Height : section == 1 ? PassRewardsCell.Height : PassFinalBundleCell.Height;
    public TableViewCell GetTableViewCell(TableView tableView, int section, int row)
    {
        if (section == 0) return Instantiate(TicketCellAsset, tableView.ScrollRect.content, false);
        if (section == 2) { _final = Instantiate(FinalBundleCellAsset, tableView.ScrollRect.content, false); _final.Init(); return _final; }
        var cell = Instantiate(RewardsCellAsset, tableView.ScrollRect.content, false);
        cell.Init(row); _cells.Add(cell); return cell;
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
