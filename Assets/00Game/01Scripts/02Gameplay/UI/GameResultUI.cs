using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameResultUI : MonoBehaviour
{
	private GameController _controller;
	private GameObject _winPrefab;
	private GameObject _failPrefab;
	private GameObject _failContentPrefab;
	private GameObject _canvasObject;
	private Canvas _canvas;
	private bool _built;
	private WinDialog _winDialog;
	private WinDialogContent _winContent;
	private FailDialog _failDialog;
	private FailDialogContent _failContent;
	private PreFailDialogContent _preFailContent;
	private NewPlayerTutorial _failTutorial;
	private RetryDialogContent _retryContent;
	private GameObject _retryRoot, _streakWarning;
	private SpecialStreakFailBanner _streakBanner;
	public Transform DialogParent { get { BuildIfNeeded(); return _canvasObject == null ? null : _canvasObject.transform; } }
	private Coroutine _showWinRoutine;
	private Coroutine _showFailRoutine;
	private static EventSystem _eventSystem;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void InstallEventSystemRepair()
	{
		_eventSystem = null;
		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		EnsureEventSystem(scene);
	}

	public static GameResultUI Ensure(GameController controller, GameObject winPrefab,
		GameObject failPrefab, GameObject failContentPrefab)
	{
		if (controller == null) return null;
		GameResultUI result = controller.GetComponent<GameResultUI>();
		if (result == null) result = controller.gameObject.AddComponent<GameResultUI>();
		result._winPrefab = winPrefab;
		result._failPrefab = failPrefab;
		result._failContentPrefab = failContentPrefab;
		result.Bind(controller);
		return result;
	}

	public void Bind(GameController controller)
	{
		if (_controller != controller)
		{
			HideAll();
			Unsubscribe();
			_controller = controller;
			if (_controller != null)
			{
				_controller.GameEnded += OnGameEnded;
				_controller.GameRevived += OnGameRevived;
				_controller.LevelStarted += OnLevelStarted;
				_controller.NextLevelUnavailable += OnNextLevelUnavailable;
			}
		}

		if (_controller != null) BuildIfNeeded();
	}

	private void BuildIfNeeded()
	{
		if (_built || _controller == null) return;
		_built = true;
		EnsureEventSystem();

		_canvasObject = new GameObject("RuntimeDialogCanvas", typeof(RectTransform),
			typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		_canvasObject.transform.SetParent(transform, false);
		_canvasObject.SetActive(false);
		_canvas = _canvasObject.GetComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.overrideSorting = true;
		// Gameplay UICanvas also uses 1000; results must cover it regardless
		// of creation order. Reward counters temporarily use 1100 above both.
		_canvas.sortingOrder = 1001;
		CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1080f, 1920f);
		scaler.matchWidthOrHeight = 0.5f;

		if (_winPrefab != null) BuildAuthoredWinDialog();
		if (_failPrefab != null) BuildAuthoredFailDialog();
		if (_winContent == null && _winDialog != null)
		{
			Destroy(_winDialog.gameObject);
			_winDialog = null;
		}
		if (_failContent == null && _failDialog != null)
		{
			Destroy(_failDialog.gameObject);
			_failDialog = null;
		}
		if (_winDialog == null || _failDialog == null) BuildFallbackDialogs();

		HideAll();
		_canvasObject.SetActive(true);
	}

	private void BuildAuthoredWinDialog()
	{
		GameObject root = Instantiate(_winPrefab, _canvasObject.transform, false);
		root.name = _winPrefab.name;
		root.SetActive(false);
		_winDialog = root.GetComponent<WinDialog>();
		if (_winDialog == null) _winDialog = root.AddComponent<WinDialog>();
		_winDialog.Bind(_controller);
		_winContent = root.GetComponentInChildren<WinDialogContent>(true);
		if (_winContent != null) _winContent.Bind(_controller);
	}

	private void BuildAuthoredFailDialog()
	{
		GameObject root = Instantiate(_failPrefab, _canvasObject.transform, false);
		root.name = _failPrefab.name;
		root.SetActive(false);
		_failDialog = root.GetComponent<FailDialog>();
		if (_failDialog == null) _failDialog = root.AddComponent<FailDialog>();
		_failDialog.Bind(_controller);

		_preFailContent = root.GetComponentInChildren<PreFailDialogContent>(true);
		if (_preFailContent != null) _preFailContent.gameObject.SetActive(false);

		foreach (var candidate in root.GetComponentsInChildren<FailDialogContent>(true))
			if (!(candidate is PreFailDialogContent)) { _failContent = candidate; break; }
		if (_failContent == null && _failContentPrefab != null)
		{
			Snui.SafeArea safeArea = root.GetComponentInChildren<Snui.SafeArea>(true);
			Transform contentParent = safeArea != null ? safeArea.transform : root.transform;
			GameObject content = Instantiate(_failContentPrefab, contentParent, false);
			content.name = _failContentPrefab.name;
			content.SetActive(false);
			_failContent = content.GetComponent<FailDialogContent>();
			if (_failContent != null) _failContent.Bind(_controller);
			content.SetActive(true);
		}
		else
		{
			if (_failContent != null) _failContent.Bind(_controller);
		}
	}

	private void BuildFallbackDialogs()
	{
		if (_winDialog == null)
		{
			GameObject root = CreateOverlay("WinDialogFallback", _canvasObject.transform);
			_winDialog = root.AddComponent<WinDialog>();
			_winDialog.Bind(_controller);
			RectTransform panel = CreatePanel(root.transform, new Color(0.08f, 0.18f, 0.22f, 0.98f));
			Text title = CreateText("Title", panel, new Vector2(0f, 95f), new Vector2(580f, 100f), 42);
			Button next = CreateButton("NextButton", panel, "NEXT LEVEL", new Vector2(0f, -35f));
			Button replay = CreateButton("ReplayButton", panel, "REPLAY", new Vector2(0f, -135f));
			_winContent = panel.gameObject.AddComponent<WinDialogContent>();
			_winContent.Bind(_controller, title, next, replay);
		}

		if (_failDialog == null)
		{
			GameObject root = CreateOverlay("FailDialogFallback", _canvasObject.transform);
			_failDialog = root.AddComponent<FailDialog>();
			_failDialog.Bind(_controller);
			RectTransform panel = CreatePanel(root.transform, new Color(0.24f, 0.09f, 0.10f, 0.98f));
			Text title = CreateText("Title", panel, new Vector2(0f, 110f), new Vector2(580f, 90f), 40);
			Text reason = CreateText("Reason", panel, new Vector2(0f, 25f), new Vector2(580f, 65f), 25);
			Button replay = CreateButton("ReplayButton", panel, "REPLAY LEVEL", new Vector2(0f, -105f));
			_failContent = panel.gameObject.AddComponent<FailDialogContent>();
			_failContent.Bind(_controller, title, reason, replay);
			Button adButton = CreateButton("AdButton", panel, "FREE (OFFLINE)", new Vector2(-130f, -40f));
			Button coinOffer = CreateButton("CoinButton", panel, "900 COINS", new Vector2(130f, -40f));
			coinOffer.GetComponentInChildren<Text>().gameObject.name = "Number";
			((RectTransform)adButton.transform).sizeDelta = new Vector2(240f, 65f);
			((RectTransform)coinOffer.transform).sizeDelta = new Vector2(240f, 65f);
			// Rebind after the fallback controls are created.
			_failContent.BindFallbackOffers();
		}
	}

	private void OnGameEnded(GameEndState state)
	{
		BuildIfNeeded();
		HideAll();
		if (state == GameEndState.Won)
		{
			_showWinRoutine = StartCoroutine(ShowWinAfterAnimations(_controller, _controller.Data));
		}
		else if (state == GameEndState.Failed)
		{
			_showFailRoutine = StartCoroutine(ShowFailAfterDelay(_controller, _controller.Data));
		}
	}

	private IEnumerator ShowFailAfterDelay(GameController controller, GameData data)
	{
		// Native ShowFailDialog: 0.2s before stopping BGM, then 1s on ordinary levels.
		yield return new WaitForSeconds(controller.IsFailTutorial() ? 0.4f : 1.2f);
		_showFailRoutine = null;
		if (controller == null || _controller != controller || controller.Data != data ||
			controller.EndState != GameEndState.Failed) yield break;
		if (_failDialog != null) _failDialog.Show();
		if (_preFailContent != null)
		{
			if (_failContent != null) _failContent.Hide();
			_preFailContent.Configure(PlayerPrefs.GetInt("offline_free_revive_used", 0) == 0, ShowFinalFailure);
			_preFailContent.Show();
			ShowStreakWarning(_preFailContent.transform);
			ShowFailTutorial(_preFailContent);
		}
		else ShowFinalFailure();
	}

	private void ShowFinalFailure()
	{
		HideFailTutorial();
		if (_controller == null || _controller.EndState != GameEndState.Failed) return;
		if (_preFailContent != null) _preFailContent.Hide();
		if (_failContent == null) return;
		_failContent.Configure(PlayerPrefs.GetInt("offline_free_revive_used", 0) == 0, ShowRetryAfterLoss);
		_failContent.ReviveRoute = "fail";
		_failContent.SetLevel(_controller.CurrentLevelNumber, _controller.Data.FailReason);
		_failContent.Show();
		ShowStreakWarning(_failContent.transform);
		ShowFailTutorial(_failContent);
	}
	private void ShowStreakWarning(Transform parent)
	{
		if (_streakWarning != null) Destroy(_streakWarning);
		_streakWarning = null;
		var manager = SpecialStreakManager.Ensure();
		var prefab = Snui.DataPrefabLibrary.Current?.LoseSpecialStreakPrefab;
		if (!manager.ShouldShowLoseItem() || prefab == null) return;
		_streakWarning = Instantiate(prefab, parent, false);
		var rect = _streakWarning.transform as RectTransform;
		if (rect != null) { rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(0, -470); }
		Snui.Snui_Text.EnsureFontAssets(_streakWarning.transform);
		var slider = _streakWarning.GetComponentInChildren<SpecialStreakMultiplierSlider>(true);
		if (slider != null) { slider.Type = SpecialStreakMultiplierSliderType.PreLose; slider.Init(manager.MultiplierIndex); }
	}
	private void ShowRetryAfterLoss()
	{
		if (_controller == null || _controller.EndState != GameEndState.Failed || _retryRoot != null) return;
		_controller.CommitAbandonedLevel();
		HideFailTutorial();
		if (_streakWarning != null) Destroy(_streakWarning); _streakWarning = null;
		if (_failContent != null) _failContent.Hide();
		var prefab = Snui.DataPrefabLibrary.Current?.RetryDialogContentPrefab;
		if (prefab == null) { _controller.TryReplayLevel(); return; }
		_retryRoot = Instantiate(prefab, _canvasObject.transform, false);
		Snui.RewardDialog.PrepareOverlay(_retryRoot, 1320);
		_retryContent = _retryRoot.GetComponent<RetryDialogContent>();
		if (_retryContent == null) { Destroy(_retryRoot); _retryRoot = null; return; }
		_retryContent.Bind(_controller);
		_retryContent.Dismissed += OnRetryDismissed;
		_retryContent.Show();
		// Native FailDialog CheckSpecialStreakBanner parents to the full-screen safe area.
		Transform area = _failDialog == null ? null : _failDialog.SafeArea;
		_streakBanner = SpecialStreakFailBanner.Show(area == null ? _canvasObject.transform : area).GetAwaiter().GetResult();
		if (_streakBanner != null) Snui.RewardDialog.PrepareOverlay(_streakBanner.gameObject, 1321);
	}
	private void OnRetryDismissed(int result)
	{
		if (result == Snui.DialogContent.RESULT_CLOSE && _controller != null) _controller.TryReturnHome();
	}
	private void ShowFailTutorial(FailDialogContent content)
	{
		if (!_controller.IsFailTutorial() || content.FreeButtonNode == null) return;
		var library = Snui.DataPrefabLibrary.Current;
		var prefab = library == null ? null : library.FailTutorialPrefab;
		if (prefab == null) return;
		var instance = Instantiate(prefab, content.transform, false);
		_failTutorial = instance.GetComponent<NewPlayerTutorial>();
		if (_failTutorial == null) { Destroy(instance); return; }
		Transform capacity = UICanvas.Current == null ? null : UICanvas.Current.CapacityNode;
		if (capacity != null) _failTutorial.ShowFail(capacity);
		else _failTutorial.ShowFail(content.transform.InverseTransformPoint(content.FreeButtonNode.position));
		PlayerPrefs.SetInt("offline_HasShowFailTutorial", 1);
		PlayerPrefs.Save();
	}
	private void HideFailTutorial()
	{
		if (_failTutorial != null) _failTutorial.CancelAndDismiss();
		_failTutorial = null;
	}
	private void OnGameRevived() => HideAll();

	private IEnumerator ShowWinAfterAnimations(GameController controller, GameData data)
	{
		yield return new WaitForSeconds(0.8f);
		while (controller != null && controller.IsFinishAnimationPlaying) yield return null;
		_showWinRoutine = null;
		if (controller == null || _controller != controller || controller.Data != data ||
			controller.EndState != GameEndState.Won) yield break;
		if (_winContent != null)
			_winContent.SetLevel(controller.CurrentLevelNumber, controller.CanLoadNextLevel);
		HudContainer.Show(_canvasObject.transform).Forget();
		HudContainer.Current?.DisableClick();
		if (_winDialog != null) _winDialog.Show();
	}

	private void OnLevelStarted(int level) { HideAll(); }

	private void OnNextLevelUnavailable(int level)
	{
		if (_winContent != null) _winContent.SetNoMoreLevels();
	}

	private void HideAll()
	{
		if (_retryContent != null) _retryContent.Dismissed -= OnRetryDismissed;
		if (_retryRoot != null) { _retryRoot.SetActive(false); Destroy(_retryRoot); }
		_retryContent = null; _retryRoot = null;
		if (_streakBanner != null) Destroy(_streakBanner.gameObject); _streakBanner = null;
		if (_streakWarning != null) Destroy(_streakWarning); _streakWarning = null;
		HudContainer.TryRevertToOriginLayer();
		if (HudContainer.Current != null && _canvasObject != null && HudContainer.Current.transform.IsChildOf(_canvasObject.transform))
			HudContainer.Current.gameObject.SetActive(false);
		HideFailTutorial();
		if (_showWinRoutine != null) StopCoroutine(_showWinRoutine);
		_showWinRoutine = null;
		if (_showFailRoutine != null) StopCoroutine(_showFailRoutine);
		_showFailRoutine = null;
		if (_winDialog != null) _winDialog.Hide();
		if (_failDialog != null) _failDialog.Hide();
	}

	private static GameObject CreateOverlay(string name, Transform parent)
	{
		GameObject result = new GameObject(name, typeof(RectTransform), typeof(Image));
		result.transform.SetParent(parent, false);
		RectTransform rect = (RectTransform)result.transform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		result.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
		return result;
	}

	private static RectTransform CreatePanel(Transform parent, Color color)
	{
		GameObject result = new GameObject("Panel", typeof(RectTransform), typeof(Image));
		result.transform.SetParent(parent, false);
		RectTransform rect = (RectTransform)result.transform;
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(680f, 480f);
		result.GetComponent<Image>().color = color;
		return rect;
	}

	private static Text CreateText(string name, Transform parent, Vector2 position,
		Vector2 size, int fontSize)
	{
		GameObject result = new GameObject(name, typeof(RectTransform), typeof(Text));
		result.transform.SetParent(parent, false);
		RectTransform rect = (RectTransform)result.transform;
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = size;
		Text text = result.GetComponent<Text>();
		text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		text.fontSize = fontSize;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.white;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		return text;
	}

	private static Button CreateButton(string name, Transform parent, string label, Vector2 position)
	{
		GameObject result = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		result.transform.SetParent(parent, false);
		RectTransform rect = (RectTransform)result.transform;
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = new Vector2(430f, 78f);
		Image image = result.GetComponent<Image>();
		image.color = new Color(0.15f, 0.68f, 0.52f, 1f);
		Button button = result.GetComponent<Button>();
		ColorBlock colors = button.colors;
		colors.highlightedColor = new Color(0.22f, 0.82f, 0.64f, 1f);
		colors.pressedColor = new Color(0.10f, 0.52f, 0.40f, 1f);
		colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.75f);
		button.colors = colors;
		Text text = CreateText("Text", result.transform, Vector2.zero, rect.sizeDelta, 28);
		text.text = label;
		return button;
	}

	public static EventSystem EnsureEventSystem()
	{
		Scene scene = GameController.Current != null
			? GameController.Current.gameObject.scene : SceneManager.GetActiveScene();
		return EnsureEventSystem(scene);
	}

	private static EventSystem EnsureEventSystem(Scene scene)
	{
		EventSystem current = EventSystem.current;
		if (current == null) current = _eventSystem;
		if (current == null && scene.IsValid() && scene.isLoaded)
		{
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				current = root.GetComponentInChildren<EventSystem>(true);
				if (current != null) break;
			}
		}
		if (current == null)
		{
			GameObject eventSystem = new GameObject("RuntimeEventSystem");
			current = eventSystem.AddComponent<EventSystem>();
		}
		current.gameObject.SetActive(true);
		current.enabled = true;
		BaseInputModule inputModule = current.GetComponent<BaseInputModule>();
		if (inputModule == null)
			inputModule = current.gameObject.AddComponent<StandaloneInputModule>();
		inputModule.enabled = true;
		_eventSystem = current;
		return current;
	}

	private void Unsubscribe()
	{
		if (_controller == null) return;
		_controller.GameEnded -= OnGameEnded;
		_controller.GameRevived -= OnGameRevived;
		_controller.LevelStarted -= OnLevelStarted;
		_controller.NextLevelUnavailable -= OnNextLevelUnavailable;
	}

	private void OnDisable() { HideAll(); }
	private void OnDestroy() { Unsubscribe(); }
}
