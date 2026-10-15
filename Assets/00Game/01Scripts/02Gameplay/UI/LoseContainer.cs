using Spine.Unity;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoseContainer : MonoBehaviour, ILoseItemContainer
{
    [SerializeField] private SkeletonGraphic _ipSpine;
    [SerializeField] private Transform _loseBg;
    [SerializeField] private string _entranceAnimation = "entrance";
    [SerializeField] private string _idleAnimation = "idle";
    [SerializeField] private GameObject _loseStarsPrefab;
    [SerializeField] private GameObject _loseHeartsPrefab;
    [SerializeField] private GameObject _loseStreakLinePrefab;
    [SerializeField] private GameObject _loseLavaPrefab;
    [SerializeField] private GameObject _loseSpecialStreakPrefab;
    private Tween _backgroundTween;
    private Tween _mascotFade;
    private Tween _itemTween;
    public GameObject LoseStarsPrefab => _loseStarsPrefab;
    public GameObject LoseHeartsPrefab => _loseHeartsPrefab;
    public GameObject LoseStreakLinePrefab => _loseStreakLinePrefab;
    public GameObject LoseLavaPrefab => _loseLavaPrefab;
    public GameObject LoseSpecialStreakPrefab => _loseSpecialStreakPrefab;

    private readonly struct LoseItemEntry
    {
        public readonly ILoseItemProvider Provider;
        public readonly GameObject Prefab;

        public LoseItemEntry(ILoseItemProvider provider, GameObject prefab)
        {
            Provider = provider;
            Prefab = prefab;
        }
    }

    private readonly List<LoseItemEntry> _loseItemEntries = new List<LoseItemEntry>();
    private GameObject _currentLoseItem;
    private Coroutine _switchRoutine;
    private int _loseIndex;
    private int _maxShowLoseCount;
    private bool _isSwitching;

    private void EnsureDefaultProviders()
    {
        if (!LoseItemRegistry.HasProvider(StarsLoseItemProvider.ProviderId))
            LoseItemRegistry.Register(new StarsLoseItemProvider());
    }

    // Ghidra 0x1779FE8: stars, lava, streak line, hearts, special streak.
    public void SetPauseLoseItems(int maxShowCount = 3)
    {
        ClearLoseItems();
        _maxShowLoseCount = Mathf.Max(1, maxShowCount);
        EnsureDefaultProviders();

        var providers = LoseItemRegistry.GetOrderedProviders();
        for (int i = 0; i < providers.Count; i++)
        {
            var provider = providers[i];
            if (provider == null || !provider.IsAvailable()) continue;
            GameObject prefab = provider.GetPrefab(this);
            if (prefab != null)
            {
                _loseItemEntries.Add(new LoseItemEntry(provider, prefab));
            }
        }

        if (_loseItemEntries.Count > 0) CreateLoseItem();
    }

    // Ghidra 0x177A4C8: consume clicks during switching; dismiss only on the final page.
    public bool OnClickSwitchNext()
    {
        if (_isSwitching) return true;
        if (_currentLoseItem == null || _loseIndex >= _loseItemEntries.Count - 1 ||
            _loseIndex >= _maxShowLoseCount - 1) return false;
        _isSwitching = true;
        _switchRoutine = StartCoroutine(SwitchNext());
        return true;
    }

    private void CreateLoseItem()
    {
        if (_loseIndex < 0 || _loseIndex >= _loseItemEntries.Count) return;
        var entry = _loseItemEntries[_loseIndex];
        _currentLoseItem = Instantiate(entry.Prefab, _loseBg != null ? _loseBg : transform, false);
        _currentLoseItem.transform.localPosition = Vector3.zero;
        Image icon = HierarchyReferenceCache.ChildComponent<Image>(_currentLoseItem.transform, "Icon")
            ?? HierarchyReferenceCache.ChildComponent<Image>(_currentLoseItem.transform, "Star");
        if (icon != null && icon.sprite != null)
        {
            icon.preserveAspect = true;
            icon.SetNativeSize();
        }
        Snui.Snui_Text.EnsureFontAssets(_currentLoseItem.transform);
        entry.Provider.OnItemInstantiated(_currentLoseItem);
    }

    private IEnumerator SwitchNext()
    {
        _itemTween = _currentLoseItem.transform.DOScale(0f, .16f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
        yield return _itemTween.WaitForCompletion();
        _currentLoseItem.SetActive(false);
        Destroy(_currentLoseItem);
        _loseIndex++;
        CreateLoseItem();
        _currentLoseItem.transform.localScale = Vector3.zero;
        _itemTween = _currentLoseItem.transform.DOScale(1f, .16f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
        yield return _itemTween.WaitForCompletion();
        _itemTween = null;
        _switchRoutine = null;
        _isSwitching = false;
    }

    private void ClearLoseItems()
    {
        if (_switchRoutine != null) StopCoroutine(_switchRoutine);
        _switchRoutine = null;
        _itemTween?.Kill();
        _itemTween = null;
        _isSwitching = false;
        _loseIndex = 0;
        _loseItemEntries.Clear();
        if (_currentLoseItem != null)
        {
            _currentLoseItem.SetActive(false);
            Destroy(_currentLoseItem);
        }
        _currentLoseItem = null;
    }

    private void OnEnable() { Show(); }

    public void Show()
    {
        CancelTweens();
        if (_loseBg != null)
        {
            _loseBg.gameObject.SetActive(true);
            _loseBg.localScale = Vector3.zero;
            _backgroundTween = _loseBg.DOScale(1f, 0.16f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
        }
        if (_ipSpine == null) _ipSpine = GetComponentInChildren<SkeletonGraphic>(true);
        if (_ipSpine == null || _ipSpine.skeletonDataAsset == null) return;
        _ipSpine.gameObject.SetActive(true);
        CanvasGroup group = _ipSpine.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            _mascotFade = group.DOFade(1f, 0.1f).SetUpdate(true).SetLink(gameObject);
        }
        _ipSpine.UnscaledTime = true;
        _ipSpine.Initialize(false);
        var data = _ipSpine.skeletonDataAsset.GetSkeletonData(false);
        if (data == null || _ipSpine.AnimationState == null) return;
        _ipSpine.Skeleton.SetToSetupPose();
        _ipSpine.AnimationState.ClearTracks();
        if (data.FindAnimation(_entranceAnimation) != null)
        {
            _ipSpine.AnimationState.SetAnimation(0, _entranceAnimation, false);
            if (data.FindAnimation(_idleAnimation) != null)
                _ipSpine.AnimationState.AddAnimation(0, _idleAnimation, true, 0f);
        }
        else if (data.FindAnimation(_idleAnimation) != null)
            _ipSpine.AnimationState.SetAnimation(0, _idleAnimation, true);
    }

    private void OnDisable()
    {
        ClearLoseItems();
        CancelTweens();
        if (_ipSpine != null && _ipSpine.AnimationState != null) _ipSpine.AnimationState.ClearTracks();
    }

    private void OnDestroy()
    {
        ClearLoseItems();
        CancelTweens();
    }

    private void CancelTweens()
    {
        _itemTween?.Kill();
        _itemTween = null;
        _backgroundTween?.Kill();
        _mascotFade?.Kill();
        _backgroundTween = _mascotFade = null;
    }
}
