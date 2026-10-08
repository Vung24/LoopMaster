using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.StreakLine
{
	public class StreakLineInfoDialog : DialogContent
	{
		[SerializeField]
		private Button _okButton;
		[SerializeField] private AudioClip _infoContentAudio, _infoWordsAudio;
		public RectTransform tf_bone1, tf_bone2, tf_bone3, tf_bone4, tf_bone5;
		public RectTransform tf_bone6, tf_bone7, tf_bone8, tf_bone9, tf_bone13;
		private readonly List<Tween> _animations = new List<Tween>();

		protected override void Awake()
		{
			base.Awake();
			if (_okButton == null)
			{
				_okButton = GetComponentInChildren<Button>(true);
			}
			if (_okButton != null)
			{
				_okButton.onClick.AddListener(OnClickOk);
			}
			ResetParts();
		}

		protected override void Start()
		{
			base.Start();
			PlayAnimation();
		}

		private void ResetParts()
		{
			foreach (var part in Parts()) if (part != null) part.localScale = new Vector3(0, 0, 1);
			if (_okButton != null) _okButton.enabled = false;
		}

		private RectTransform[] Parts() => new[] { tf_bone1, tf_bone2, tf_bone3, tf_bone4, tf_bone5,
			tf_bone6, tf_bone7, tf_bone8, tf_bone9, tf_bone13 };

		public Sequence PlayAnimation()
		{
			StopAnimations();
			ResetParts();
			var parts = Parts();
			float[] delays = { 0, .27f, .4f, .47f, .57f, .7f, .87f, .93f, 1.07f, 1.67f };
			var main = DOTween.Sequence().SetLink(gameObject);
			_animations.Add(main);
			for (int i = 0; i < parts.Length; i++)
			{
				if (parts[i] == null) continue;
				AudioClip clip = i == 0 || i == 9 ? _infoWordsAudio : i == 1 || i == 4 || i == 7 ? _infoContentAudio : null;
				var reveal = DOTween.Sequence().SetLink(gameObject)
					.AppendCallback(() => AudioManager.Current?.PlayOneShot(clip, gameObject))
					.Append(parts[i].DOScale(new Vector3(1.1f, 1.1f, 1), .27f))
					.Append(parts[i].DOScale(Vector3.one, .17f));
				// Native returns only the Title/Image sequence (.71s); the remaining reveals run independently.
				if (i < 2) main.Insert(delays[i], reveal);
				else { reveal.SetDelay(delays[i]); _animations.Add(reveal); }
			}
			main.OnComplete(() =>
			{
				if (_okButton != null) _okButton.enabled = true;
				if (tf_bone13 != null)
					_animations.Add(DOTween.Sequence().SetLink(gameObject).SetDelay(1)
						.Append(tf_bone13.DOScale(Vector3.one * 1.2f, .15f).From(Vector3.one, false).SetLoops(4, LoopType.Yoyo))
						.AppendInterval(3).SetLoops(-1));
			});
			return main;
		}

		private void StopAnimations()
		{
			foreach (var animation in _animations) animation?.Kill();
			_animations.Clear();
		}
		private void OnDisable() => StopAnimations();
		private void OnDestroy() => StopAnimations();

		private void OnClickOk()
		{
			if (_okButton != null && _okButton.enabled) Dismiss();
		}
	}
}
