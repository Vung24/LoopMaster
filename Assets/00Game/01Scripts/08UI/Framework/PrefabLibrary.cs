using UnityEngine;

namespace Snui
{
    public sealed class PrefabLibrary : ScriptableObject
    {
        [System.Serializable]
        public class EventPrefabEntry { public string Name; public GameObject Prefab; }
        public EventPrefabEntry[] EventPrefabs;
        public GameObject FingerPrefab;
        public GameObject FlyCoinPrefab;
		public GameObject AddNumberToastPrefab;
        public GameObject HudContainerPrefab;
        public GameObject HomeIconNotifierPrefab;
        public GameObject HomeIconProgressPrefab;
        public GameObject SpecialStreakProgressPrefab;
        public GameObject SpecialStreakCellPrefab;
        public GameObject SpecialStreakCollectItemPrefab;
        public GameObject SpecialStreakFailBannerPrefab;
        public GameObject LoseSpecialStreakPrefab;
        public GameObject LevelBoxMachinePrefab;
        public GameObject RewardDetailToastPrefab;
        public GameObject RewardCellPrefab;
        public GameObject CheatItemSelectorPrefab;
        public Sprite RewardCoinSprite, RewardHeartSprite, RewardInfinityHeartSprite;
        public Sprite RewardUndoSprite, RewardShuffleSprite, RewardPathSprite;
        public Shader LevelBoxSpineShader, LevelBoxSpineAdditiveShader, LevelBoxSpineMultiplyShader, LevelBoxSpineScreenShader;
        public Sprite HudCoinSprite;
        public GameObject NewPlayerTutorialPrefab;
        public GameObject NewPlayerTutorialBluePrefab;
        public GameObject FailTutorialPrefab;
        public GameObject FailTutorialBluePrefab;
        public GameObject ShopCoinItemPrefab;
        public GameObject ShopBundleItemPrefab;
        public GameObject ShopNoAdsItemPrefab, ShopNoAdsPlusItemPrefab;
        public GameObject ShopMoreOffersItemPrefab;
        public GameObject ShopRewardItemPrefab;
        public GameObject ShopRibbonPrefab;
        public Sprite[] CoinSprites;
        private static PrefabLibrary current;

        public static PrefabLibrary Current
        {
            get
            {
                if (current == null) current = Resources.Load<PrefabLibrary>("PrefabLibrary");
                return current;
            }
        }

        public GameObject Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path.EndsWith("CheatItemSelector.prefab")) return CheatItemSelectorPrefab;
            path = path.Replace('\\', '/');
            string eventName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (EventPrefabs != null)
                foreach (var entry in EventPrefabs)
                    if (entry != null && entry.Name == eventName && entry.Prefab != null) return entry.Prefab;
            if (path == "Events/Common/HomeIconNotifier" || path == "UI/Common/HomeIconNotifier") return HomeIconNotifierPrefab;
            if (path == "Events/Common/HomeIconProgress" || path == "UI/Common/HomeIconProgress") return HomeIconProgressPrefab;
            if (path == "Snui/Common/Prefabs/Finger.prefab") return FingerPrefab;
            if (path == "Snui/Common/Prefabs/FlyCoin.prefab") return FlyCoinPrefab;
            if (path == "Common/Prefab/HudContainer.prefab" || path == "Common/Prefab/HudContainer") return HudContainerPrefab;
            if (path.EndsWith("SpecialStreakProgress.prefab")) return SpecialStreakProgressPrefab;
            if (path.EndsWith("SpecialStreakCell.prefab")) return SpecialStreakCellPrefab;
            if (path.EndsWith("SpecialStreakCollectItem.prefab")) return SpecialStreakCollectItemPrefab;
            if (path.EndsWith("SpecialStreakFailBanner.prefab")) return SpecialStreakFailBannerPrefab;
            if (path.EndsWith("LoseSpecialStreakNode.prefab")) return LoseSpecialStreakPrefab;
            if (path == "Home/LevelBox/Prefab/LevelBoxMachine.prefab" || path == "LevelBox/Prefab/LevelBoxMachine.prefab") return LevelBoxMachinePrefab;
            if (path == "Snui/Common/Prefabs/RewardDetailToast.prefab") return RewardDetailToastPrefab;
            if (path == "Snui/Common/Prefabs/RewardCell.prefab") return RewardCellPrefab;
            switch (System.IO.Path.GetFileNameWithoutExtension(path))
            {
                case "BeginBundleItem":
                case "MasterBundleItem":
                case "MegaBundleItem":
                case "UltraBundleItem":
                case "GiantBundleItem":
                case "LegendaryBundleItem": return ShopBundleItemPrefab;
                case "RemoveAdItem": return ShopNoAdsItemPrefab;
                case "RemoveAdBundleItem": return ShopNoAdsPlusItemPrefab;
            }
            if (path.IndexOf("MoreOffer", System.StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("moreoffers", System.StringComparison.OrdinalIgnoreCase) >= 0) return ShopMoreOffersItemPrefab;
            if (path.IndexOf("CoinItem", System.StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("coins_", System.StringComparison.OrdinalIgnoreCase) >= 0) return ShopCoinItemPrefab;
            if (path.IndexOf("ShopRewardItem", System.StringComparison.OrdinalIgnoreCase) >= 0) return ShopRewardItemPrefab;
            if (path.IndexOf("ShopRibbon", System.StringComparison.OrdinalIgnoreCase) >= 0) return ShopRibbonPrefab;
            if (path.IndexOf("Bundle", System.StringComparison.OrdinalIgnoreCase) >= 0) return ShopBundleItemPrefab;
            return Resources.Load<GameObject>(path.EndsWith(".prefab") ? path.Substring(0, path.Length - 7) : path);
        }
        public Sprite ResolveRewardIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path == RewardType.Coin.GetIconPath()) return RewardCoinSprite;
            if (path == RewardType.Heart.GetIconPath()) return RewardHeartSprite;
            if (path == RewardType.InfinityHeart.GetIconPath()) return RewardInfinityHeartSprite;
            if (path == RewardType.Undo.GetIconPath()) return RewardUndoSprite;
            if (path == RewardType.Shuffle.GetIconPath()) return RewardShuffleSprite;
            if (path == RewardType.Path.GetIconPath()) return RewardPathSprite;
            return null;
        }
    }
}
