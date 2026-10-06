using System;
using UnityEngine;

namespace ShopUI
{
    // Local reward simulation for this project's existing offline mode; no store payment.
    public static class OfflineShopPurchases
    {
        public static event Action<string, bool> PurchaseFinished;
        public static bool IsBusy { get; private set; }
        public static bool IsAnyPurchased => PlayerPrefs.GetInt("offline_shop_any_purchase", 0) != 0;
        public static int PurchaseCount(string id) => PlayerPrefs.GetInt("offline_shop_purchase_" + ShopProductCatalog.CanonicalId(id), 0);

        public static void Purchase(string id, string route)
        {
            if (IsBusy) return;
            id = ShopProductCatalog.CanonicalId(id);
            bool success = false;
            IsBusy = true;
            try
            {
                success = Grant(id, route);
            }
            finally
            {
                IsBusy = false;
                PurchaseFinished?.Invoke(id, success);
            }
        }

		private static bool Grant(string id, string route)
        {
            if (new ShopDelegate().IsLimitShowProduct(id)) return false;
            var rewards = ShopProductCatalog.GetRewards(id);
            if (rewards == null) return false;
            foreach (var reward in rewards)
                if (reward.Type == RewardType.InfinityHeart) HeartManager.EnsureInitialized();
            if (rewards.Count > 0)
            {
                var inventory = InventoryManager.Ensure();
                if (!inventory.TryClaimRewards("offline-shop/" + Guid.NewGuid().ToString("N"), rewards, out var result)) return false;
                if (result == RewardClaimResult.Claimed)
                    foreach (var reward in rewards)
                        if (reward.Type == RewardType.InfinityHeart) Managers.Heart.AddInfinite(reward.Count);
            }
            if (id == ShopProductCatalog.NoAds || id == ShopProductCatalog.NoAdsPlus)
                PlayerPrefs.SetInt("HasRemovedAds", 1);
            PlayerPrefs.SetInt("offline_shop_purchase_" + id, PurchaseCount(id) + 1);
            PlayerPrefs.SetInt("offline_shop_any_purchase", 1);
            PlayerPrefs.Save();
            return true;
        }
    }
}
