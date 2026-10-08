using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class EventInfoDialogFourRow : DialogContent
	{
		public AudioClip ContentEffect;
		public AudioClip WordsEffect;
		public Transform tf_tutorial_1;
		public Transform tf_TP_tutorial_2;
		public Transform tf_TP_tutorial_3;
		public Transform tf_TP_tutorial_4;
		public Transform tf_TP_tutorial_5;
		public Transform tf_TP_tutorial_6;
		public Transform tf_TP_tutorial_7;
		public Transform tf_TP_tutorial_8;
		private Button _continueBtn;

		protected override void Awake()
		{
			base.Awake();
			if (_continueBtn == null)
			{
				var btnTrans = HierarchyReferenceCache.Descendant(transform, "ContinueBtn");
				if (btnTrans != null) _continueBtn = btnTrans.GetComponent<Button>();
				if (_continueBtn == null) _continueBtn = GetComponentInChildren<Button>(true);
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
			Transform[] tutorials = new Transform[] { tf_tutorial_1, tf_TP_tutorial_2, tf_TP_tutorial_3, tf_TP_tutorial_4, tf_TP_tutorial_5, tf_TP_tutorial_6, tf_TP_tutorial_7, tf_TP_tutorial_8 };
			var seq = DOTween.Sequence();
			float delay = 0.06f;
			for (int i = 0; i < tutorials.Length; i++)
			{
				if (tutorials[i] != null)
				{
					tutorials[i].localScale = Vector3.zero;
					seq.Insert(i * delay, tutorials[i].DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
				}
			}
			seq.SetLink(gameObject);
		}

		protected virtual void OnClickClose()
		{
			Dismiss(RESULT_CLOSE);
		}
	}
}