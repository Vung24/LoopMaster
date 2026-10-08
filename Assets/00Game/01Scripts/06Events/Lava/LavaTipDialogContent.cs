using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Snui.Lava
{
	public class LavaTipDialogContent : DialogContent
	{
		[SerializeField] private Snui_FullButton _fullButton;
		[SerializeField] private Transform _image1;
		[SerializeField] private Transform _image2;
		[SerializeField] private Transform _image3;
		[SerializeField] private Transform _arrow1;
		[SerializeField] private Transform _arrow2;
		[SerializeField] private Transform _titleNode;
		[SerializeField] private Transform _textButton;
		[SerializeField] private TMP_Text _info1Text;
		[SerializeField] private TMP_Text _info4Text;

		public static UniTask<LavaTipDialogContent> Show(Transform parent) => EventPrefabUI.Show<LavaTipDialogContent>(parent, "LavaTipDialog");



		public static async UniTask ShowWithDialog(Transform parent)
		{
			await Show(parent);
		}

		protected override void Awake()
		{
			base.Awake();
			if (_fullButton == null)
			{
				_fullButton = HierarchyReferenceCache.ChildComponent<Snui_FullButton>(transform, "FullButton");
			}
			if (_image1 == null)
			{
				_image1 = HierarchyReferenceCache.Child(transform, "Image1");
			}
			if (_image2 == null)
			{
				_image2 = HierarchyReferenceCache.Child(transform, "Image2");
			}
			if (_image3 == null)
			{
				_image3 = HierarchyReferenceCache.Child(transform, "Image3");
			}
			if (_arrow1 == null)
			{
				_arrow1 = HierarchyReferenceCache.Child(transform, "Arrow1");
			}
			if (_arrow2 == null)
			{
				_arrow2 = HierarchyReferenceCache.Child(transform, "Arrow2");
			}
			if (_titleNode == null)
			{
				_titleNode = HierarchyReferenceCache.Child(transform, "Title");
			}
			if (_textButton == null && _fullButton != null)
			{
				_textButton = HierarchyReferenceCache.Child(_fullButton.transform, "Text");
			}
			if (_info1Text == null && _image1 != null)
			{
				_info1Text = HierarchyReferenceCache.ChildComponent<TMP_Text>(_image1, "Text");
			}
			if (_info4Text == null)
			{
				_info4Text = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Info4");
			}

			if (Managers.Lava != null)
			{
				if (_info1Text != null)
				{
					_info1Text.text = $"Compete with {Managers.Lava.GetPlayerCount()} players. Win levels to jump between platforms; losing ends your round.";
				}
				if (_info4Text != null)
				{
					_info4Text.text = $"Reach platform {Managers.Lava.GetLevelGoal()} to share the prize with the surviving players.";
				}
			}
		}

		protected override void Start()
		{
			base.Start();

			if (_fullButton != null && _fullButton.Button != null)
			{
				_fullButton.Button.onClick.AddListener(OnClickFullBtn);
				_fullButton.DisableButton();
			}

			InitStatus();
			StartCoroutine(PlayComeInAnimation());
		}

		private void InitStatus()
		{
			if (_titleNode != null) _titleNode.localScale = Vector3.zero;
			if (_image1 != null) _image1.localScale = Vector3.zero;
			if (_image2 != null) _image2.localScale = Vector3.zero;
			if (_image3 != null) _image3.localScale = Vector3.zero;
			if (_arrow1 != null) _arrow1.localScale = Vector3.zero;
			if (_arrow2 != null) _arrow2.localScale = Vector3.zero;
			if (_textButton != null) _textButton.localScale = Vector3.zero;
		}

		private IEnumerator PlayComeInAnimation()
		{
			Transform[] sequence = new Transform[] { _titleNode, _image1, _arrow1, _image2, _arrow2, _image3, _textButton };
			foreach (Transform t in sequence)
			{
				if (t != null)
				{
					t.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetLink(t.gameObject);
					yield return new WaitForSeconds(0.1f);
				}
			}

			OnAnimationEnd();
		}

		private void OnAnimationEnd()
		{
			if (_fullButton != null)
			{
				_fullButton.EnableButton();
			}
		}

		private void OnClickFullBtn()
		{
			Dismiss();
		}
	}
}