using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snui
{
	public class SettingButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
	{
		public enum Type { None = 0, Sound = 1, Music = 2, Vibration = 3, Language = 4, ContactUs = 5,
			About = 6, Restore = 7, Sign = 8, DeleteAccount = 9, Term = 10, Privacy = 11,
			Personal = 12, Nickname = 13, ColorBlind = 14 }
		public UnityAction<Type> OnClickCellButton;
		[SerializeField] private Type _type;
		[SerializeField, Range(0f, 1f)] private float _clickAlpha = 0.3f;
		[SerializeField] private Button _button;
		[SerializeField] private CanvasGroup _canvasGroup;
		[SerializeField] private Transform _toggleOn, _toggleOff;
		[SerializeField] private TMPro.TMP_Text _label;
		private float _originAlpha;
		private bool _pressed;
		public Type ButtonType => _type;
		public bool IsOfflineToggle => _type == Type.Sound || _type == Type.Music || _type == Type.Vibration;

		private void Awake()
		{
			if (_button == null) _button = GetComponent<Button>();
			if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
			if (_toggleOn == null) _toggleOn = HierarchyReferenceCache.Child(transform, "ToggleOn");
			if (_toggleOff == null) _toggleOff = HierarchyReferenceCache.Child(transform, "ToggleOff");
			if (_label == null)
			{
				var label = HierarchyReferenceCache.Child(transform, "Text");
				_label = label == null ? GetComponentInChildren<TMPro.TMP_Text>(true) : label.GetComponent<TMPro.TMP_Text>();
			}
			if (_type == Type.Sound && _label != null)
			{
				_label.text = "Sound";
			}
			if (_button != null) _button.onClick.AddListener(OnClickButton);
		}
		private void OnEnable() => RefreshToggle();
		public void RefreshToggle()
		{
			bool on = _type == Type.Music ? GameAudioSettings.MusicEnabled :
				_type == Type.Sound ? GameAudioSettings.EffectsEnabled : GameVibrationHelper.Enabled;
			if (_toggleOn != null) _toggleOn.gameObject.SetActive(IsOfflineToggle && on);
			if (_toggleOff != null) _toggleOff.gameObject.SetActive(IsOfflineToggle && !on);
		}
		private void OnClickButton()
		{
			if (!isActiveAndEnabled || !IsOfflineToggle || _button == null || !_button.IsInteractable()) return;
			if (GetComponent<Snui_Button>() == null)
			{
				AudioClip clip = GameAssetsConfig.Instance?.ButtonClickAudio;
				if (GameAudioSettings.EffectsEnabled && clip != null)
				{
					if (GameAudioPlayer.Current != null) GameAudioPlayer.Current.PlayOneShot(clip);
					else
					{
						var source = GetComponent<AudioSource>();
						if (source == null) { source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; }
						source.PlayOneShot(clip);
					}
				}
				GameVibrationHelper.PlayBoxTap(false);
			}
			OnClickCellButton?.Invoke(_type);
			RefreshToggle();
		}
		public void OnPointerDown(PointerEventData eventData)
		{
			if (_pressed || _button == null || !_button.IsInteractable() || _canvasGroup == null) return;
			_pressed = true; _originAlpha = _canvasGroup.alpha; _canvasGroup.alpha = _clickAlpha;
		}
		public void OnPointerUp(PointerEventData eventData) => RestorePress();
		private void RestorePress() { if (_pressed && _canvasGroup != null) _canvasGroup.alpha = _originAlpha; _pressed = false; }
		private void OnDisable() => RestorePress();
		private void OnDestroy() { if (_button != null) _button.onClick.RemoveListener(OnClickButton); OnClickCellButton = null; }
	}
}
