using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using TMPro;
using UnityEngine.UI;

public class SpecialStreakDialog : MonoBehaviour
{
    [SerializeField] private Button _infoBtn;
    [SerializeField] private Button _closeBtn;
    [SerializeField] private SpecialStreakMultiplierSlider _multiplierSlider;
    [SerializeField] private ProgressBar _progressBar;
    [SerializeField] private RewardCell _rewardCell;
    [SerializeField] private SpecialStreakScrollAdapter _scrollAdapter;
    [SerializeField] private Transform _completedTickNode;
    [SerializeField] private Timer _timer;
    [SerializeField] private TMP_Text _timerText;
    private readonly List<Material> _materials = new List<Material>();
    private Image _tutorialMask;
    private Transform _infoBtnOriginParent;
    private GameObject _info;
    private bool _infoLoading;
    private bool _closed;
    private bool _started;

    public event Action Closed;

    public static async UniTask<SpecialStreakDialog> Show(Transform parent)
    {
        if (parent == null) return null;
        var dialog = await PopupAssets.Instantiate<SpecialStreakDialog>("SpecialStreakDialog", parent, parent.GetCancellationTokenOnDestroy());
        var instance = dialog.gameObject;
        RewardDialog.PrepareOverlay(instance, 1350);
        dialog.Refresh();
        return dialog;
    }

    private void Awake()
    {
        SpecialStreakPresentation.Prepare(transform, _materials);
        ResolveHierarchyReferences();
    }

    private void Start()
    {
        SetSafeBottom();
        Canvas.ForceUpdateCanvases();
        _started = true;
        InitProgress();
        InitMultiplierNode();
        _scrollAdapter?.RefreshRewards(true);
        InitCountdown();
        CheckTutorialShow().Forget();

        if (_infoBtn != null) _infoBtn.onClick.AddListener(() => OnClickInfoBtn().Forget());
        if (_closeBtn != null) _closeBtn.onClick.AddListener(OnClickCloseBtn);

        SpecialStreakManager.Ensure().Changed += Refresh;
    }

    private void ResolveHierarchyReferences()
    {
        if (_scrollAdapter == null)
            _scrollAdapter = GetComponentInChildren<SpecialStreakScrollAdapter>(true);
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
        if (_scrollAdapter == null) return;
        var canvas = GetComponentInParent<Canvas>();
        float safeBottom = ScreenHelper.GetCanvasSafeBottom(canvas);
        var rt = _scrollAdapter.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.offsetMin = new Vector2(rt.offsetMin.x, safeBottom + rt.offsetMin.y);
        }
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

    private async UniTaskVoid CheckTutorialRewardScroll()
    {
        var manager = SpecialStreakManager.Ensure();
        if (!manager.ShouldShowTutorial()) return;

        if (_tutorialMask != null)
        {
            Destroy(_tutorialMask.gameObject);
            _tutorialMask = null;
        }

        if (_scrollAdapter != null && _scrollAdapter.IsInitialized && _scrollAdapter.GetItemsCount() > 0)
        {
            if (_scrollAdapter.GetContentSize() > _scrollAdapter.GetViewportSize())
            {
                var cancellationToken = this.GetCancellationTokenOnDestroy();
                await UniTask.WaitForSeconds(0.5f, cancellationToken: cancellationToken);
                _scrollAdapter.SmoothScrollTo(0, 3f, overrideCurrentScrollingAnimation: true);
                await UniTask.WaitUntil(() => !_scrollAdapter.IsScrollToAnimationRunning(),
                    cancellationToken: cancellationToken);
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
        if (_started) _scrollAdapter?.RefreshRewards();
        InitCountdown();
    }

    public async UniTaskVoid OnClickInfoBtn()
    {
        if (_info != null || _infoLoading || _closed) return;

        CheckTutorialMaskDismiss();

        _infoLoading = true;
        try
        {
            var instance = await PopupAssets.Instantiate("SpecialStreakInfoDialog", transform.parent, this.GetCancellationTokenOnDestroy());
            if (this == null || _closed || !isActiveAndEnabled) { Destroy(instance); return; }
            _info = instance;
        }
        finally { if (this != null) _infoLoading = false; }
        RewardDialog.PrepareOverlay(_info, 1400);
        SpecialStreakPresentation.Prepare(_info.transform, _materials);

        var content = _info.GetComponent<EventInfoDialog>();
        if (content != null) content.Dismissed += OnInfoDismissed;
        else
        {
            var button = HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Descendant(_info.transform, "ContinueButton"));
            if (button != null) button.onClick.AddListener(CloseInfo);
        }

        DialogUI.Label(HierarchyReferenceCache.Descendant(_info.transform, "Text1"), "WIN LEVELS TO COLLECT");
        DialogUI.Label(HierarchyReferenceCache.Descendant(_info.transform, "Text2"), "CONSECUTIVE WINS INCREASE YOUR MULTIPLIER");
        DialogUI.Label(HierarchyReferenceCache.Descendant(_info.transform, "Text3"), "RETRY OR QUIT RESETS THE MULTIPLIER. REVIVE KEEPS IT.");

        var manager = SpecialStreakManager.Ensure();
        if (manager.ShouldShowTutorial())
        {
            var bgImg = _info.GetComponent<Image>();
            if (bgImg != null) bgImg.enabled = false;
        }

        await UniTask.WaitUntil(() => _info == null);
        CheckTutorialRewardScroll().Forget();
    }

    private void CloseInfo()
    {
        if (_info != null)
        {
            var content = _info.GetComponent<EventInfoDialog>();
            if (content != null) content.Dismissed -= OnInfoDismissed;
            Destroy(_info);
        }
        _info = null;
    }

    private void OnInfoDismissed(int result) => CloseInfo();

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
