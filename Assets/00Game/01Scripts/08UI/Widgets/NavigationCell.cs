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
		public float IconNormalPosition = -120f, IconSelectPosition = -77f;
		[FormerlySerializedAs("IconScale")] public float SelectedScale = 1f;
		public float UnselectedScale = 0.9f;
		[SerializeField] private Sprite _normalSprite, _selectedSprite;
		[SerializeField] private Image _icon;
		[SerializeField] private TMP_Text _text;
		[SerializeField] private Transform _line;
		private Button _button;
		private Tween _iconTween;
		private Vector3 _authoredScale;
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
				_authoredScale = _icon.transform.localScale;
				if (_normalSprite == null) _normalSprite = _icon.sprite;
				if (_selectedSprite == null) _selectedSprite = _icon.sprite;
			}
			if (_button != null) _button.onClick.AddListener(HandleClick);
		}
		private void HandleClick() { if (isActiveAndEnabled && IsAvailable) OnClick.Invoke(); }
		private void OnEnable() => ApplySelection(IsSelected, false);
		public void SetAvailable(bool value) { if (_button != null) _button.interactable = value; }
		public void Init(bool select, float normalWidth, float selectWidth) => ApplySelection(select, false);
		public void SetSelected(bool select, NavigationAnim navigationAnim) => ApplySelection(select, true);
		private void ApplySelection(bool select, bool animate)
		{
			IsSelected = select;
			_iconTween?.Kill();
			if (_icon == null) return;
			_icon.sprite = select ? _selectedSprite : _normalSprite;
			Vector3 scale = _authoredScale * (select ? SelectedScale : UnselectedScale);
			// Native Default animates icon scale for 0.16s; other navigation styles are deferred.
			if (animate && isActiveAndEnabled) _iconTween = _icon.transform.DOScale(scale, 0.16f).SetUpdate(true);
			else _icon.transform.localScale = scale;
		}
		public void SetLineActive(bool value) { if (_line != null) _line.gameObject.SetActive(value); }
		private void OnDisable() { _iconTween?.Kill(); _iconTween = null; }
		private void OnDestroy() { OnDisable(); if (_button != null) _button.onClick.RemoveListener(HandleClick); OnClick.RemoveAllListeners(); }
	}
}
