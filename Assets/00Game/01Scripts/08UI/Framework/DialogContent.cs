using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
namespace Snui
{
	public class DialogContent : MonoBehaviour
	{
		public const int RESULT_UNKNOWN = -1;
		public const int RESULT_CLOSE = 0;
		public bool PlayOnStart;
		public int EnterAnim;
		public int ExitAnim;

		[SerializeField] protected Image _bg;
		[SerializeField] protected Snui_Text _title;
		[SerializeField] protected Button _closeBtn;

		protected Canvas _canvas;
		protected CanvasGroup _canvasGroup;
		protected int _result = RESULT_UNKNOWN;

        public event Action<int> Dismissed;
        public int Result { get { return _result; } }

		protected virtual void Awake()
		{
			_canvas = GetComponentInParent<Canvas>();
			_canvasGroup = GetComponent<CanvasGroup>();
			if (_closeBtn == null)
			{
				_closeBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseBtn")
						 ?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseButton");
			}
			BindButton(_closeBtn, OnClickCloseButton);
		}

		protected virtual void Start()
		{
			if (PlayOnStart) Show();
		}

		public virtual void Show()
		{
            _result = RESULT_UNKNOWN;
			gameObject.SetActive(true);
			SetAlpha(1f);
			if (_canvasGroup != null)
			{
				_canvasGroup.interactable = true;
				_canvasGroup.blocksRaycasts = true;
			}
		}

		public virtual void Dismiss(int result = RESULT_CLOSE)
		{
            if (_result != RESULT_UNKNOWN) return;
			_result = result;
			Hide();
            Dismissed?.Invoke(result);
		}

		public virtual void Hide()
		{
			if (_canvasGroup != null)
			{
				_canvasGroup.interactable = false;
				_canvasGroup.blocksRaycasts = false;
			}
			gameObject.SetActive(false);
		}

		public virtual void ShowAfterHide() { Show(); }

		public void SetTitle(string value)
		{
			if (_title != null) _title.SetText(value);
		}

		public void SetAlpha(float alpha)
		{
			if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Clamp01(alpha);
		}

		protected virtual bool OnAndroidBackButtonPressed()
		{
			OnClickCloseButton();
			return true;
		}

        public bool HandleBackButton() => isActiveAndEnabled && OnAndroidBackButtonPressed();

		protected virtual void OnClickCloseButton() { Dismiss(RESULT_CLOSE); }

		private static void BindButton(Button button, UnityAction callback)
		{
			if (button == null) return;
			button.onClick.RemoveListener(callback);
			button.onClick.AddListener(callback);
		}
	}
}

