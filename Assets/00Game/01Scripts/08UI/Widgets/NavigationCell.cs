using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Snui
{
	public class NavigationCell : MonoBehaviour
	{
		public float IconNormalPosition = -86f, IconSelectPosition = -30f;
		[FormerlySerializedAs("IconScale")] public float SelectedScale = 0.9f;
		public float UnselectedScale = 0.8f;
		[SerializeField] private Sprite _normalSprite, _selectedSprite;
		[SerializeField] private Image _icon;
		[SerializeField] private TMP_Text _text;
		[SerializeField] private Transform _line;
		private Button _button;
		private Tween _iconTween;
		private Tween _positionTween, _textTween;
		public UnityEvent OnClick { get; } = new UnityEvent();
		public bool IsSelected { get; private set; }
		public bool IsAvailable => _button != null && _button.interactable;
		private void Awake()
		{
			_button = GetComponent<Button>();
			if (_icon == null) _icon = HierarchyReferenceCache.Component<Image>(HierarchyReferenceCache.Child(transform, "Icon"));
			if (_text == null) _text = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Child(transform, "Text"));
			if (_line == null) _line = HierarchyReferenceCache.Child(transform, "Line");
			if (_icon != null)
			{
				_icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
				if (_normalSprite == null) _normalSprite = _icon.sprite;
				if (_selectedSprite == null) _selectedSprite = _icon.sprite;
			}
			if (_button != null) _button.onClick.AddListener(HandleClick);
		}
		private void HandleClick() { if (isActiveAndEnabled && IsAvailable) OnClick.Invoke(); }
		public void SetAvailable(bool value) { if (_button != null) _button.interactable = value; }
		public void Init(bool select, float normalWidth, float selectWidth)
		{
			StopAnimation();
			IsSelected = select;
			((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, select ? selectWidth : normalWidth);
			if (_icon != null)
			{
				_icon.sprite = select ? _selectedSprite : _normalSprite;
				_icon.SetNativeSize();
				_icon.transform.localScale = Vector3.one * (select ? SelectedScale : UnselectedScale);
				var position = _icon.rectTransform.anchoredPosition;
				position.y = select ? IconSelectPosition : IconNormalPosition;
				_icon.rectTransform.anchoredPosition = position;
			}
			if (_text != null) { var color = _text.color; color.a = select ? 1f : 0f; _text.color = color; }
		}
		public void SetSelected(bool select, NavigationAnim navigationAnim)
		{
			if (IsSelected == select) return;
			StopAnimation(); IsSelected = select;
			if (_icon == null) return;
			_icon.sprite = select ? _selectedSprite : _normalSprite;
			_icon.SetNativeSize();
			float scale = select ? SelectedScale : UnselectedScale;
			if (!select && navigationAnim != NavigationAnim.Default)
			{
				_icon.transform.localScale = Vector3.one * scale;
				var position = _icon.rectTransform.anchoredPosition;
				position.y = IconNormalPosition;
				_icon.rectTransform.anchoredPosition = position;
				if (_text != null) _textTween = _text.DOFade(0f, 0.1f).SetUpdate(true).SetLink(gameObject);
				return;
			}
			if (navigationAnim == NavigationAnim.PoppedUp && select)
			{
				var position = _icon.rectTransform.anchoredPosition;
				position.y = IconSelectPosition;
				_icon.rectTransform.anchoredPosition = position;
				_icon.transform.localScale = Vector3.one * UnselectedScale;
				_iconTween = DOTween.Sequence().Append(_icon.transform.DOScale(scale * 1.1f, 0.12f))
					.Append(_icon.transform.DOScale(scale, 0.08f)).SetUpdate(true).SetLink(gameObject);
			}
			else
			{
				if (select && navigationAnim == NavigationAnim.WidthChangesDynamically)
					_icon.transform.localScale = Vector3.one * (SelectedScale + 0.12f);
				_iconTween = _icon.transform.DOScale(scale, navigationAnim == NavigationAnim.Default ? 0.16f : 0.3f).SetUpdate(true).SetLink(gameObject);
			}
			if (navigationAnim == NavigationAnim.WidthChangesDynamically)
				_positionTween = _icon.rectTransform.DOAnchorPosY(IconSelectPosition, 0.3f).SetUpdate(true).SetLink(gameObject);
			if (navigationAnim != NavigationAnim.Default && _text != null)
				_textTween = _text.DOFade(1f, 0.1f).SetUpdate(true).SetLink(gameObject);
		}
		public void SetIconPivot(Vector2 pivot) { if (_icon != null) _icon.rectTransform.pivot = pivot; }
		private void StopAnimation()
		{
			_iconTween?.Kill(); _positionTween?.Kill(); _textTween?.Kill();
			_iconTween = _positionTween = _textTween = null;
		}
		public void SetLineActive(bool value) { if (_line != null) _line.gameObject.SetActive(value); }
		private void OnDisable() => StopAnimation();
		private void OnDestroy() { OnDisable(); if (_button != null) _button.onClick.RemoveListener(HandleClick); OnClick.RemoveAllListeners(); }
	}
}
