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
		public virtual List<string> GetShopProducts()
		{
			var list = new List<string>
			{
				ShopProductCatalog.GoldenPass, ShopProductCatalog.Beginner,
				"loop.master.bundle", "loop.mega.bundle", "loop.ultra.bundle",
				"loop.giant.bundle", "loop.legendary.bundle",
				"loop.coins.1000", "loop.coins.5000", "loop.coins.10000",
				"loop.coins.25000", "loop.coins.50000", "loop.coins.100000"
			};
			if (GamePrefs.GetInt(GamePrefKeys.RemoveAdsOffer, 0) == 1)
			{
				list.Insert(1, ShopProductCatalog.NoAdsPlus);
				list.Insert(2, ShopProductCatalog.NoAds);
			}
			return list;
		}

		public virtual bool IsLimitShowProduct(string type)
		{
			type = ShopProductCatalog.CanonicalId(type);
			if (type == ShopProductCatalog.GoldenPass) return Managers.Pass == null || !Managers.Pass.CanPurchaseVIP();
			if (type == ShopProductCatalog.Beginner) return OfflineShopPurchases.PurchaseCount(type) > 0;
			if (type == ShopProductCatalog.NoAds || type == ShopProductCatalog.NoAdsPlus)
			{
				return GamePrefs.GetInt(GamePrefKeys.RemovedAds, 0) == 1;
			}
			return false;
		}

		public void ClaimFreeCoins(int amount)
		{
			InventoryManager.Ensure().Add(InventoryKeys.COIN, amount, "shop_free_coin");
		}

		public virtual string GetProductResPath(string id)
		{
			if (string.IsNullOrEmpty(id)) return "";
			switch (ShopProductCatalog.CanonicalId(id))
			{
				case ShopProductCatalog.GoldenPass:
					return "Events/PassBundleItem";
				case ShopProductCatalog.Beginner:
				case "loop.master.bundle":
				case "loop.mega.bundle":
				case "loop.ultra.bundle":
				case "loop.giant.bundle":
				case "loop.legendary.bundle":
					return "04Prefabs/Shop/BundleBase.prefab";
				case ShopProductCatalog.NoAds:
					return "ShopUI/Prefab/RemoveAdItem.prefab";
				case ShopProductCatalog.NoAdsPlus:
					return "ShopUI/Prefab/RemoveAdBundleItem.prefab";
				case ShopProductCatalog.MoreOffers:
					return "ShopUI/Prefabs/MoreOffer.prefab";
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
			var list = new List<string>();
			if (!OfflineShopPurchases.IsAnyPurchased)
			{
				list.Add(ShopProductCatalog.Beginner);
				list.Add("loop.master.bundle");
				list.Add("loop.coins.1000");
			}
			else
			{
				if (OfflineShopPurchases.PurchaseCount(ShopProductCatalog.Beginner) == 0)
					list.Add(ShopProductCatalog.Beginner);
				list.Add("loop.master.bundle");
				if (OfflineShopPurchases.PurchaseCount(ShopProductCatalog.Beginner) > 0)
					list.Add("loop.mega.bundle");
				list.Add("loop.coins.5000");
			}
			list.Add("loop.coins.10000");
			list.Add(ShopProductCatalog.MoreOffers);
			return list;
		}
	}
}

public class ShopDelegate : ShopUI.ShopDelegate { }
public class MiniShopDelegate : ShopUI.MiniShopDelegate { }
