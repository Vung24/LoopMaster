using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Snui;
using Cysharp.Threading.Tasks;

public class HomeController : MonoBehaviour
{
	public const int SHOP_PAGE_INDEX = 0, RANK_PAGE_INDEX = 1, HOME_PAGE_INDEX = 2, TEAM_PAGE_INDEX = 3;
	public static HomeController Current { get; private set; }
	[SerializeField] private NavigationBar _navigationBar;
	public HomePage HomePage;
	private Canvas _canvas;
	private Button _settingButton;
	private SettingsDialog _settings;
	private LevelBoxMachine _levelBox;
	private SpecialStreakProgress _specialStreak;
	public bool HasSpecialStreakOpen => _specialStreak != null && (_specialStreak.HasDialogOpen || _specialStreak.IsAnimating);
	public bool CanOpenSpecialStreak => !_openingGame && !HasSettingsOpen && !HasLevelBoxOpen && !HasSpecialStreakOpen && isActiveAndEnabled;
	public bool HasLevelBoxOpen => _levelBox != null && _levelBox.HasDialogOpen;
	public bool CanOpenLevelBox(LevelBoxMachine machine) => !_openingGame && !HasSettingsOpen && !HasLevelBoxOpen && !HasSpecialStreakOpen && isActiveAndEnabled;
	private bool _ready, _openingGame;
	public int CurrentPageIndex { get; private set; } = HOME_PAGE_INDEX;
	public bool HasSettingsOpen => _settings != null && _settings.gameObject.activeSelf;

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
		LevelBoxManager.EnsureInitialized();
		if (_ready) return;
		if (HomePage == null) HomePage = GetComponentInScene<HomePage>();
		if (HomePage == null) return;
		_canvas = HomePage.GetComponentInParent<Canvas>()?.rootCanvas;
		if (_canvas == null) return;
		_ready = true;
		var scaler = _canvas.GetComponent<CanvasScaler>() ?? _canvas.gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		// UiUtils::.cctor in the native build initializes the Home design to 750 x 1334.
		scaler.referenceResolution = new Vector2(750f, 1334f); scaler.matchWidthOrHeight = 0f;
		// Home remains the only available offline page; preserve the exported viewport fix.
		RectTransform strip = HomePage.transform.parent as RectTransform;
		if (strip != null && strip.name == "Content" && strip.parent != null && strip.parent.name == "Viewport")
		{
			foreach (Behaviour layout in strip.GetComponents<Behaviour>())
				if (layout is LayoutGroup || layout is ContentSizeFitter) layout.enabled = false;
			foreach (Transform page in strip) if (page != HomePage.transform) page.gameObject.SetActive(false);
			Stretch(strip); Stretch(HomePage.transform as RectTransform);
		}
		if (_navigationBar == null) _navigationBar = _canvas.GetComponentInChildren<NavigationBar>(true);
		if (_navigationBar != null)
		{
			_navigationBar.SetEnabledPages(HOME_PAGE_INDEX);
			_navigationBar.OnClickCell.AddListener(OnClickNavigationCell);
			_navigationBar.Init(HOME_PAGE_INDEX);
		}
		_settingButton = HomePage.SettingButton
			?? HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Descendant(HomePage.transform, "SettingBtn"));
		if (_settingButton == null) _settingButton = HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Descendant(_canvas.transform, "SettingBtn"));
		if (_settingButton != null) _settingButton.onClick.AddListener(OpenSettings);
		HomePage.RefreshProgress();
		_levelBox = HomePage.GetComponentInChildren<LevelBoxMachine>(true);
		if (_levelBox != null && !_levelBox.HasAuthoredLayout && RecoveryPrefabLibrary.Current?.LevelBoxMachinePrefab != null)
		{
			// Restore legacy scenes that still contain a field-less LevelBox placeholder.
			Transform slot = _levelBox.transform;
			var restored = Instantiate(RecoveryPrefabLibrary.Current.LevelBoxMachinePrefab, slot.parent, false);
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
		if (_levelBox == null && RecoveryPrefabLibrary.Current?.LevelBoxMachinePrefab != null)
			_levelBox = Instantiate(RecoveryPrefabLibrary.Current.LevelBoxMachinePrefab, HomePage.transform, false).GetComponent<LevelBoxMachine>();
		if (_levelBox != null) { _levelBox.gameObject.SetActive(true); _levelBox.Bind(this); }
		_specialStreak = HomePage.GetComponentInChildren<SpecialStreakProgress>(true);
		if ((_specialStreak == null || !_specialStreak.HasAuthoredLayout) && RecoveryPrefabLibrary.Current?.SpecialStreakProgressPrefab != null)
		{
			Transform slot = _specialStreak == null ? HierarchyReferenceCache.Descendant(HomePage.transform, "SpecialStreakWidget") : _specialStreak.transform;
			var restored = Instantiate(RecoveryPrefabLibrary.Current.SpecialStreakProgressPrefab, slot == null ? HomePage.transform : slot.parent, false);
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
		if (_specialStreak != null) { _specialStreak.gameObject.SetActive(true); _specialStreak.Bind(this); }
		HudContainer.Show(_canvas.transform).Forget(); HudContainer.Current?.DisableClick();
	}
	private static void Stretch(RectTransform rect)
	{
		if (rect == null) return;
		rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
	}
	private void OnClickNavigationCell(int index)
	{
		if (_openingGame || HasSettingsOpen || HasLevelBoxOpen || index != HOME_PAGE_INDEX) return;
		CurrentPageIndex = HOME_PAGE_INDEX; _navigationBar?.SwitchPage(index);
	}
	public void JumpToPage(int index)
	{
		if (_openingGame || HasSettingsOpen || HasLevelBoxOpen || index != HOME_PAGE_INDEX) return;
		CurrentPageIndex = HOME_PAGE_INDEX; _navigationBar?.JumpToPage(index);
	}
	public void OpenSettings()
	{
		Initialize();
		if (!_ready || _openingGame || HasSettingsOpen || HasLevelBoxOpen || HasSpecialStreakOpen || !isActiveAndEnabled) return;
		_settings = SettingsDialog.Show(_canvas.transform).GetAwaiter().GetResult();
		if (_settings != null) _settings.Closed += OnSettingsClosed;
	}
	private void OnSettingsClosed() { if (_settings != null) _settings.Closed -= OnSettingsClosed; _settings = null; }
	public bool TryEnterGame()
	{
		if (_openingGame || HasSettingsOpen || HasLevelBoxOpen || HasSpecialStreakOpen || !isActiveAndEnabled) return false;
		if (GameProgressStore.IsCampaignComplete || !SpecialStreakManager.Ensure().SettlePending()) return false;
		var catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
		if (catalog == null || catalog.GetLevel(GameProgressStore.NextPlayableLevel) == null) { HomePage.RefreshProgress(); return false; }
		if (!Application.CanStreamedLevelBeLoaded("GameScene")) { Debug.LogWarning("GameScene is not enabled in Build Settings."); return false; }
		_openingGame = true;
		_specialStreak?.CommitProgressBeforeEnterGame();
		GameData.ClearCurrent(); Time.timeScale = 1f;
		SceneManager.LoadScene("GameScene");
		return true;
	}
	private void OnDisable()
	{
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
		if (Current == this) Current = null;
	}
}
