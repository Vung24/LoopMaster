using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewPlayerTutorial : MonoBehaviour
{
	private enum TutorialStage { None, Level1, Level2, Level3, Fail }
	[SerializeField] private Transform _textNode;
	[SerializeField] private Transform _arrowTextNode;
	[SerializeField] private Transform _bigArrow;
	[SerializeField] private Finger _finger;
	[SerializeField] private Transform _bgMask;
	[SerializeField] private Transform _topMask;
	[SerializeField] private Button _bgBtn;
	[SerializeField] private Button _tapBtn;
	[SerializeField] private TMP_Text _descText;
	private TutorialStage _stage;
	private bool _hasClickedBox;
	private bool _isHiding;
	private CancellationTokenSource _lifetime;
	private Transform _highlightedNode;
	private Transform _originalParent;
	private int _originalSibling;
	private Vector3 _originalPosition;
	private Vector3 _originalScale;
	private Quaternion _originalRotation;
	private RectTransform _highlightedRect;
	private Vector2 _originalAnchorMin, _originalAnchorMax, _originalPivot, _originalSize;
	private Vector3 _originalAnchoredPosition;
	private readonly System.Collections.Generic.List<Tween> _tweens =
		new System.Collections.Generic.List<Tween>();

	private void Awake()
	{
		if (_textNode == null) _textNode = HierarchyReferenceCache.Descendant(transform, "TextNode");
		if (_arrowTextNode == null) _arrowTextNode = HierarchyReferenceCache.Descendant(transform, "ArrowTextNode");
		if (_bigArrow == null) _bigArrow = HierarchyReferenceCache.Descendant(transform, "BigArrow");
		if (_bgMask == null) _bgMask = HierarchyReferenceCache.Descendant(transform, "BgMask");
		if (_topMask == null) _topMask = HierarchyReferenceCache.Descendant(transform, "TopMask");
		if (_finger == null) _finger = GetComponentInChildren<Finger>(true);
		if (_bgBtn == null && _bgMask != null) _bgBtn = _bgMask.GetComponent<Button>();
		if (_tapBtn == null)
		{
			Transform tap = HierarchyReferenceCache.Descendant(transform, "BtnTapTo");
			if (tap != null) _tapBtn = tap.GetComponent<Button>();
		}
		if (_descText == null && _textNode != null)
		{
			Transform desc = HierarchyReferenceCache.Descendant(_textNode, "Desc");
			_descText = desc != null ? desc.GetComponent<TMP_Text>() : _textNode.GetComponentInChildren<TMP_Text>(true);
		}
		_lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
		foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
		foreach (Button button in new[] { _bgBtn, _tapBtn,
			_topMask == null ? null : _topMask.GetComponent<Button>() })
			if (button != null && button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
		HideAllNodes();
	}

	private static void SetActive(Component node, bool active) { if (node != null) node.gameObject.SetActive(active); }
	private void Track(Tween tween) { if (tween != null) _tweens.Add(tween.SetLink(gameObject)); }

	public void Initialize(int stage)
	{
		CancelAnimations();
		_lifetime.Dispose();
		_lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
		_stage = stage >= 1 && stage <= 3 ? (TutorialStage)stage : TutorialStage.None;
		_hasClickedBox = false;
		_isHiding = false;
		HideAllNodes();
		gameObject.SetActive(_stage != TutorialStage.Level3);
	}

	public void ShowLevel1(Vector2 fingerPos)
	{
		_stage = TutorialStage.Level1;
		gameObject.SetActive(true);
		InitTextNode();
		SetDescription("Tap a box to send its colors onto the belt.");
		if (_finger != null) _finger.ShowFinger(fingerPos);
	}

	public void ShowLevel2()
	{
		_stage = TutorialStage.Level2;
		gameObject.SetActive(true);
		InitTextNode();
		SetDescription("Match the colors and stack them nicely!");
	}

	public void ShowFail(Vector3 showPos)
	{
		_stage = TutorialStage.Fail;
		gameObject.SetActive(true);
		InitTextNode(0.2f);
		if (_textNode != null) _textNode.localPosition = new Vector3(
			IsBlueSkin ? 0f : showPos.x, showPos.y - 180f, IsBlueSkin ? 0f : showPos.z);
		SetDescription("Add extra capacity to continue playing.");
		if (_finger != null) _finger.gameObject.SetActive(true);
	}

	public void ShowFail(Transform capacityNode) => ShowFail(capacityNode, null);

	public void ShowFail(Transform capacityNode, Transform targetButton)
	{
		_stage = TutorialStage.Fail;
		gameObject.SetActive(true);
		InitTextNode(0.2f);
		if (capacityNode != null)
		{
			Highlight(capacityNode);
			Vector3 textPos = transform.InverseTransformPoint(capacityNode.position);
			if (_textNode != null)
			{
				_textNode.localPosition = new Vector3(
					IsBlueSkin ? 0f : textPos.x, textPos.y - 180f, IsBlueSkin ? 0f : textPos.z);
			}
		}
		SetDescription("Add extra capacity to continue playing.");
		if (_finger != null)
		{
			if (targetButton != null)
			{
				Transform fingerParent = _finger.transform.parent != null ? _finger.transform.parent : transform;
				Vector3 fingerPos = fingerParent.InverseTransformPoint(targetButton.position);
				_finger.ShowFinger(fingerPos);
			}
			else
			{
				_finger.gameObject.SetActive(true);
			}
		}
	}

	private bool IsBlueSkin => gameObject.name.Contains("Blue");

	private void SetDescription(string fallback)
	{
		if (_descText != null) _descText.text = fallback;
		Snui_Text.EnsureFontAssets(transform);
	}

	private void InitTextNode(float duration = 0.08f)
	{
		if (_textNode == null) return;
		RectTransform rect = _textNode as RectTransform;
		Canvas canvas = GetComponentInParent<Canvas>();
		float bottomInset = Screen.safeArea.yMin / Mathf.Max(0.001f, canvas == null ? 1f : canvas.scaleFactor);
		if (rect != null) rect.anchoredPosition = new Vector2(0f, 305f + bottomInset);
		_textNode.gameObject.SetActive(true);
		_textNode.localScale = Vector3.zero;
		Track(_textNode.DOScale(1f, duration));
	}

	public void OnClickBox(Transform levelNode)
	{
		if (_hasClickedBox || _isHiding) return;
		_hasClickedBox = true;
		if (_stage == TutorialStage.Level3 && levelNode != null) ShowLevel3Async(levelNode).Forget();
		else if (_stage == TutorialStage.Level1 && _finger != null)
		{
			var graphic = _finger.GetComponentInChildren<Graphic>(true);
			if (graphic != null) Track(graphic.DOFade(0f, 0.24f));
			else _finger.HideFinger();
		}
	}

	private async UniTask ShowLevel3Async(Transform levelNode)
	{
		CancellationToken token = _lifetime.Token;
		try
		{
			await UniTask.WaitForSeconds(1f, cancellationToken: token);
			if (levelNode == null) return;
			gameObject.SetActive(true);
			SetActive(_bgMask, true);
			if (_bgMask != null)
			{
				CanvasGroup group = _bgMask.GetComponent<CanvasGroup>();
				if (group != null) { group.alpha = 0f; Track(group.DOFade(1f, 0.3f)); }
			}
			Highlight(levelNode);
			SetActive(_textNode, true);
			if (_textNode != null)
			{
				_textNode.localScale = Vector3.one;
				_textNode.localPosition = new Vector3(IsBlueSkin ? 0f : levelNode.localPosition.x,
					levelNode.localPosition.y - 520f, IsBlueSkin ? 0f : levelNode.localPosition.z);
			}
			SetDescription("Watch the belt capacity. Leave room for the next colors!");
			SetActive(_bigArrow, true);
			if (_bigArrow != null) _bigArrow.localPosition = levelNode.localPosition + new Vector3(0f, -180f, 0f);
			await UniTask.WaitForSeconds(1f, cancellationToken: token);
			if (!gameObject.activeInHierarchy || _highlightedNode == null) return;
			SetActive(_topMask, true);
			SetActive(_tapBtn, true);
			if (_tapBtn != null)
			{
				CanvasGroup group = _tapBtn.GetComponent<CanvasGroup>();
				if (group != null) { group.alpha = 0f; Track(group.DOFade(1f, 0.5f)); }
				BreathAnim breath = _tapBtn.GetComponent<BreathAnim>();
				if (breath == null) breath = _tapBtn.gameObject.AddComponent<BreathAnim>();
				breath.Play(0f, 3f);
				_tapBtn.onClick.AddListener(DismissHighlight);
			}
			if (_bgBtn != null) _bgBtn.onClick.AddListener(DismissHighlight);
			Button top = _topMask == null ? null : _topMask.GetComponent<Button>();
			if (top != null) top.onClick.AddListener(DismissHighlight);
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { RestoreHighlight(); }
	}

	private void Highlight(Transform node)
	{
		_highlightedNode = node;
		_originalParent = node.parent;
		_originalSibling = node.GetSiblingIndex();
		_originalPosition = node.localPosition;
		_originalRotation = node.localRotation;
		_originalScale = node.localScale;
		_highlightedRect = node as RectTransform;
		if (_highlightedRect != null)
		{
			_originalAnchorMin = _highlightedRect.anchorMin; _originalAnchorMax = _highlightedRect.anchorMax;
			_originalPivot = _highlightedRect.pivot; _originalSize = _highlightedRect.sizeDelta;
			_originalAnchoredPosition = _highlightedRect.anchoredPosition3D;
		}
		node.SetParent(transform, true);
		node.gameObject.SetActive(true);
	}

	private void RestoreHighlight()
	{
		if (_highlightedNode == null) return;
		Transform light = HierarchyReferenceCache.Descendant(_highlightedNode, "Light");
		SetActive(light, false);
		_highlightedNode.SetParent(_originalParent, false);
		_highlightedNode.SetSiblingIndex(_originalSibling);
		if (_highlightedRect != null)
		{
			_highlightedRect.anchorMin = _originalAnchorMin; _highlightedRect.anchorMax = _originalAnchorMax;
			_highlightedRect.pivot = _originalPivot; _highlightedRect.sizeDelta = _originalSize;
			_highlightedRect.anchoredPosition3D = _originalAnchoredPosition;
		}
		else _highlightedNode.localPosition = _originalPosition;
		_highlightedNode.localRotation = _originalRotation;
		_highlightedNode.localScale = _originalScale;
		_highlightedNode = null;
	}

	private void DismissHighlight() => CancelAndDismiss();

	public async UniTaskVoid Hide()
	{
		if (_isHiding) return;
		_isHiding = true;
		if (!gameObject.activeInHierarchy) { CancelAndDismiss(); return; }
		CancellationToken token = _lifetime.Token;
		try
		{
			await UniTask.WaitForSeconds(1f, cancellationToken: token);
			Transform node = _stage == TutorialStage.Level3 ? _arrowTextNode : _textNode;
			if (_bgMask != null)
			{
				Image image = _bgMask.GetComponent<Image>();
				if (image != null) Track(image.DOFade(0f, 0.08f));
			}
			if (node != null)
			{
				Tween tween = node.DOScale(0.2f, 0.08f);
				Track(tween);
				await UniTask.WaitUntil(() => !tween.IsActive() || tween.IsComplete(), cancellationToken: token);
			}
			CancelAndDismiss();
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { }
	}

	private void HideAllNodes()
	{
		SetActive(_textNode, false); SetActive(_arrowTextNode, false); SetActive(_finger, false);
		SetActive(_bigArrow, false); SetActive(_bgMask, false); SetActive(_topMask, false); SetActive(_tapBtn, false);
	}

	private void CancelAnimations()
	{
		_lifetime?.Cancel();
		foreach (Tween tween in _tweens) if (tween.IsActive()) tween.Kill();
		_tweens.Clear();
		if (_tapBtn != null) _tapBtn.onClick.RemoveListener(DismissHighlight);
		if (_bgBtn != null) _bgBtn.onClick.RemoveListener(DismissHighlight);
		Button top = _topMask == null ? null : _topMask.GetComponent<Button>();
		if (top != null) top.onClick.RemoveListener(DismissHighlight);
		RestoreHighlight();
	}

	public void CancelAndDismiss()
	{
		CancelAnimations();
		gameObject.SetActive(false);
		Destroy(gameObject);
	}

	private void OnDisable()
	{
		if (_highlightedNode == null) return;
		_lifetime?.Cancel();
		RestoreHighlight();
	}

	private void OnDestroy() { CancelAnimations(); _lifetime?.Dispose(); }
}
