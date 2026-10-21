using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Snui;
using DG.Tweening;
using UnityEngine.EventSystems;
[DefaultExecutionOrder(-900)]
public sealed class UICanvas : MonoBehaviour, IGameplayViewport
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
	[SerializeField] private Transform _capacityNode;
	[SerializeField] private string PauseDialogAddress = "PauseDialogContent";
	private bool _pauseLoading;
	private int _pauseVersion;

	public Transform CapacityNode => _capacityNode != null ? _capacityNode : (_capacityNode = HierarchyReferenceCache.Descendant(transform, "Capacity"));
	[SerializeField] private SafeArea _safeArea;
	[SerializeField] private Transform _top;
	[SerializeField] private Transform _bottom;
	[SerializeField] private RectTransform _topFlag;
	[SerializeField] private RectTransform _bgGame;

	private GameController _controller;
	private Canvas _canvas;
	private float _capacityFillWidth;
	private int _displayedLevel = -1;
	private int _displayedCapacityUsed = -1;
	private int _displayedCapacityMaximum = -1;
	private GameData _capacityData;
	private List<SliceInfo> _capacitySlices;
	private uint _capacityVersion;
	private int _capacitySliceCount;
	private int _capacityUsed;
	private GameObject _pathFlightItem;
	private Sequence _pathFlightTween;
	private Tween _capacityExtensionTween;
	private PauseDialogContent _pauseDialog;
	private bool _visibilityInitialized;
	private bool _inputInitialized;
	private bool _referencesCached;
	private ToolBase _undoTool;
	private ToolBase _shuffleTool;
	private ToolBase _pathTool;
	private Image _speedIcon;
	private Sprite _lastSpeedSprite;
	[SerializeField] private ShuffleSelectionView _shuffleSelectionView;

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
		GameBackgroundController shadowBackground = GetComponent<GameBackgroundController>();
		if (shadowBackground != null) shadowBackground.enabled = true;
		if (!ActiveCanvases.Contains(this)) ActiveCanvases.Add(this);
		CacheLegacyPrefabReferences();
		ConfigureCanvasCamera();
		EnsureVisibleLayout();
		Bind(GameController.Current);
	}

	private void OnDisable()
	{
		if (_shuffleSelectionView != null) _shuffleSelectionView.CancelSelection();
		CancelPathPresentation();
		GameBackgroundController shadowBackground = GetComponent<GameBackgroundController>();
		if (shadowBackground != null) shadowBackground.enabled = false;
		_pauseVersion++;
		_pauseLoading = false;
		if (_pauseDialog != null) { _pauseDialog.gameObject.SetActive(false); Destroy(_pauseDialog.gameObject); }
		_pauseDialog = null;
		ActiveCanvases.Remove(this);
	}

	private void OnDestroy()
	{
		ActiveCanvases.Remove(this);
	}

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
		CancelPathPresentation();
		// Rebinding (including re-enable) must render the current values again.
		_displayedLevel = -1;
		_displayedCapacityUsed = -1;
		_displayedCapacityMaximum = -1;
		_capacityData = null;
		_capacitySlices = null;
		_controller = controller;
		if (_shuffleSelectionView != null) _shuffleSelectionView.Bind(controller);
		CacheLegacyPrefabReferences();
		_undoTool = BindTool(_undoButton, InventoryKeys.TOOL_UNDO);
		_shuffleTool = BindTool(_shuffleButton, InventoryKeys.TOOL_SHUFFLE);
		_pathTool = BindTool(_pathButton, InventoryKeys.TOOL_PATH);
		ConfigureBackground();
		ConfigureInput();
		ConfigureCanvasCamera();
		EnsureVisibleLayout();
		BindButton(_pauseButton, ShowPauseDialog);
		BindButton(_undoButton, UseUndo);
		BindButton(_shuffleButton, UseShuffle);
		BindButton(_pathButton, UsePath);
		BindButton(_speedButton, ToggleSpeed);
		RefreshDisplay();
	}

	private void ConfigureBackground()
	{
		Camera mainCam = _controller != null ? _controller.MainCamera : Camera.main;
		if (mainCam != null)
		{
			GameBackgroundController shadowBackground = GetComponent<GameBackgroundController>();
			if (shadowBackground == null) shadowBackground = gameObject.AddComponent<GameBackgroundController>();
			shadowBackground.Bind(mainCam);
		}

		// Ensure duplicate _bgGame inside UICanvas remains inactive so it does not overlap UI.
		if (_bgGame != null)
		{
			_bgGame.gameObject.SetActive(false);
		}
	}

	private void CacheLegacyPrefabReferences()
	{
		if (_referencesCached) return;
		_referencesCached = true;
		if (_safeArea == null) _safeArea = GetComponentInChildren<SafeArea>(true);
		if (_top == null) _top = HierarchyReferenceCache.Child(_safeArea != null ? _safeArea.transform : null, "Top");
		if (_bottom == null) _bottom = HierarchyReferenceCache.Child(transform, "Bottom");
		if (_topFlag == null) _topFlag = HierarchyReferenceCache.Descendant(transform, "TopFlag") as RectTransform;
		if (_capacityNode == null) _capacityNode = HierarchyReferenceCache.Descendant(transform, "Capacity");
		if (_pauseButton == null) _pauseButton = ResolveButton(_top, _bottom, "PauseBtn");
		if (_undoButton == null) _undoButton = ResolveButton(_top, _bottom, "Undo");
		if (_shuffleButton == null) _shuffleButton = ResolveButton(_top, _bottom, "Shuffle");
		if (_pathButton == null) _pathButton = ResolveButton(_top, _bottom, "Path");
		if (_speedButton == null) _speedButton = ResolveButton(_top, _bottom, "Speed");
		if (_speedButton != null && _speedIcon == null)
		{
			_speedIcon = _speedButton.targetGraphic as Image;
			if (_speedIcon == null) _speedIcon = _speedButton.GetComponent<Image>();
		}
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
		if (!_controller.IsSelectingShuffle) HideShuffleSelection();

		int level = _controller.CurrentLevelNumber;
		if (level > 0 && _levelLabel != null && level != _displayedLevel)
		{
			_levelLabel.text = level.ToString();
			_displayedLevel = level;
		}

		int maximum = Mathf.Max(0, _controller.RoadMaxCapacity);
		int used = Mathf.Clamp(GetCapacityUsed(), 0, maximum);
		if (used != _displayedCapacityUsed || maximum != _displayedCapacityMaximum)
		{
			_displayedCapacityUsed = used;
			_displayedCapacityMaximum = maximum;
			float progress = maximum == 0 ? 0f : used / (float)maximum;
			if (_capacityFill != null && _capacityFill.type == Image.Type.Filled)
				_capacityFill.fillAmount = progress;
			else if (_capacityFillRect != null)
			{
				float currentWidth = _capacityFillRect.rect.width;
				if (_capacityFillWidth <= 0f && currentWidth > 0f)
					_capacityFillWidth = currentWidth;
				if (_capacityFillWidth > 0f)
				{
					float targetWidth = _capacityFillWidth * progress;
					if (!Mathf.Approximately(currentWidth, targetWidth))
						_capacityFillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
				}
			}
			if (_capacityLabel != null)
			{
				_capacityLabel.text = used + "/" + maximum;
			}
		}

		bool active = _controller.IsGameplayActive;
		if (_pauseButton != null) _pauseButton.interactable = active;
		if (_speedButton != null)
		{
			_speedButton.interactable = active;
			RefreshSpeedButtonDisplay();
		}
		if (_undoTool != null || _shuffleTool != null || _pathTool != null)
		{
			_controller.GetToolRequestState(out bool undo, out bool shuffle, out bool path);
			if (_undoTool != null) _undoTool.UpdateCanUseState(undo);
			if (_shuffleTool != null) _shuffleTool.UpdateCanUseState(shuffle);
			if (_pathTool != null) _pathTool.UpdateCanUseState(path);
		}
	}

	private int GetCapacityUsed()
	{
		GameData data = _controller.Data;
		if (data == null)
		{
			_capacityData = null;
			_capacitySlices = null;
			return 0;
		}

		// Movement and list sorting do not change capacity. Count/reference checks also
		// cover direct list edits by loaders and conveyor null-entry cleanup.
		if (_capacityData != data || _capacitySlices != data.TransferSlice ||
			_capacityVersion != data.TransferGroupVersion || _capacitySliceCount != data.TransferSlice.Count)
		{
			_capacityUsed = data.GetTransferGroup();
			_capacityData = data;
			_capacitySlices = data.TransferSlice;
			_capacityVersion = data.TransferGroupVersion;
			_capacitySliceCount = data.TransferSlice.Count;
		}
		return _capacityUsed;
	}

	internal void PlayPathExtension()
	{
		if (!isActiveAndEnabled) return;
		CancelPathPresentation();
		RectTransform source = _pathButton == null ? null : _pathButton.transform as RectTransform;
		GameLoader loader = _controller == null ? null : _controller.GetComponent<GameLoader>();
		GameObject prefab = loader == null ? null : loader.FlyPath;
		Image artwork = prefab == null ? null : HierarchyReferenceCache.ChildComponent<Image>(prefab.transform, "Icon");
		if (source == null || _capacityLabel == null || artwork == null || artwork.sprite == null)
		{
			Debug.LogWarning("Path flight needs the Path button, capacity label and FlyAddPath/Icon sprite.", this);
			return;
		}

		// Copy only the item artwork: no particle objects or behaviours from the effect prefab.
		_pathFlightItem = new GameObject("FlyAddPath", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		_pathFlightItem.layer = gameObject.layer;
		RectTransform item = (RectTransform)_pathFlightItem.transform;
		item.SetParent(transform, false);
		item.SetAsLastSibling();
		Image icon = item.GetComponent<Image>();
		icon.sprite = artwork.sprite;
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		item.sizeDelta = artwork.rectTransform.rect.size;
		Vector3 start = transform.InverseTransformPoint(source.TransformPoint(source.rect.center));
		Vector3 center = ((RectTransform)transform).rect.center;
		float sourceWidth = transform.InverseTransformVector(source.TransformVector(Vector3.right * source.rect.width)).magnitude;
		Vector3 scale = Vector3.one * (sourceWidth / Mathf.Max(1f, item.rect.width));
		item.localPosition = start;
		item.localScale = scale;
		Vector3 target = transform.InverseTransformPoint(_capacityLabel.rectTransform.TransformPoint(
			_capacityLabel.rectTransform.rect.center));
		Vector3 control = (center + target) * 0.5f + new Vector3(-60f, 60f, 0f);
		float flightProgress = 0f;
		_pathFlightTween = DOTween.Sequence().SetLink(gameObject)
			.Append(item.DOLocalMove(center, 0.35f).SetEase(Ease.OutCubic))
			.Append(item.DOScale(scale * 2.2f, 0.4f).SetEase(Ease.OutQuad))
			.AppendInterval(0.12f)
			.Append(DOTween.To(() => flightProgress, progress =>
			{
				flightProgress = progress;
				item.localPosition = EvaluateQuadraticBezier(center, control, target, progress);
			}, 1f, 0.5f).SetEase(Ease.InQuad))
			.Join(item.DOScale(scale * 0.25f, 0.5f).SetEase(Ease.InQuad))
			.OnComplete(OnPathFlightCompleted);
	}

	private void OnPathFlightCompleted()
	{
		_pathFlightTween = null;
		if (_pathFlightItem != null)
		{
			Destroy(_pathFlightItem);
			_pathFlightItem = null;
		}
		_capacityExtensionTween = Capacity.AnimateExtension(_capacityLabel)
			.OnComplete(OnCapacityExtensionCompleted);
	}

	private void OnCapacityExtensionCompleted()
	{
		_capacityExtensionTween = null;
	}

	private static Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
	{
		float u = 1f - t;
		return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
	}

	private void CancelPathPresentation()
	{
		_pathFlightTween?.Kill();
		_pathFlightTween = null;
		_capacityExtensionTween?.Kill();
		_capacityExtensionTween = null;
		if (_pathFlightItem != null)
		{
			_pathFlightItem.SetActive(false);
			Destroy(_pathFlightItem);
			_pathFlightItem = null;
		}
	}

	private void ToggleSpeed()
	{
		if (_controller == null || !_controller.IsGameplayActive) return;
		_controller.SetSpeedUp(!_controller.IsSpeedUpActive);
		RefreshSpeedButtonDisplay();
	}

	private void RefreshSpeedButtonDisplay()
	{
		if (_controller == null) return;
		if (_speedIcon == null && _speedButton != null)
		{
			_speedIcon = _speedButton.targetGraphic as Image;
			if (_speedIcon == null) _speedIcon = _speedButton.GetComponent<Image>();
		}
		if (_speedIcon == null) return;
		Sprite speedSprite = _controller.SpeedButtonSprite;
		if (speedSprite != null && _lastSpeedSprite != speedSprite)
		{
			_lastSpeedSprite = speedSprite;
			_speedIcon.sprite = speedSprite;
		}
	}

	private void ShowPauseDialog() => LoadPauseDialog().Forget();
	private async UniTask LoadPauseDialog()
	{
		if (_pauseLoading || _pauseDialog != null || _controller == null || !_controller.IsGameplayActive) return;
		var data = _controller.Data;
		int version = _pauseVersion;
		_pauseLoading = true;
		try
		{
			var instance = await PopupAssets.Instantiate(PauseDialogAddress, transform, this.GetCancellationTokenOnDestroy());
			if (this == null || version != _pauseVersion || !isActiveAndEnabled || _controller == null || _controller.Data != data || _controller.EndState != GameEndState.Playing)
			{ Destroy(instance); return; }
			_pauseDialog = instance.GetComponent<PauseDialogContent>() ?? instance.AddComponent<PauseDialogContent>();
			_pauseDialog.Bind(_controller);
		}
		finally { if (this != null && version == _pauseVersion) _pauseLoading = false; }
	}

	private void UseUndo()
	{
		if (_controller != null) _controller.OnClickToolUndo();
	}

	public void RefreshShuffleSelection()
	{
		if (_shuffleSelectionView != null) _shuffleSelectionView.Refresh();
	}

	public bool ShowShuffleSelection(Camera camera, IReadOnlyList<Box> boxes)
	{
		if (_shuffleSelectionView == null)
		{
			Debug.LogError("UICanvas requires its ShuffleSelectionView reference.", this);
			return false;
		}
		_shuffleSelectionView.Bind(_controller);
		return _shuffleSelectionView.Show(camera, boxes);
	}

	public void HideShuffleSelection()
	{
		if (_shuffleSelectionView != null) _shuffleSelectionView.Hide();
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
