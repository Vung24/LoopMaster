using Cysharp.Threading.Tasks;
using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShopUI
{
	public class RemoveAdsDialogContent : DialogContent
	{
		public const string PATH = "ShopUI/Prefabs/RemoveAdsDialogContent.prefab";
		[SerializeField] private Button _buyBtn;
		[SerializeField] private Button _dontBuyBtn;
		[SerializeField] private TMP_Text _price;
		private string _route;
		private string _productId;

		public static async UniTask<RemoveAdsDialogContent> Show(Transform parent, string productId, string route)
		{
			var prefab = Resources.Load<GameObject>("ShopUI/Prefabs/RemoveAdsDialogContent")
				?? Resources.Load<GameObject>("Shop/Prefab/RemoveAdsDialogContent")
				?? Snui.RecoveryPrefabLibrary.Current?.Resolve(PATH);

			RemoveAdsDialogContent content = null;
			if (prefab != null)
			{
				var go = Instantiate(prefab, parent);
				content = go.GetComponent<RemoveAdsDialogContent>();
			}

			if (content != null)
			{
				content.Init(productId, route);
			}

			await UniTask.Yield();
			return content;
		}

		protected override void Awake()
		{
			base.Awake();
			if (_buyBtn == null)
			{
				_buyBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "BuyBtn")
					?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "BuyBtn")
					?? GetComponentInChildren<Button>(true);
			}

			if (_dontBuyBtn == null)
			{
				_dontBuyBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "DontBuyBtn")
					?? HierarchyReferenceCache.ChildComponent<Button>(transform, "CloseBtn")
					?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "DontBuyBtn")
					?? GetComponentInChildren<Button>(true);
			}

			if (_price == null)
			{
				_price = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Price")
					?? HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Price")
					?? GetComponentInChildren<TMP_Text>(true);
			}
		}

		protected override void Start()
		{
			base.Start();
			if (_buyBtn != null)
			{
				_buyBtn.onClick.AddListener(OnBuyBtnClicked);
			}

			if (_dontBuyBtn != null)
			{
				_dontBuyBtn.onClick.AddListener(OnDontBuyBtnClicked);
			}
		}

		private void OnEnable()
		{
		}

		private void OnDisable()
		{
		}

		public void Init(string id, string route)
		{
			_productId = id;
			_route = route;

			if (_price != null)
			{
				_price.text = "$2.99";
			}
		}

		private void OnBuyBtnClicked()
		{
			OnPurchaseFinished(_productId, true);
		}

		private async void OnPurchaseFinished(string product, bool isSuccess)
		{
			if (isSuccess)
			{
				// Remove ads purchased
				PlayerPrefs.SetInt("HasRemovedAds", 1);
				PlayerPrefs.Save();
				Hide();
			}
			await UniTask.Yield();
		}

		private void OnDontBuyBtnClicked()
		{
			Hide();
		}
	}
}