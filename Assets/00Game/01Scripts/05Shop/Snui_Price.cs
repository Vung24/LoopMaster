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
			switch (id.ToLowerInvariant())
			{
				case "coins_500": return "$0.99";
				case "coins_1200": return "$1.99";
				case "coins_2500": return "$3.99";
				case "coins_6500": return "$9.99";
				case "coins_14000": return "$19.99";
				case "beginbundle":
				case "beginnerbundle": return "$1.99";
				case "megabundle": return "$4.99";
				case "ultrabundle": return "$9.99";
				case "giantbundle": return "$14.99";
				case "masterbundle": return "$19.99";
				case "legendarybundle": return "$49.99";
				case "removead":
				case "removeads": return "$2.99";
				default:
					if (id.IndexOf("coin", System.StringComparison.OrdinalIgnoreCase) >= 0) return "$0.99";
					if (id.IndexOf("bundle", System.StringComparison.OrdinalIgnoreCase) >= 0) return "$4.99";
					return "$0.99";
			}
		}
	}
}