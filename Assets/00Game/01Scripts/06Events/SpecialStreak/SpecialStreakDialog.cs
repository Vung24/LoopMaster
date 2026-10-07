using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using Sugar.UI;
using TMPro;
using UnityEngine.UI;

public class SpecialStreakDialog : MonoBehaviour, ITableViewDataSource
{
    [SerializeField] private Button _infoBtn;
    [SerializeField] private Button _closeBtn;
    [SerializeField] private SpecialStreakMultiplierSlider _multiplierSlider;
    [SerializeField] private ProgressBar _progressBar;
    [SerializeField] private RewardCell _rewardCell;
    [SerializeField] private TableView _tableView;
    [SerializeField] private Transform _completedTickNode;
    [SerializeField] private Timer _timer;
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private TableViewCell _specialStreakCellPrefab;

    private const string CELL_ID = "special_streak_cell";
    private readonly List<Material> _materials = new List<Material>();
    private Image _tutorialMask;
    private Transform _infoBtnOriginParent;
    private GameObject _info;
    private bool _closed;

    public event Action Closed;

    public static UniTask<SpecialStreakDialog> Show(Transform parent)
    {
        var prefab = DataPrefabLibrary.Current?.SpecialStreakDialogPrefab;
        if (parent == null || prefab == null) return UniTask.FromResult<SpecialStreakDialog>(null);
        var instance = Instantiate(prefab, parent, false);
        RewardDialog.PrepareOverlay(instance, 1350);
        var dialog = instance.GetComponent<SpecialStreakDialog>();
        if (dialog == null) { Destroy(instance); return UniTask.FromResult<SpecialStreakDialog>(null); }
        dialog.Refresh();
        return UniTask.FromResult(dialog);
    }

    private void Awake()
    {
        SpecialStreakPresentation.Prepare(transform, _materials);
        ResolveHierarchyReferences();
    }

    private void Start()
    {
        InitProgress();
        InitMultiplierNode();
        InitTableView();
        InitCountdown();
        CheckTutorialShow().Forget();
        SetSafeBottom();

        if (_infoBtn != null) _infoBtn.onClick.AddListener(() => OnClickInfoBtn().Forget());
        if (_closeBtn != null) _closeBtn.onClick.AddListener(OnClickCloseBtn);

        SpecialStreakManager.Ensure().Changed += Refresh;
    }

    private void ResolveHierarchyReferences()
    {
        if (_tableView == null)
        {
            var tableNode = HierarchyReferenceCache.Descendant(transform, "TableView");
            if (tableNode != null && tableNode.GetComponent<ScrollRect>() != null)
                _tableView = tableNode.GetComponent<TableView>() ?? tableNode.gameObject.AddComponent<TableView>();
        }
        if (_infoBtn == null)
            _infoBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "InfoBtn") ??
                       HierarchyReferenceCache.DescendantComponent<Button>(transform, "InfoButton");
        if (_closeBtn == null)
            _closeBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseBtn") ??
                        HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseButton");
        if (_multiplierSlider == null)
            _multiplierSlider = HierarchyReferenceCache.DescendantComponent<SpecialStreakMultiplierSlider>(transform, "MultiplierSlider");
        if (_progressBar == null)
            _progressBar = HierarchyReferenceCache.DescendantComponent<ProgressBar>(transform, "ProgressBar");
        if (_rewardCell == null)
            _rewardCell = HierarchyReferenceCache.DescendantComponent<RewardCell>(transform, "RewardCell");
        if (_completedTickNode == null)
            _completedTickNode = HierarchyReferenceCache.Descendant(transform, "CompletedTickNode") ??
                                 HierarchyReferenceCache.Descendant(transform, "Tick");
        if (_timer == null)
            _timer = HierarchyReferenceCache.DescendantComponent<Timer>(transform, "Timer");
        if (_timerText == null)
            _timerText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "TimerText");

        if (_specialStreakCellPrefab == null && DataPrefabLibrary.Current?.SpecialStreakCellPrefab != null)
        {
            var cellObj = DataPrefabLibrary.Current.SpecialStreakCellPrefab;
            _specialStreakCellPrefab = cellObj.GetComponent<TableViewCell>();
        }
    }

    private void InitProgress()
    {
        var manager = SpecialStreakManager.Ensure();
        var currReward = manager.GetCurrRewardInfo();

        if (manager.IsCompleted())
        {
            int lastGoal = manager.GetLastGoal();
            _progressBar?.SetPercent(lastGoal, lastGoal);
            if (_rewardCell != null) _rewardCell.gameObject.SetActive(false);
            SpecialStreakPresentation.SetActive(_completedTickNode, true);
        }
        else if (currReward != null)
        {
            _progressBar?.SetPercent(manager.CurrCount, currReward.Goal);
            if (_rewardCell != null)
            {
                _rewardCell.gameObject.SetActive(true);
                if (currReward.Items().Count > 0)
                {
                    _rewardCell.Init(currReward.Items()[0], true, true).Forget();
                }
            }
            SpecialStreakPresentation.SetActive(_completedTickNode, false);
        }
    }

    private void InitMultiplierNode()
    {
        var manager = SpecialStreakManager.Ensure();
        _multiplierSlider?.Init(manager.MultiplierIndex);
    }

    private void InitTableView()
    {
        if (_tableView == null) return;
        _tableView.DataSource = this;
        _tableView.ReloadData();

        var manager = SpecialStreakManager.Ensure();
        var configs = manager.GetRewardInfosConfig();
        if (configs != null && configs.Count > 1)
        {
            float scroll = Mathf.Clamp01((float)manager.ClaimedIndex / (float)(configs.Count - 1));
            if (_tableView.ScrollRect != null)
            {
                _tableView.ScrollRect.verticalNormalizedPosition = scroll;
            }
        }
    }

    private void InitCountdown()
    {
        var manager = SpecialStreakManager.Ensure();
        bool completed = manager.IsCompleted();
        SpecialStreakPresentation.SetActive(_timer, false);
        if (_timerText != null)
        {
            _timerText.text = completed ? "COMPLETED" : "OFFLINE — NO TIME LIMIT";
        }
    }

    private void SetSafeBottom()
    {
        if (_tableView == null) return;
        var canvas = GetComponentInParent<Canvas>();
        float safeBottom = ScreenHelper.GetCanvasSafeBottom(canvas);
        var rt = _tableView.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.offsetMin = new Vector2(rt.offsetMin.x, safeBottom + rt.offsetMin.y);
        }
    }

    public int GetNumOfSections(TableView tableView) => 1;

    public int GetNumOfCellsInSection(TableView tableView, int section)
    {
        var manager = SpecialStreakManager.Ensure();
        var configs = manager.GetRewardInfosConfig();
        return configs != null ? configs.Count : 0;
    }

    public float GetHeightOfCell(TableView tableView, int section, int row) => 220f;

    public TableViewCell GetTableViewCell(TableView tableView, int section, int row)
    {
        var cell = tableView.DequeueCell(CELL_ID);
        if (cell == null)
        {
            if (_specialStreakCellPrefab != null)
            {
                cell = Instantiate(_specialStreakCellPrefab, tableView.ScrollRect.content, false);
            }
            else
            {
                var cellPrefab = DataPrefabLibrary.Current?.SpecialStreakCellPrefab;
                if (cellPrefab != null)
                {
                    var cellGo = Instantiate(cellPrefab, tableView.ScrollRect.content, false);
                    cell = cellGo.GetComponent<TableViewCell>() ?? cellGo.AddComponent<TableViewCell>();
                }
            }
            if (cell != null) cell.TypeIdentifier = CELL_ID;
        }

        if (cell != null)
        {
            var streakCell = cell.GetComponent<SpecialStreakCell>();
            if (streakCell != null) streakCell.Init(row);
        }

        return cell;
    }

    private async UniTaskVoid CheckTutorialShow()
    {
        var manager = SpecialStreakManager.Ensure();
        if (!manager.ShouldShowTutorial()) return;

        if (_closeBtn != null) _closeBtn.interactable = false;
        if (_infoBtn != null) _infoBtn.interactable = false;

        await UniTask.WaitForSeconds(0.4f);

        var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        _tutorialMask = UiUtils.CreateMask(canvas);
        if (_tutorialMask != null)
        {
            await _tutorialMask.DOFade(0.99f, 0.2f).SetLink(gameObject).AsyncWaitForCompletion();
        }

        if (_infoBtn != null && _tutorialMask != null)
        {
            _infoBtnOriginParent = _infoBtn.transform.parent;
            _infoBtn.transform.SetParent(_tutorialMask.transform);
        }

        await UniTask.WaitForSeconds(0.3f);

        if (_tutorialMask != null)
        {
            var finger = await Finger.Show(_tutorialMask.transform);
            if (finger != null && _infoBtn != null)
            {
                finger.ShowFinger(_infoBtn.transform.localPosition);
            }
        }

        if (_infoBtn != null) _infoBtn.interactable = true;
    }

    private void CheckTutorialMaskDismiss()
    {
        var manager = SpecialStreakManager.Ensure();
        if (!manager.ShouldShowTutorial() || _tutorialMask == null) return;

        if (_infoBtn != null)
        {
            _infoBtn.interactable = false;
            if (_infoBtnOriginParent != null)
            {
                _infoBtn.transform.SetParent(_infoBtnOriginParent);
            }
        }

        if (_tutorialMask.transform.childCount > 0)
        {
            var child = _tutorialMask.transform.GetChild(0);
            if (child != null) child.gameObject.SetActive(false);
        }
    }

    private async UniTaskVoid CheckTutorialTableViewScroll()
    {
        var manager = SpecialStreakManager.Ensure();
        if (!manager.ShouldShowTutorial()) return;

        if (_tutorialMask != null)
        {
            Destroy(_tutorialMask.gameObject);
            _tutorialMask = null;
        }

        if (_tableView != null && _tableView.ScrollRect != null)
        {
            var content = _tableView.ScrollRect.content;
            var viewport = _tableView.ScrollRect.viewport ?? (_tableView.transform as RectTransform);
            if (content != null && viewport != null && content.rect.height - viewport.rect.height > 0)
            {
                await UniTask.WaitForSeconds(0.5f);
                await content.DOAnchorPosY(0f, 3f).SetEase(Ease.InOutSine).SetLink(gameObject).AsyncWaitForCompletion();
            }
        }

        manager.IsTutorialFinished = true;
        if (_closeBtn != null) _closeBtn.interactable = true;
        if (_infoBtn != null) _infoBtn.interactable = true;
    }

    public void Refresh()
    {
        InitProgress();
        InitMultiplierNode();
        InitTableView();
        InitCountdown();
    }

    public async UniTaskVoid OnClickInfoBtn()
    {
        var prefab = DataPrefabLibrary.Current?.SpecialStreakInfoDialogPrefab;
        if (_info != null || prefab == null || _closed) return;

        CheckTutorialMaskDismiss();

        _info = Instantiate(prefab, transform.parent, false);
        RewardDialog.PrepareOverlay(_info, 1400);
        SpecialStreakPresentation.Prepare(_info.transform, _materials);

        var button = HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Descendant(_info.transform, "ContinueButton"));
        if (button != null) button.onClick.AddListener(CloseInfo);

        RecoveryDialogUI.Label(HierarchyReferenceCache.Descendant(_info.transform, "Text1"), "WIN LEVELS TO COLLECT");
        RecoveryDialogUI.Label(HierarchyReferenceCache.Descendant(_info.transform, "Text2"), "CONSECUTIVE WINS INCREASE YOUR MULTIPLIER");
        RecoveryDialogUI.Label(HierarchyReferenceCache.Descendant(_info.transform, "Text3"), "RETRY OR QUIT RESETS THE MULTIPLIER. REVIVE KEEPS IT.");

        var manager = SpecialStreakManager.Ensure();
        if (manager.ShouldShowTutorial())
        {
            var bgImg = _info.GetComponent<Image>();
            if (bgImg != null) bgImg.enabled = false;
        }

        await UniTask.WaitUntil(() => _info == null);
        CheckTutorialTableViewScroll().Forget();
    }

    private void CloseInfo()
    {
        if (_info != null) Destroy(_info);
        _info = null;
    }

    private void OnClickCloseBtn()
    {
        Close();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_info != null) CloseInfo();
            else Close();
        }
    }

    public void Close()
    {
        if (_closed) return;
        _closed = true;
        SpecialStreakManager.Ensure().IsTutorialFinished = true;
        CheckTutorialMaskDismiss();
        Closed?.Invoke();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (SpecialStreakManager.Current != null) SpecialStreakManager.Current.Changed -= Refresh;
        CloseInfo();
        if (!_closed)
        {
            _closed = true;
            Closed?.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (SpecialStreakManager.Current != null) SpecialStreakManager.Current.Changed -= Refresh;
        if (_closeBtn != null) _closeBtn.onClick.RemoveListener(OnClickCloseBtn);
        SpecialStreakPresentation.Cleanup(_materials);
    }
}
