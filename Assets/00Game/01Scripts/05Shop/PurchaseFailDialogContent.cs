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
			var settings = ShopUISettings_SO.Instance;
			if (settings != null && settings.PurchaseFailDialogContent != null && settings.PurchaseFailDialogContent.RuntimeKeyIsValid())
			{
				return await Dialog.ShowContent<PurchaseFailDialogContent>(parent, settings.PurchaseFailDialogContent, 0.03f);
			}

			var prefab = DataPrefabLibrary.Current?.PurchaseFailDialogContentPrefab
				?? DataPrefabLibrary.Current?.Resolve("PurchaseFailDialogContent")
				?? Resources.Load<GameObject>("Shop/Prefab/PurchaseFailDialogContent");
			if (prefab != null)
			{
				var go = Instantiate(prefab, parent, false);
				var content = go.GetComponent<PurchaseFailDialogContent>();
				return content;
			}

			return null;
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
				_closeBtn.onClick.AddListener(Hide);
			}
			if (_continueBtn != null)
			{
				_continueBtn.onClick.AddListener(OnContinueClicked);
			}
		}

		private void OnContinueClicked()
		{
			Hide();
		}
	}
}
