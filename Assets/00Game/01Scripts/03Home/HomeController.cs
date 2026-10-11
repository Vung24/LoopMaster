using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Snui;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public class HomeController : MonoBehaviour
{
	public const int SHOP_PAGE_INDEX = 0, RANK_PAGE_INDEX = 1, HOME_PAGE_INDEX = 2, TEAM_PAGE_INDEX = 3;
	public static HomeController Current { get; private set; }
	[SerializeField] private NavigationBar _navigationBar;
	[SerializeField] private RectTransform _pageContent;
	[SerializeField] private RectTransform[] _pages;
	private Tween _pageTween;
	private Vector2 _pageSize;
	public HomePage HomePage;
	private Canvas _canvas;
	private Button _settingButton;
	private Button _heartButton;
	private MoreHeartDialogContent _heartDialog;
	private DailyCheckInHomeUI _dailyCheckIn;
	public bool HasDailyCheckInOpen => _dailyCheckIn != null && _dailyCheckIn.IsDialogOpen;
	private bool _heartLoading, _settingsLoading;
	private int _uiLoadVersion;
	public bool HasHeartDialogOpen => _heartLoading || _heartDialog != null;
	private SettingsDialog _settings;
	private LevelBoxMachine _levelBox;
	private SpecialStreakProgress _specialStreak;
	public bool HasSpecialStreakOpen => _specialStreak != null && (_specialStreak.HasDialogOpen || _specialStreak.IsAnimating);
	public bool CanOpenSpecialStreak => CurrentPageIndex == HOME_PAGE_INDEX && !_openingGame && !HasSettingsOpen && !HasDailyCheckInOpen && !HasHeartDialogOpen && !HasLevelBoxOpen && !HasSpecialStreakOpen && isActiveAndEnabled;
	public bool HasLevelBoxOpen => _levelBox != null && _levelBox.HasDialogOpen;
	public bool CanOpenLevelBox(LevelBoxMachine machine) => CurrentPageIndex == HOME_PAGE_INDEX && !_openingGame && !HasSettingsOpen && !HasDailyCheckInOpen && !HasHeartDialogOpen && !HasLevelBoxOpen && !HasSpecialStreakOpen && isActiveAndEnabled;
	private bool _ready, _openingGame;
	public int CurrentPageIndex { get; private set; } = HOME_PAGE_INDEX;
	public bool HasSettingsOpen => _settingsLoading || (_settings != null && _settings.gameObject.activeSelf);

	public static HomeController ForScene(Scene scene)
	{
		if (Current != null && Current.gameObject.scene == scene) return Current;
		if (!scene.IsValid() || !scene.isLoaded) return null;
		foreach (var root in scene.GetRootGameObjects())
		{
			var controller = root.GetComponentInChildren<HomeController>(true);
			if (controller != null) return controller;
		}
		return null;
	}
	private T GetComponentInScene<T>() where T : Component
	{
		foreach (var root in gameObject.scene.GetRootGameObjects())
		{
			T result = root.GetComponentInChildren<T>(true);
			if (result != null) return result;
		}
		return null;
	}
	private void Awake() { Current = this; }
	private void Start() => Initialize();
	public void Initialize()
	{
		EventRuntime.EnsureInitialized();
		LevelBoxManager.EnsureInitialized();
		if (_ready) return;
		if (HomePage == null) HomePage = GetComponentInScene<HomePage>();
		if (HomePage == null) return;
		_canvas = HomePage.GetComponentInParent<Canvas>()?.rootCanvas;
		if (_canvas == null) return;
		_ready = true;
		var scaler = _canvas.GetComponent<CanvasScaler>() ?? _canvas.gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(750f, 1624f);
		scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; scaler.matchWidthOrHeight = 0f;
		if (_canvas.GetComponent<GraphicRaycaster>() == null) _canvas.gameObject.AddComponent<GraphicRaycaster>();
		InitializePages();
		if (_navigationBar == null) _navigationBar = _canvas.GetComponentInChildren<NavigationBar>(true);
		if (_navigationBar != null)
		{
			_navigationBar.SetEnabledPages(SHOP_PAGE_INDEX, RANK_PAGE_INDEX, HOME_PAGE_INDEX, TEAM_PAGE_INDEX);
			_navigationBar.OnClickCell.AddListener(OnClickNavigationCell);
			_navigationBar.Init(HOME_PAGE_INDEX);
		}
		_settingButton = HomePage.SettingButton
			?? HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Descendant(HomePage.transform, "SettingBtn"));
		if (_settingButton == null) _settingButton = HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Descendant(_canvas.transform, "SettingBtn"));
		if (_settingButton != null) _settingButton.onClick.AddListener(OpenSettings);
		HomePage.RefreshProgress();
		_levelBox = HomePage.GetComponentInChildren<LevelBoxMachine>(true);
		if (_levelBox != null && !_levelBox.HasAuthoredLayout && PrefabLibrary.Current?.LevelBoxMachinePrefab != null)
		{
			Transform slot = _levelBox.transform;
			var restored = Instantiate(PrefabLibrary.Current.LevelBoxMachinePrefab, slot.parent, false);
			var target = restored.transform as RectTransform; var source = slot as RectTransform;
			if (target != null && source != null)
			{
				target.anchorMin=source.anchorMin; target.anchorMax=source.anchorMax; target.pivot=source.pivot;
				target.sizeDelta=source.sizeDelta;
				target.anchoredPosition3D=source.anchoredPosition3D; target.localScale=source.localScale;
			}
			restored.transform.SetSiblingIndex(slot.GetSiblingIndex());
			slot.gameObject.SetActive(false); Destroy(slot.gameObject);
			_levelBox = restored.GetComponent<LevelBoxMachine>();
		}
		if (_levelBox == null && PrefabLibrary.Current?.LevelBoxMachinePrefab != null)
			_levelBox = Instantiate(PrefabLibrary.Current.LevelBoxMachinePrefab, HomePage.transform, false).GetComponent<LevelBoxMachine>();
		if (_levelBox != null) { _levelBox.gameObject.SetActive(true); _levelBox.Bind(this); }
		_specialStreak = HomePage.GetComponentInChildren<SpecialStreakProgress>(true);
		if ((_specialStreak == null || !_specialStreak.HasAuthoredLayout) && PrefabLibrary.Current?.SpecialStreakProgressPrefab != null)
		{
			Transform slot = _specialStreak == null ? HierarchyReferenceCache.Descendant(HomePage.transform, "SpecialStreakWidget") : _specialStreak.transform;
			var restored = Instantiate(PrefabLibrary.Current.SpecialStreakProgressPrefab, slot == null ? HomePage.transform : slot.parent, false);
			var target = restored.transform as RectTransform; var source = slot as RectTransform;
			if (target != null && source != null)
			{
				target.anchorMin=source.anchorMin; target.anchorMax=source.anchorMax; target.pivot=source.pivot;
				target.sizeDelta=source.sizeDelta;
				target.anchoredPosition3D=source.anchoredPosition3D; target.localScale=source.localScale;
			}
			if (slot != null) { restored.transform.SetSiblingIndex(slot.GetSiblingIndex()); slot.gameObject.SetActive(false); Destroy(slot.gameObject); }
			_specialStreak = restored.GetComponent<SpecialStreakProgress>();
		}
		if (_specialStreak != null) { _specialStreak.gameObject.SetActive(true); _specialStreak.Bind(this, false); }
		if (DailyCheckInManager.IsOpen)
		{
			_dailyCheckIn = HomePage.GetComponent<DailyCheckInHomeUI>();
			if (_dailyCheckIn == null) _dailyCheckIn = HomePage.gameObject.AddComponent<DailyCheckInHomeUI>();
			_dailyCheckIn.Bind(this);
		}
		HudContainer.Show(_canvas.transform).Forget();
		var hud = HudContainer.Current;
		if (hud != null)
		{
			hud.CoinHud?.EnableClick();
			_heartButton = hud.HeartHud == null ? null : hud.HeartHud.GetComponent<Button>();
			if (_heartButton != null) { _heartButton.interactable = true; _heartButton.onClick.AddListener(OpenHeartDialog); }
		}
	}
	private void InitializePages()
	{
		if (_pageContent == null) _pageContent = HomePage.transform.parent as RectTransform;
		if (_pageContent == null || !(_pageContent.parent is RectTransform)) return;
		if (_pages == null || _pages.Length == 0)
			_pages = new[] { GetComponentInScene<ShopPage>()?.transform as RectTransform,
				GetComponentInScene<RankPage>()?.transform as RectTransform, HomePage.transform as RectTransform,
				GetComponentInScene<TeamPage>()?.transform as RectTransform };
		foreach (Behaviour layout in _pageContent.GetComponents<Behaviour>())
			if (layout is LayoutGroup || layout is ContentSizeFitter) layout.enabled = false;
		_pageContent.anchorMin = Vector2.zero; _pageContent.anchorMax = new Vector2(0f, 1f);
		_pageContent.pivot = new Vector2(0f, 0.5f);
		Canvas.ForceUpdateCanvases();
		LayoutPages();
		FinishPageTransition();
	}
	private void LayoutPages()
	{
		_pageTween?.Kill(); _pageTween = null;
		_pageSize = ((RectTransform)_pageContent.parent).rect.size;
		_pageContent.sizeDelta = new Vector2(_pageSize.x * _pages.Length, 0f);
		for (int i = 0; i < _pages.Length; i++)
		{
			if (_pages[i] == null) continue;
			var page = _pages[i];
			page.anchorMin = Vector2.zero; page.anchorMax = new Vector2(0f, 1f); page.pivot = new Vector2(0f, 0.5f);
			page.sizeDelta = new Vector2(_pageSize.x, 0f); page.anchoredPosition = new Vector2(_pageSize.x * i, 0f);
		}
		_pageContent.anchoredPosition = new Vector2(-_pageSize.x * CurrentPageIndex, 0f);
	}
	private void LateUpdate()
	{
		if (!_ready || _pageContent == null || _pages == null) return;
		if (((RectTransform)_pageContent.parent).rect.size != _pageSize) { LayoutPages(); FinishPageTransition(); }
	}
	private void FinishPageTransition()
	{
		if (_pages == null) return;
		for (int i = 0; i < _pages.Length; i++)
			if (_pages[i] != null) _pages[i].gameObject.SetActive(i == CurrentPageIndex);
		_pageTween = null;
	}
	private void SelectPage(int index, bool animate)
	{
		Initialize();
		if (!_ready || _openingGame || HasSettingsOpen || HasDailyCheckInOpen || HasHeartDialogOpen || HasLevelBoxOpen || HasSpecialStreakOpen || !isActiveAndEnabled
			|| _pages == null || index < 0 || index >= _pages.Length || _pages[index] == null) return;
		if (animate && index == CurrentPageIndex) return;
		_pageTween?.Kill();
		int previous = CurrentPageIndex;
		CurrentPageIndex = index;
		for (int i = 0; i < _pages.Length; i++)
		{
			if (_pages[i] == null) continue;
			var group = _pages[i].GetComponent<CanvasGroup>();
			if (group == null) group = _pages[i].gameObject.AddComponent<CanvasGroup>();
			group.interactable = group.blocksRaycasts = i == index;
			bool crossed = i >= Mathf.Min(previous, index) && i <= Mathf.Max(previous, index);
			if (i == index || (animate && crossed)) _pages[i].gameObject.SetActive(true);
		}
		if (previous != index) _pages[index].GetComponent<Sugar.UI.PageViewCell>()?.OnEnterPage?.Invoke();
		if (animate)
		{
			_navigationBar?.SwitchPage(index);
			_pageTween = _pageContent.DOAnchorPosX(-_pageSize.x * index, 0.2f).SetUpdate(true).OnComplete(FinishPageTransition);
		}
		else
		{
			_navigationBar?.JumpToPage(index);
			_pageContent.anchoredPosition = new Vector2(-_pageSize.x * index, 0f);
			FinishPageTransition();
		}
	}
	private void OnClickNavigationCell(int index)
	{
		SelectPage(index, true);
	}
	public void JumpToPage(int index)
	{
		SelectPage(index, false);
	}
	public void SetPageIndex(int index) => JumpToPage(index);
	public bool TryOpenPendingLevelBox()
	{
		if (!_ready || _levelBox == null || !_levelBox.isActiveAndEnabled || !CanOpenLevelBox(_levelBox)
			|| EventPrefabUI.HasOpenDialogs || !_levelBox.IsGetRewards()) return false;
		_levelBox.OnClickBtn();
		return HasLevelBoxOpen;
	}
	public void OpenHeartDialog() => LoadHeartDialog().Forget();
	private async UniTask LoadHeartDialog()
	{
		Initialize();
		if (!_ready || !CanOpenLevelBox(_levelBox)) return;
		int version = _uiLoadVersion;
		_heartLoading = true;
		try
		{
			var content = await MoreHeartDialogContent.Show(_canvas.transform, JumpHeartDialogSourceScene.HudIn);
			if (this == null || version != _uiLoadVersion || !isActiveAndEnabled)
			{ if (content != null) content.CloseOverlay(); return; }
			_heartDialog = content;
			if (_heartDialog != null) _heartDialog.Closed += OnHeartDialogClosed;
		}
		finally { if (this != null && version == _uiLoadVersion) _heartLoading = false; }
	}
	private void OnHeartDialogClosed()
	{
		if (_heartDialog != null) _heartDialog.Closed -= OnHeartDialogClosed;
		_heartDialog = null;
	}
	public void OpenSettings() => LoadSettings().Forget();
	private async UniTask LoadSettings()
	{
		Initialize();
		if (!_ready || CurrentPageIndex != HOME_PAGE_INDEX || _openingGame || HasSettingsOpen || HasDailyCheckInOpen || HasHeartDialogOpen || HasLevelBoxOpen || HasSpecialStreakOpen || !isActiveAndEnabled) return;
		int version = _uiLoadVersion;
		_settingsLoading = true;
		try
		{
			var settings = await SettingsDialog.Show(_canvas.transform);
			if (this == null || version != _uiLoadVersion || !isActiveAndEnabled)
			{ if (settings != null) Destroy(settings.gameObject); return; }
			_settings = settings;
			if (_settings != null) _settings.Closed += OnSettingsClosed;
		}
		finally { if (this != null && version == _uiLoadVersion) _settingsLoading = false; }
	}
	private void OnSettingsClosed() { if (_settings != null) _settings.Closed -= OnSettingsClosed; _settings = null; }
	public bool TryEnterGame()
	{
		if (HomePage != null && HomePage.IsEntryBlocked) return false;
		if (Managers.Heart != null && !Managers.Heart.IsHeartEnough()) return false;
		if (_openingGame || CurrentPageIndex != HOME_PAGE_INDEX || HasSettingsOpen || HasDailyCheckInOpen || HasHeartDialogOpen || HasLevelBoxOpen || HasSpecialStreakOpen || !isActiveAndEnabled) return false;
		if (GameProgressStore.IsCampaignComplete || !SpecialStreakManager.Ensure().SettlePending()) return false;
		var catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
		if (catalog == null || !catalog.HasLevel(GameProgressStore.NextPlayableLevel)) { HomePage.RefreshProgress(); return false; }
		if (HomePage != null && !HomePage.CommitProgressBeforeEnterGame()) return false;
		_openingGame = true;
		HomePage?.SetLeavingHome(true);
		_specialStreak?.CommitProgressBeforeEnterGame();
		GameData.ClearCurrent(); Time.timeScale = 1f;
		EnterGameAsync().Forget();
		return true;
	}
	private async UniTask EnterGameAsync()
	{
		try
		{
			await GameSceneManager.EnterGame(GameEnterRoute.Home);
		}
		finally
		{
			if (this != null)
			{
				_openingGame = false;
				HomePage?.SetLeavingHome(false);
			}
		}
	}
	private void OnDisable()
	{
		_uiLoadVersion++;
		_heartLoading = _settingsLoading = false;
		_pageTween?.Kill(); _pageTween = null;
		if (_heartDialog != null) { _heartDialog.Closed -= OnHeartDialogClosed; _heartDialog.CloseOverlay(); }
		_heartDialog = null;
		if (_levelBox != null) _levelBox.gameObject.SetActive(false);
		if (_specialStreak != null) _specialStreak.gameObject.SetActive(false);
		if (_settings != null) { _settings.Closed -= OnSettingsClosed; _settings.gameObject.SetActive(false); Destroy(_settings.gameObject); }
		_settings = null;
	}
	private void OnDestroy()
	{
		OnDisable();
		if (_navigationBar != null) _navigationBar.OnClickCell.RemoveListener(OnClickNavigationCell);
		if (_settingButton != null) _settingButton.onClick.RemoveListener(OpenSettings);
		if (_heartButton != null) _heartButton.onClick.RemoveListener(OpenHeartDialog);
		if (Current == this) Current = null;
	}
}
