using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Snui
{
	[RequireComponent(typeof(Button))]
	[RequireComponent(typeof(CanvasGroup))]
	public class Snui_Button : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
		IPointerEnterHandler, IPointerExitHandler,
		IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		[HideInInspector] public UnityEvent OnPointerDownEvent = new UnityEvent();
		[HideInInspector] public UnityEvent OnPointerUpEvent = new UnityEvent();
		public bool EnableClickAudio = true;
		public bool EnableVibration = true;
		public ButtonPreventClickType PreventClickType = ButtonPreventClickType.Repeatedly;
		public float PreventDuration = 0.2f;
		public bool UsePressDownEffect = true;
		public float DownOffset = 4f;
		[Range(0f, 1f)] public float ClickScale = 1f;
		[Range(0f, 1f)] public float ClickAlpha = 0.3f;
		[Range(0f, 1f)] public float DisableAlpha = 1f;
		public bool AnimateWholeButton = false;

		private Transform _content;
		private Button _button;
		private CanvasGroup _canvasGroup;
		private float _dragThreshold = 32f;
		private CanvasGroup _contentCanvasGroup;
		private Vector3 _contentLocalPos;
		private Vector3 _contentLocalScale;
		private Vector2 _beginDragPos;
		private Coroutine _preventClickRoutine;
		private bool _pressed;
		private bool _isNavigationCell;

		public Button Button
		{
			get
			{
				if (_button == null) _button = GetComponent<Button>();
				return _button;
			}
		}

		public CanvasGroup CanvasGroup
		{
			get
			{
				if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
				return _canvasGroup;
			}
		}

		private void Awake()
		{
			_button = GetComponent<Button>();
			_isNavigationCell = GetComponent<NavigationCell>() != null;
			_canvasGroup = GetComponent<CanvasGroup>();
			_content = AnimateWholeButton ? transform : (transform.childCount > 0 ? transform.GetChild(0) : transform);

			if (_content != transform)
			{
				_contentCanvasGroup = _content.GetComponent<CanvasGroup>();
				if (_contentCanvasGroup == null)
					_contentCanvasGroup = _content.gameObject.AddComponent<CanvasGroup>();
			}
			else
			{
				_contentCanvasGroup = _canvasGroup;
			}

			if (EventSystem.current != null)
				_dragThreshold = Mathf.Max(_dragThreshold, EventSystem.current.pixelDragThreshold);
		}

		private void Start()
		{
			if (Button == null) return;
			Button.onClick.RemoveListener(HandleClick);
			Button.onClick.AddListener(HandleClick);
		}

		private void HandleClick()
		{
			PlayClickButtonSound();
			PlayVibration();

			switch (PreventClickType)
			{
				case ButtonPreventClickType.Repeatedly:
					DisableButtonQuickTouch();
					break;
				case ButtonPreventClickType.OnlyOnce:
					DisableButton();
					break;
			}
		}

		private void PlayClickButtonSound()
		{
			if (!isActiveAndEnabled || !EnableClickAudio || !GameAudioSettings.EffectsEnabled) return;
			AudioClip clip = GameAssetsConfig.Instance?.ButtonClickAudio;
			if (clip == null) return;
			if (GameAudioPlayer.Current != null) GameAudioPlayer.Current.PlayOneShot(clip);
			else
			{
				AudioSource source = GetComponent<AudioSource>();
				if (source == null)
				{
					source = gameObject.AddComponent<AudioSource>();
					source.playOnAwake = false;
				}
				if (source.isActiveAndEnabled) source.PlayOneShot(clip);
			}
		}

		private void PlayVibration()
		{
			if (!isActiveAndEnabled || !EnableVibration) return;
			if (_isNavigationCell)
			{
				GameVibrationHelper.PlayNavigationTap();
				return;
			}
			GameVibrationHelper.PlayTiny();
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			if (Button == null || !Button.IsInteractable() || _pressed) return;
			if (_content != null)
			{
				_contentLocalPos = _content.localPosition;
				_contentLocalScale = _content.localScale;
			}
			_pressed = true;
			ApplyPressedVisual();
			OnPointerDownEvent?.Invoke();
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			bool isInside = eventData.pointerCurrentRaycast.gameObject == gameObject;
			ReleasePressedVisual(isInside);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (_pressed)
			{
				ReleasePressedVisual(false);
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (eventData.dragging && eventData.pointerPress == gameObject && Button != null && Button.IsInteractable())
			{
				_pressed = true;
				ApplyPressedVisual();
			}
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
			_beginDragPos = eventData.position;
			if (GetComponentInParent<ScrollRect>() != null)
				ReleasePressedVisual(false);
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (GetComponentInParent<ScrollRect>() != null && Vector2.Distance(_beginDragPos, eventData.position) > _dragThreshold)
				ReleasePressedVisual(false);
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			if (GetComponentInParent<ScrollRect>() != null)
				ReleasePressedVisual(false);
		}

		public void DisableButton()
		{
			if (Button != null) Button.interactable = false;
			if (_contentCanvasGroup != null) _contentCanvasGroup.alpha = DisableAlpha;
			ReleasePressedVisual(false, false);
		}

		public void EnableButton()
		{
			if (Button != null) Button.interactable = true;
			if (_contentCanvasGroup != null) _contentCanvasGroup.alpha = 1f;
		}

		private void DisableButtonQuickTouch()
		{
			if (!isActiveAndEnabled || Button == null || !Button.IsInteractable()) return;
			if (_preventClickRoutine != null) StopCoroutine(_preventClickRoutine);
			_preventClickRoutine = StartCoroutine(PreventRepeatedClick());
		}

		private IEnumerator PreventRepeatedClick()
		{
			DisableButton();
			yield return new WaitForSecondsRealtime(Mathf.Max(0f, PreventDuration));
			EnableButton();
			_preventClickRoutine = null;
		}

		private void ApplyPressedVisual()
		{
			if (_content == null) return;
			if (UsePressDownEffect)
				_content.localPosition = _contentLocalPos + Vector3.down * DownOffset;
			_content.localScale = _contentLocalScale * ClickScale;
			if (_contentCanvasGroup != null) _contentCanvasGroup.alpha = ClickAlpha;
		}

		private void ReleasePressedVisual(bool invokeEvent, bool restoreAlpha = true)
		{
			bool wasPressed = _pressed;
			_pressed = false;
			if (wasPressed && _content != null)
			{
				_content.localPosition = _contentLocalPos;
				_content.localScale = _contentLocalScale;
			}
			if (restoreAlpha && _contentCanvasGroup != null)
				_contentCanvasGroup.alpha = Button != null && !Button.interactable ? DisableAlpha : 1f;
			if (Button != null && Button.targetGraphic is Image img)
				img.overrideSprite = null;
			if (invokeEvent && wasPressed) OnPointerUpEvent?.Invoke();
		}

		private void OnDisable()
		{
			ReleasePressedVisual(false);
		}

		private void OnDestroy()
		{
			if (_button != null) _button.onClick.RemoveListener(HandleClick);
		}
	}
}
