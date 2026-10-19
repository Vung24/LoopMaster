using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;
using UnityEngine.UI;

namespace ShopUI
{
	public class PurchaseFailDialogContent : DialogContent
	{
		[SerializeField] private Button _continueBtn;

		public static async UniTask<PurchaseFailDialogContent> Show(Transform parent)
		{
			if (parent == null) return null;
			return await Dialog.ShowContent<PurchaseFailDialogContent>(parent, "PurchaseFailDialogContent");
		}

		protected override void Awake()
		{
			base.Awake();
		}

		protected override void Start()
		{
			base.Start();
			if (_closeBtn != null)
			{
				_closeBtn.onClick.AddListener(OnContinueClicked);
			}
			if (_continueBtn != null)
			{
				_continueBtn.onClick.AddListener(OnContinueClicked);
			}
		}

		private void OnContinueClicked()
		{
			Dismiss();
		}
	}
}
