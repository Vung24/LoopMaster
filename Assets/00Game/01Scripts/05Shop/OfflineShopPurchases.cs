using System;
using UnityEngine;

namespace ShopUI
{
    public static class OfflineShopPurchases
    {
        public static event Action<string, bool> PurchaseFinished;
        public static bool IsBusy { get; private set; }
        public static bool IsAnyPurchased => GamePrefs.GetInt(GamePrefKeys.AnyPurchase, 0) != 0;
        public static int PurchaseCount(string id) => GamePrefs.GetInt(GamePrefKeys.ShopPurchase(ShopProductCatalog.CanonicalId(id)), 0);

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
            if (id == ShopProductCatalog.GoldenPass)
            {
                if (Managers.Pass == null || !Managers.Pass.CanPurchaseVIP()) return false;
                Managers.Pass.SetVip();
                if (!Managers.Pass.HasActiveVip) return false;
                GamePrefs.SetInt(GamePrefKeys.ShopPurchase(id), PurchaseCount(id) + 1);
                GamePrefs.SetInt(GamePrefKeys.AnyPurchase, 1);
                GamePrefs.Save();
                return true;
            }
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
                GamePrefs.SetInt(GamePrefKeys.RemovedAds, 1);
            GamePrefs.SetInt(GamePrefKeys.ShopPurchase(id), PurchaseCount(id) + 1);
            GamePrefs.SetInt(GamePrefKeys.AnyPurchase, 1);
            GamePrefs.Save();
            return true;
        }
    }
}
