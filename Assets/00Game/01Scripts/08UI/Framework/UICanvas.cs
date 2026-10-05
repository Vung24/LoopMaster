using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Snui;

[DefaultExecutionOrder(-900)]
public sealed class UICanvas : MonoBehaviour
{
	private static readonly List<UICanvas> ActiveCanvases = new List<UICanvas>();
	public static UICanvas Current => ActiveCanvases.Count > 0 ? ActiveCanvases[ActiveCanvases.Count - 1] : null;

	[SerializeField] private Button _pauseButton;
	[SerializeField] private Button _undoButton;
	[SerializeField] private Button _shuffleButton;
	[SerializeField] private Button _pathButton;
	[SerializeField] private Button _speedButton;
	[SerializeField] private TMP_Text _levelLabel;
	[SerializeField] private TMP_Text _capacityLabel;
	[SerializeField] private Image _capacityFill;
	[SerializeField] private RectTransform _capacityFillRect;
	[SerializeField] private GameObject PauseDialogPrefab;
	[SerializeField] private SafeArea _safeArea;
	[SerializeField] private Transform _top;
	[SerializeField] private Transform _bottom;
	private RectTransform _topFlag;

	private GameController _controller;
	private Canvas _canvas;
	private float _capacityFillWidth;
	private PauseDialogContent _pauseDialog;
	private bool _visibilityInitialized;
	private bool _inputInitialized;
	private bool _referencesCached;
	private Image _speedIcon;
	private TMP_Text _speedLabel;
	private ToolBase _undoTool;
	private ToolBase _shuffleTool;
	private ToolBase _pathTool;

	public SafeArea SafeArea => _safeArea;

	public Rect GetGameplayViewport(Camera camera)
	{
		CacheLegacyPrefabReferences();
		float scale = _canvas == null ? 1f : _canvas.scaleFactor;
		float top = _topFlag == null ? 100f : _topFlag.rect.height;
		return GameplayCameraLayout.Viewport(camera.pixelRect, Screen.safeArea,
			top * scale, GameplayCameraLayout.NativeBottomReserve * scale);
	}

	private void Awake()
	{
		CacheLegacyPrefabReferences();
		ConfigureCanvasCamera();
		EnsureVisibleLayout();
		Snui_Text.EnsureFontAssets(transform);
		Bind(GameController.Current);
	}

	private void OnEnable()
	{
		if (!ActiveCanvases.Contains(this)) ActiveCanvases.Add(this);
		CacheLegacyPrefabReferences();
		ConfigureCanvasCamera();
		EnsureVisibleLayout();
		Bind(GameController.Current);
	}

	private void OnDisable() => ActiveCanvases.Remove(this);
	private void OnDestroy() => ActiveCanvases.Remove(this);

	private void Start()
	{
		Animation intro = GetComponent<Animation>();
		if (intro != null && intro.clip != null) intro.Play();
		StartCoroutine(RefreshLayoutNextFrame());
	}

	private IEnumerator RefreshLayoutNextFrame()
	{
		yield return null;
		ConfigureCanvasCamera();
		Snui_Text.EnsureFontAssets(transform);
		Canvas.ForceUpdateCanvases();
		RectTransform rootRect = transform as RectTransform;
		if (rootRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
	}

	public void Bind(GameController controller)
	{
		_controller = controller;
		CacheLegacyPrefabReferences();
		_undoTool = BindTool(_undoButton, InventoryKeys.TOOL_UNDO);
		_shuffleTool = BindTool(_shuffleButton, InventoryKeys.TOOL_SHUFFLE);
		_pathTool = BindTool(_pathButton, InventoryKeys.TOOL_PATH);
		ConfigureInput();
		ConfigureCanvasCamera();
		EnsureVisibleLayout();
		BindButton(_pauseButton, ShowPauseDialog);
		BindButton(_undoButton, UseUndo);
		BindButton(_shuffleButton, UseShuffle);
		BindButton(_pathButton, UsePath);
		if (_speedButton != null)
		{
			SpeedButtonInput input = _speedButton.GetComponent<SpeedButtonInput>();
			if (input == null) input = _speedButton.gameObject.AddComponent<SpeedButtonInput>();
			input.Bind(_controller);
			_speedIcon = _speedButton.targetGraphic as Image;
		}
		RefreshDisplay();
	}

	private void CacheLegacyPrefabReferences()
	{
		if (_referencesCached) return;
		_referencesCached = true;
		if (_safeArea == null) _safeArea = GetComponentInChildren<SafeArea>(true);
		if (_top == null) _top = HierarchyReferenceCache.Child(_safeArea != null ? _safeArea.transform : null, "Top");
		if (_bottom == null) _bottom = HierarchyReferenceCache.Child(transform, "Bottom");
		_topFlag = HierarchyReferenceCache.Descendant(transform, "TopFlag") as RectTransform;
		if (_pauseButton == null) _pauseButton = ResolveButton(_top, _bottom, "PauseBtn");
		if (_undoButton == null) _undoButton = ResolveButton(_top, _bottom, "Undo");
		if (_shuffleButton == null) _shuffleButton = ResolveButton(_top, _bottom, "Shuffle");
		if (_pathButton == null) _pathButton = ResolveButton(_top, _bottom, "Path");
		if (_speedButton == null) _speedButton = ResolveButton(_top, _bottom, "Speed");
	}

	private static Button ResolveButton(Transform top, Transform bottom, string name)
	{
		Transform node = HierarchyReferenceCache.Descendant(top, name)
			?? HierarchyReferenceCache.Descendant(bottom, name);
		return HierarchyReferenceCache.Component<Button>(node);
	}

	private void ConfigureInput()
	{
		if (_inputInitialized) return;
		_inputInitialized = true;
		GameResultUI.EnsureEventSystem();
		GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
		if (raycaster == null) raycaster = gameObject.AddComponent<GraphicRaycaster>();
		raycaster.enabled = true;
		CanvasGroup group = GetComponent<CanvasGroup>();
		if (group != null)
		{
			group.interactable = true;
			group.blocksRaycasts = true;
		}

		var hitAreas = new HashSet<Graphic>();
		foreach (Button button in new[] { _pauseButton, _undoButton,
			_shuffleButton, _pathButton, _speedButton })
		{
			if (button == null) continue;
			button.gameObject.SetActive(true);
			button.enabled = true;
			for (Transform node = button.transform; node != null; node = node.parent)
			{
				CanvasGroup buttonGroup = node.GetComponent<CanvasGroup>();
				if (buttonGroup != null)
				{
					buttonGroup.enabled = true;
					buttonGroup.interactable = true;
					buttonGroup.blocksRaycasts = true;
					if (buttonGroup.alpha <= 0f) buttonGroup.alpha = 1f;
				}
				if (node == transform) break;
			}
			if (button.targetGraphic != null) hitAreas.Add(button.targetGraphic);
			Graphic ownGraphic = button.GetComponent<Graphic>();
			if (ownGraphic != null) hitAreas.Add(ownGraphic);
		}
		foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
			graphic.raycastTarget = hitAreas.Contains(graphic);
	}

	private void ConfigureCanvasCamera()
	{
		if (_canvas == null) _canvas = GetComponent<Canvas>();
		if (_canvas == null) return;
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.worldCamera = null;
		_canvas.overrideSorting = true;
		_canvas.sortingOrder = 1000;
		_canvas.enabled = true;
	}

	private void EnsureVisibleLayout()
	{
		if (transform.localScale.sqrMagnitude < 0.000001f)
			transform.localScale = Vector3.one;

		if (_safeArea != null)
		{
			_safeArea.gameObject.SetActive(true);
		}
		if (_top != null) _top.gameObject.SetActive(true);
		if (_bottom != null) _bottom.gameObject.SetActive(true);

		CanvasGroup rootGroup = GetComponent<CanvasGroup>();
		if (rootGroup != null && rootGroup.alpha <= 0f)
			rootGroup.alpha = 1f;

		if (!_visibilityInitialized)
		{
			_visibilityInitialized = true;
			Canvas.ForceUpdateCanvases();
			RectTransform rootRect = transform as RectTransform;
			if (rootRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
		}
	}

	private void Update()
	{
		RefreshDisplay();
	}

	private void RefreshDisplay()
	{
		if (_controller == null) _controller = GameController.Current;
		if (_controller == null) return;

		if (_controller.CurrentLevelNumber > 0 && _levelLabel != null)
			_levelLabel.text = _controller.CurrentLevelNumber.ToString();

		int maximum = Mathf.Max(0, _controller.RoadMaxCapacity);
		int used = Mathf.Clamp(_controller.RoadUsedCapacity, 0, maximum);
		float progress = maximum == 0 ? 0f : used / (float)maximum;
		if (_capacityFill != null && _capacityFill.type == Image.Type.Filled)
			_capacityFill.fillAmount = progress;
		else if (_capacityFillRect != null)
		{
			if (_capacityFillWidth <= 0f && _capacityFillRect.rect.width > 0f)
				_capacityFillWidth = _capacityFillRect.rect.width;
			if (_capacityFillWidth > 0f)
				_capacityFillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
					_capacityFillWidth * progress);
		}
		if (_capacityLabel != null) _capacityLabel.text = used + "/" + maximum;

		bool active = _controller.IsGameplayActive;
		if (_pauseButton != null) _pauseButton.interactable = active;
		if (_undoTool != null) _undoTool.UpdateCanUseState(_controller.CanRequestUndoTool);
		if (_shuffleTool != null) _shuffleTool.UpdateCanUseState(_controller.CanRequestShuffleTool);
		if (_pathTool != null) _pathTool.UpdateCanUseState(_controller.CanRequestPathTool);
		if (_speedButton != null) _speedButton.interactable = active;
		if (_speedLabel != null) _speedLabel.text = _controller.IsSpeedUpActive ? "x4" : "x2";
		Sprite speedSprite = _controller.SpeedButtonSprite;
		if (_speedIcon != null && speedSprite != null && _speedIcon.sprite != speedSprite)
			_speedIcon.sprite = speedSprite;
	}

	private void ShowPauseDialog()
	{
		if (_controller == null || !_controller.IsGameplayActive) return;
		if (_pauseDialog == null)
		{
			if (PauseDialogPrefab == null)
			{
				Debug.LogWarning("UICanvas has no PauseDialogPrefab assigned.", this);
				return;
			}
			GameObject instance = Instantiate(PauseDialogPrefab, transform, false);
			_pauseDialog = instance.GetComponent<PauseDialogContent>();
			if (_pauseDialog == null) _pauseDialog = instance.AddComponent<PauseDialogContent>();
		}
		else _pauseDialog.gameObject.SetActive(true);
		_pauseDialog.Bind(_controller);
	}

	private void UseUndo()
	{
		if (_controller != null) _controller.OnClickToolUndo();
	}

	private void UseShuffle()
	{
		if (_controller != null) _controller.OnClickToolShuffle();
	}

	private void UsePath()
	{
		if (_controller != null) _controller.OnClickToolPath();
	}


	private static ToolBase BindTool(Button button, string key)
	{
		if (button == null) return null;
		ToolBase tool = button.GetComponent<ToolBase>();
		if (tool == null) tool = button.gameObject.AddComponent<ToolBase>();
		tool.Bind(key);
		return tool;
	}

	private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
	{
		if (button == null) return;
		button.onClick.RemoveListener(callback);
		button.onClick.AddListener(callback);
	}

}
