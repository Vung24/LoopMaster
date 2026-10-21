using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class EventInfoDialog : DialogContent
	{
		public AudioClip ContentEffect;
		public AudioClip WordsEffect;
		public Transform tf_bone1;
		public Transform tf_bone2;
		public Transform tf_bone3;
		public Transform tf_bone4;
		public Transform tf_bone5;
		public Transform tf_bone6;
		public Transform tf_bone7;
		public Transform tf_bone8;
		public Transform tf_bone9;
		public Transform BtnText;
		[SerializeField] protected Button _continueBtn;

		protected override void Awake()
		{
			base.Awake();
			if (_continueBtn == null)
			{
				var btnTrans = HierarchyReferenceCache.Descendant(transform, "ContinueBtn")
							 ?? HierarchyReferenceCache.Descendant(transform, "ContinueButton");
				if (btnTrans != null) _continueBtn = btnTrans.GetComponent<Button>();
				if (_continueBtn == null) _continueBtn = _closeBtn ?? GetComponentInChildren<Button>(true);
			}
			if (_continueBtn != null)
			{
				_continueBtn.onClick.AddListener(OnClickClose);
			}
		}

		protected override void Start()
		{
			base.Start();
			PlayTutorialAnims();
		}

		protected virtual void PlayTutorialAnims()
		{
			Transform[] bones = new Transform[] { tf_bone1, tf_bone2, tf_bone3, tf_bone4, tf_bone5, tf_bone6, tf_bone7, tf_bone8, tf_bone9 };
			var seq = DOTween.Sequence();
			seq.InsertCallback(0f, PlayWordsSound);
			float delay = 0.05f;
			for (int i = 0; i < bones.Length; i++)
			{
				if (bones[i] != null)
				{
					bones[i].localScale = Vector3.zero;
					seq.Insert(i * delay, bones[i].DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
					if (i % 3 == 1) seq.InsertCallback(i * delay, PlayContentSound);
				}
			}
			if (BtnText != null)
			{
				BtnText.localScale = Vector3.zero;
				seq.Insert(bones.Length * delay, BtnText.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
				seq.InsertCallback(bones.Length * delay, PlayWordsSound);
			}
			seq.SetLink(gameObject, LinkBehaviour.KillOnDisable);
		}

		private void PlayContentSound()
		{
			if (AudioManager.Current != null) AudioManager.Current.PlayOneShot(ContentEffect, gameObject);
		}

		private void PlayWordsSound()
		{
			if (AudioManager.Current != null) AudioManager.Current.PlayOneShot(WordsEffect, gameObject);
		}

		protected virtual void OnClickClose()
		{
			Dismiss(RESULT_CLOSE);
		}
	}
}
