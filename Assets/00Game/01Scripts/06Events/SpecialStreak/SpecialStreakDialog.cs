using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Snui;
using TMPro;
using UnityEngine.UI;

public class SpecialStreakDialog : MonoBehaviour
{
    [SerializeField] private Button _infoBtn, _closeBtn;
    [SerializeField] private SpecialStreakMultiplierSlider _multiplierSlider;
    [SerializeField] private ProgressBar _progressBar;
    [SerializeField] private RewardCell _rewardCell;
    [SerializeField] private Transform _completedTickNode;
    [SerializeField] private Timer _timer;
    [SerializeField] private TMP_Text _timerText;
    private readonly List<Material> _materials = new List<Material>();
    private readonly List<SpecialStreakCell> _cells = new List<SpecialStreakCell>();
    private GameObject _info;
    private bool _closed;
    public event Action Closed;
    public static UniTask<SpecialStreakDialog> Show(Transform parent)
    {
        var prefab = RecoveryPrefabLibrary.Current?.SpecialStreakDialogPrefab;
        if (parent == null || prefab == null) return UniTask.FromResult<SpecialStreakDialog>(null);
        var instance = Instantiate(prefab, parent, false);
        RewardDialog.PrepareOverlay(instance, 1350);
        var dialog = instance.GetComponent<SpecialStreakDialog>();
        if (dialog == null) { Destroy(instance); return UniTask.FromResult<SpecialStreakDialog>(null); }
        dialog.Refresh(); return UniTask.FromResult(dialog);
    }
    private void Awake()
    {
        SpecialStreakPresentation.Prepare(transform, _materials);
        if (_closeBtn != null) _closeBtn.onClick.AddListener(Close);
        if (_infoBtn != null) _infoBtn.onClick.AddListener(OnClickInfoBtn);
        SpecialStreakManager.Ensure().Changed += Refresh;
        BuildRows();
    }
    private void BuildRows()
    {
        var prefab = RecoveryPrefabLibrary.Current?.SpecialStreakCellPrefab;
        var content = HierarchyReferenceCache.Path(transform, "SafeArea", "ScrollNode", "TableView", "Viewport", "Content") as RectTransform;
        if (content == null) content = HierarchyReferenceCache.Descendant(transform, "Content") as RectTransform;
        if (content == null || prefab == null) return;
        var manager = SpecialStreakManager.Ensure();
        var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true; layout.childControlHeight = false; layout.childForceExpandHeight = false; layout.spacing = 0;
        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var old in content.GetComponentsInChildren<SpecialStreakCell>(true)) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
        for (int i = 0; i < manager.GetRewardInfosConfig().Count; i++)
        {
            var root = Instantiate(prefab, content, false);
            var cell = root.GetComponent<SpecialStreakCell>();
            if (cell != null) { cell.Init(i); _cells.Add(cell); }
            var size = root.transform as RectTransform;
            var element = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            element.preferredHeight = size == null ? 160 : Mathf.Max(100, size.sizeDelta.y);
        }
        var scrollRoot = content.parent?.parent;
        if (scrollRoot != null)
        {
            var scroll = scrollRoot.GetComponent<ScrollRect>() ?? scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = content.parent as RectTransform; scroll.horizontal = false; scroll.vertical = true;
            scroll.verticalNormalizedPosition = 1;
        }
    }
    public void Refresh()
    {
        var manager = SpecialStreakManager.Ensure(); var stage = manager.GetCurrRewardInfo();
        _multiplierSlider?.Init(manager.MultiplierIndex);
        _progressBar?.SetPercent(manager.CurrCount, stage?.Goal ?? 0);
        SpecialStreakPresentation.SetActive(_completedTickNode, manager.IsCompleted());
        SpecialStreakPresentation.SetActive(_timer, false);
        if (_timerText != null) _timerText.text = manager.IsCompleted() ? "COMPLETED" : "OFFLINE — NO TIME LIMIT";
        if (_rewardCell != null) { _rewardCell.gameObject.SetActive(stage != null); if (stage != null) _rewardCell.Init(stage.Items()[0], true, false).Forget(); }
        for (int i = 0; i < _cells.Count; i++) _cells[i].Init(i);
    }
    public void OnClickInfoBtn()
    {
        var prefab = RecoveryPrefabLibrary.Current?.SpecialStreakInfoDialogPrefab;
        if (_info != null || prefab == null || _closed) return;
        _info = Instantiate(prefab, transform.parent, false); RewardDialog.PrepareOverlay(_info, 1400);
        SpecialStreakPresentation.Prepare(_info.transform, _materials);
        var button = HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Descendant(_info.transform, "ContinueButton"));
        if (button != null) button.onClick.AddListener(CloseInfo);
        // Instructions come from native intent; localization service is outside the offline recovery.
        RecoveryDialogUI.Label(HierarchyReferenceCache.Descendant(_info.transform, "Text1"), "WIN LEVELS TO COLLECT");
        RecoveryDialogUI.Label(HierarchyReferenceCache.Descendant(_info.transform, "Text2"), "CONSECUTIVE WINS INCREASE YOUR MULTIPLIER");
        RecoveryDialogUI.Label(HierarchyReferenceCache.Descendant(_info.transform, "Text3"), "RETRY OR QUIT RESETS THE MULTIPLIER. REVIVE KEEPS IT.");
    }
    private void CloseInfo() { if (_info != null) Destroy(_info); _info = null; }
    private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) { if (_info != null) CloseInfo(); else Close(); } }
    public void Close() { if (_closed) return; _closed = true; Closed?.Invoke(); gameObject.SetActive(false); Destroy(gameObject); }
    private void OnDisable() { if (SpecialStreakManager.Current != null) SpecialStreakManager.Current.Changed -= Refresh; CloseInfo(); if (!_closed) { _closed = true; Closed?.Invoke(); } }
    private void OnDestroy()
    {
        if (SpecialStreakManager.Current != null) SpecialStreakManager.Current.Changed -= Refresh;
        if (_closeBtn != null) _closeBtn.onClick.RemoveListener(Close);
        if (_infoBtn != null) _infoBtn.onClick.RemoveListener(OnClickInfoBtn);
        SpecialStreakPresentation.Cleanup(_materials);
    }
}
