using System.Collections.Generic;

namespace ShopUI
{
    public static class ShopProductCatalog
    {
        public const string Beginner = "loop.beginner.bundle";
        public const string NoAds = "loop.lifetime.noads";
        public const string NoAdsPlus = "loop.lifetime.noads.plus";
        public const string GoldenPass = "loop.golden.pass";
        public const string MoreOffers = "more.offers";

        public static string CanonicalId(string id)
        {
            switch ((id ?? "").Trim().ToLowerInvariant())
            {
                case "beginbundle": case "beginnerbundle": case "begin_bundle": return Beginner;
                case "removead": case "removeads": case "remove_ads": return NoAds;
                case "removeadbundle": case "remove_ad_bundle": return NoAdsPlus;
                case "masterbundle": case "master_bundle": return "loop.master.bundle";
                case "megabundle": case "mega_bundle": return "loop.mega.bundle";
                case "ultrabundle": case "ultra_bundle": return "loop.ultra.bundle";
                case "giantbundle": case "giant_bundle": return "loop.giant.bundle";
                case "legendarybundle": case "legendary_bundle": return "loop.legendary.bundle";
                case "moreoffers": case "more_offers": case "moreoffer": return MoreOffers;
                default: return (id ?? "").Trim().ToLowerInvariant();
            }
        }

        public static int CoinAmount(string id)
        {
            switch (CanonicalId(id))
            {
                case "loop.coins.1000": return 1000;
                case "loop.coins.5000": return 5000;
                case "loop.coins.10000": return 10000;
                case "loop.coins.25000": return 25000;
                case "loop.coins.50000": return 50000;
                case "loop.coins.100000": return 100000;
                case "coins_500": return 500;
                case "coins_1200": return 1200;
                case "coins_2500": return 2500;
                case "coins_6500": return 6500;
                case "coins_14000": return 14000;
                default: return 0;
            }
        }

        public static List<RewardItem> GetRewards(string id)
        {
            int coins = CoinAmount(id);
            if (coins > 0) return new List<RewardItem> { RewardItem.Coin(coins) };
            switch (CanonicalId(id))
            {
                case Beginner: return Bundle(2000, 3600, 1);
                case "loop.master.bundle": return Bundle(4500, 21600, 2);
                case "loop.mega.bundle": return Bundle(10000, 43200, 4);
                case "loop.ultra.bundle": return Bundle(25000, 86400, 8);
                case "loop.giant.bundle": return Bundle(50000, 172800, 16);
                case "loop.legendary.bundle": return Bundle(65000, 259200, 30);
                case NoAdsPlus: return Bundle(3000, 0, 1);
                case NoAds: return new List<RewardItem>();
                default: return null;
            }
        }

        private static List<RewardItem> Bundle(int coins, int infiniteSeconds, int boosters)
        {
            var result = new List<RewardItem> { RewardItem.Coin(coins) };
            if (infiniteSeconds > 0) result.Add(new RewardItem(RewardType.InfinityHeart, infiniteSeconds));
            result.Add(new RewardItem(RewardType.Undo, boosters));
            result.Add(new RewardItem(RewardType.Shuffle, boosters));
            result.Add(new RewardItem(RewardType.Path, boosters));
            return result;
        }
    }
}
