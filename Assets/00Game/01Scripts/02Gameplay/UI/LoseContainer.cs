using Spine.Unity;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoseContainer : MonoBehaviour
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
    private readonly List<GameObject> _loseItemPrefabs = new List<GameObject>();
    private GameObject _currentLoseItem;
    private Coroutine _switchRoutine;
    private int _loseIndex;
    private int _maxShowLoseCount;
    private bool _isSwitching;

    // Ghidra 0x1779FE8: stars, lava, streak line, hearts, special streak.
    public void SetPauseLoseItems(int maxShowCount = 3)
    {
        ClearLoseItems();
        _maxShowLoseCount = Mathf.Max(1, maxShowCount);
        AddLoseItem(_loseStarsPrefab, true);
        AddLoseItem(_loseLavaPrefab, Managers.Lava != null && Managers.Lava.IsOngoing() && !Managers.Lava.IsInCd());
        var streak = Managers.StreakLine;
        int checkpoint = 0;
        if (streak != null)
            foreach (var chest in streak.GetChests())
                if (chest != null && chest.Level <= streak.GetCurrLevel()) checkpoint = Mathf.Max(checkpoint, chest.Level);
        AddLoseItem(_loseStreakLinePrefab, streak != null && streak.IsOngoing && streak.GetCurrLevel() > checkpoint);
        AddLoseItem(_loseHeartsPrefab, Managers.Heart != null && !Managers.Heart.IsInfinite());
        AddLoseItem(_loseSpecialStreakPrefab, SpecialStreakManager.Ensure().ShouldShowLoseItem());
        if (_loseItemPrefabs.Count > 0) CreateLoseItem();
    }

    private void AddLoseItem(GameObject prefab, bool condition)
    {
        if (condition && prefab != null) _loseItemPrefabs.Add(prefab);
    }

    // Ghidra 0x177A4C8: consume clicks during switching; dismiss only on the final page.
    public bool OnClickSwitchNext()
    {
        if (_isSwitching) return true;
        if (_currentLoseItem == null || _loseIndex >= _loseItemPrefabs.Count - 1 ||
            _loseIndex >= _maxShowLoseCount - 1) return false;
        _isSwitching = true;
        _switchRoutine = StartCoroutine(SwitchNext());
        return true;
    }

    private void CreateLoseItem()
    {
        _currentLoseItem = Instantiate(_loseItemPrefabs[_loseIndex], _loseBg != null ? _loseBg : transform, false);
        _currentLoseItem.transform.localPosition = Vector3.zero;
        Image icon = HierarchyReferenceCache.ChildComponent<Image>(_currentLoseItem.transform, "Icon")
            ?? HierarchyReferenceCache.ChildComponent<Image>(_currentLoseItem.transform, "Star");
        if (icon != null && icon.sprite != null)
        {
            icon.preserveAspect = true;
            icon.SetNativeSize();
        }
        Snui.Snui_Text.EnsureFontAssets(_currentLoseItem.transform);
        var slider = _currentLoseItem.GetComponentInChildren<SpecialStreakMultiplierSlider>(true);
        if (slider != null)
        {
            slider.Type = SpecialStreakMultiplierSliderType.PreLose;
            slider.Init(SpecialStreakManager.Ensure().MultiplierIndex);
        }
    }

    private IEnumerator SwitchNext()
    {
        _itemTween = _currentLoseItem.transform.DOScale(0f, .16f).SetEase(Ease.OutQuad).SetUpdate(true);
        yield return _itemTween.WaitForCompletion();
        _currentLoseItem.SetActive(false);
        Destroy(_currentLoseItem);
        _loseIndex++;
        CreateLoseItem();
        _currentLoseItem.transform.localScale = Vector3.zero;
        _itemTween = _currentLoseItem.transform.DOScale(1f, .16f).SetEase(Ease.OutQuad).SetUpdate(true);
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
        _loseItemPrefabs.Clear();
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
            _backgroundTween = _loseBg.DOScale(1f, 0.16f).SetEase(Ease.OutQuad).SetUpdate(true);
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
            _mascotFade = group.DOFade(1f, 0.1f).SetUpdate(true);
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

    private void CancelTweens()
    {
        _backgroundTween?.Kill();
        _mascotFade?.Kill();
        _backgroundTween = _mascotFade = null;
    }
}
