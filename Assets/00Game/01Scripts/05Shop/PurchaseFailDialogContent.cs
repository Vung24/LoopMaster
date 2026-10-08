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
			if (_continueBtn == null)
			{
				_continueBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "ContinueBtn")
					?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "ContinueBtn")
					?? GetComponentInChildren<Button>(true);
			}

			if (_closeBtn == null)
			{
				_closeBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "CloseButton")
					?? HierarchyReferenceCache.ChildComponent<Button>(transform, "CloseBtn")
					?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseButton")
					?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseBtn");
			}
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
