using TMPro;
using UnityEngine;

namespace Snui
{
	[RequireComponent(typeof(TMP_Text))]
	public class Snui_Price : MonoBehaviour
	{
		private string _id;
		[SerializeField] private TMP_Text _tmpText;
		[SerializeField] private Transform _loading;

		private void Awake()
		{
			if (_tmpText == null) _tmpText = GetComponent<TMP_Text>();
			if (_loading == null) _loading = HierarchyReferenceCache.Child(transform, "Loading");
			if (_loading != null)
			{
				_loading.gameObject.SetActive(false);
			}
		}

		public void SetId(string id)
		{
			_id = id;
			if (_loading != null) _loading.gameObject.SetActive(false);
			if (_tmpText != null && !string.IsNullOrEmpty(id))
			{
				_tmpText.text = GetPriceString(id);
			}
		}

		public void SetIdWithDiscount(string id, float discount)
		{
			SetId(id);
		}

		public void SetPrice(string id)
		{
			SetId(id);
		}

		public static string GetPriceString(string id)
		{
			if (string.IsNullOrEmpty(id)) return "$0.99";
			switch (ShopUI.ShopProductCatalog.CanonicalId(id))
			{
				case "loop.coins.1000": return "$1.99";
				case "loop.coins.5000": return "$7.99";
				case "loop.coins.10000": return "$14.99";
				case "loop.coins.25000": return "$29.99";
				case "loop.coins.50000": return "$54.99";
				case "loop.coins.100000": return "$99.99";
				case ShopUI.ShopProductCatalog.Beginner: return "$1.99";
				case "loop.master.bundle": return "$9.99";
				case "loop.mega.bundle": return "$19.99";
				case "loop.ultra.bundle": return "$39.99";
				case "loop.giant.bundle": return "$79.99";
				case "loop.legendary.bundle": return "$99.99";
				case ShopUI.ShopProductCatalog.NoAds: return "$7.99";
				case ShopUI.ShopProductCatalog.NoAdsPlus: return "$9.99";
				case ShopUI.ShopProductCatalog.GoldenPass: return "$9.99";
				case "coins_500": return "$0.99";
				case "coins_1200": return "$1.99";
				case "coins_2500": return "$3.99";
				case "coins_6500": return "$9.99";
				case "coins_14000": return "$19.99";
				default:
					if (id.IndexOf("coin", System.StringComparison.OrdinalIgnoreCase) >= 0) return "$0.99";
					if (id.IndexOf("bundle", System.StringComparison.OrdinalIgnoreCase) >= 0) return "$4.99";
					return "$0.99";
			}
		}
	}
}
