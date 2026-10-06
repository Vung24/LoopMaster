using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShopUI
{
	public interface IShopDelegate
	{
		List<string> GetShopProducts();
		bool IsLimitShowProduct(string id);
		[Obsolete] void ClaimFreeCoins(int amount);
		string GetProductResPath(string id);
	}

	public class ShopDelegate : IShopDelegate
	{
		private GameObject _loading;

		public virtual List<string> GetShopProducts()
		{
			var list = new List<string>();
			if (!IsLimitShowProduct("beginbundle")) list.Add("beginbundle");
			if (!IsLimitShowProduct("removead")) list.Add("removead");
			list.Add("coins_500");
			list.Add("coins_1200");
			list.Add("coins_2500");
			list.Add("coins_6500");
			list.Add("coins_14000");
			if (!IsLimitShowProduct("megabundle")) list.Add("megabundle");
			if (!IsLimitShowProduct("ultrabundle")) list.Add("ultrabundle");
			if (!IsLimitShowProduct("masterbundle")) list.Add("masterbundle");
			if (!IsLimitShowProduct("legendarybundle")) list.Add("legendarybundle");
			list.Add("moreoffers");
			return list;
		}

		public virtual bool IsLimitShowProduct(string type)
		{
			if (string.IsNullOrEmpty(type)) return false;
			if (type.Equals("removead", StringComparison.OrdinalIgnoreCase) || type.Equals("removeads", StringComparison.OrdinalIgnoreCase))
			{
				return PlayerPrefs.GetInt("HasRemovedAds", 0) == 1;
			}
			return false;
		}

		public void ClaimFreeCoins(int amount)
		{
			int currentCoins = PlayerPrefs.GetInt("GameCoins", 1000);
			PlayerPrefs.SetInt("GameCoins", currentCoins + amount);
			PlayerPrefs.Save();
		}

		public virtual string GetProductResPath(string id)
		{
			if (string.IsNullOrEmpty(id)) return "";
			switch (id.ToLowerInvariant())
			{
				case "beginbundle":
				case "beginnerbundle":
				case "begin_bundle":
					return "ShopUI/Prefab/BeginBundleItem.prefab";
				case "removead":
				case "remove_ads":
				case "removeads":
					return "ShopUI/Prefab/RemoveAdItem.prefab";
				case "removeadbundle":
				case "remove_ad_bundle":
					return "ShopUI/Prefab/RemoveAdBundleItem.prefab";
				case "megabundle":
				case "mega_bundle":
					return "ShopUI/Prefab/MegaBundleItem.prefab";
				case "ultrabundle":
				case "ultra_bundle":
					return "ShopUI/Prefab/UltraBundleItem.prefab";
				case "giantbundle":
				case "giant_bundle":
					return "ShopUI/Prefab/GiantBundleItem.prefab";
				case "masterbundle":
				case "master_bundle":
					return "ShopUI/Prefab/MasterBundleItem.prefab";
				case "legendarybundle":
				case "legendary_bundle":
					return "ShopUI/Prefab/LegendaryBundleItem.prefab";
				case "moreoffers":
				case "more_offers":
				case "moreoffer":
					return "04Prefabs/Shop/MoreOffer.prefab";
				default:
					if (id.IndexOf("coin", StringComparison.OrdinalIgnoreCase) >= 0)
						return "ShopUI/Prefab/CoinItem.prefab";
					if (id.IndexOf("bundle", StringComparison.OrdinalIgnoreCase) >= 0)
						return "04Prefabs/Shop/BundleBase.prefab";
					return id;
			}
		}
	}

	public class MiniShopDelegate : ShopDelegate
	{
		public override List<string> GetShopProducts()
		{
			return new List<string>
			{
				"coins_500",
				"coins_1200",
				"coins_2500",
				"coins_6500"
			};
		}
	}
}

// Global namespace aliases for parity with Il2Cpp global namespace definitions
public class ShopDelegate : ShopUI.ShopDelegate { }
public class MiniShopDelegate : ShopUI.MiniShopDelegate { }