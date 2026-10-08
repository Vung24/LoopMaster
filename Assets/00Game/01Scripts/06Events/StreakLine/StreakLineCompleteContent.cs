using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.StreakLine
{
	public class StreakLineCompleteContent : DialogContent
	{
		[SerializeField]
		private Button _okButton;
		[SerializeField] private AudioClip _completeContentAudio;
		private Tween _popup, _exit;
		private bool _shown, _closing;

		public override void Show()
		{
			base.Show();
			if (_shown) return;
			_shown = true;
			transform.localScale = new Vector3(.98f, .98f, 1);
			_popup = DOTween.Sequence().SetLink(gameObject)
				.Append(transform.DOScale(new Vector3(1.01f, 1.01f, 1), .1f))
				.Append(transform.DOScale(Vector3.one, .13f));
		}

		protected override void Start()
		{
			base.Start();
			AudioManager.Current?.PlayOneShot(_completeContentAudio, gameObject);
		}

		public override void Dismiss(int result = RESULT_CLOSE)
		{
			if (_closing || Result != RESULT_UNKNOWN) return;
			_closing = true;
			_popup?.Kill();
			if (_canvasGroup == null) { base.Dismiss(result); return; }
			_canvasGroup.interactable = false;
			_exit = _canvasGroup.DOFade(0, .03f).SetLink(gameObject).OnComplete(() => base.Dismiss(result));
		}

		private void OnDestroy() { _popup?.Kill(); _exit?.Kill(); }

		public static async UniTask<StreakLineCompleteContent> ShowWithDialog(Transform parent)
		{
			var settings = StreakLineSettings.Instance;
			if (settings != null)
			{
				return await Dialog.ShowContent<StreakLineCompleteContent>(parent, "StreakLineCompleteContent");
			}
			return null;
		}

		protected override void Awake()
		{
			base.Awake();
			if (_okButton == null) _okButton = GetComponentInChildren<Button>(true);
			if (_okButton != null) _okButton.onClick.AddListener(OnCLickOkButton);
		}

		private void OnCLickOkButton()
		{
			Dismiss();
		}
	}
}
