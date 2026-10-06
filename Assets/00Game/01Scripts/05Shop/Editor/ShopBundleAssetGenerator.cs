#if UNITY_EDITOR
using System.IO;
using Sirenix.OdinInspector;
using Snui;
using UnityEditor;
using UnityEngine;

namespace ShopUI.Editor
{
	public static class ShopBundleAssetGenerator
	{
		private const string RewardsPath = "Assets/00Game/04Resources/Shop/Rewards";
		private const string BundlesPath = "Assets/00Game/04Resources/Shop/Bundles";
		private const string DatabasePath = "Assets/00Game/Resources";

		[MenuItem("Tools/Shop/Generate Default Bundle & Reward Assets")]
		public static void GenerateDefaultAssets()
		{
			EnsureDirectory(RewardsPath);
			EnsureDirectory(BundlesPath);
			EnsureDirectory(DatabasePath);

			var lib = DataPrefabLibrary.Current;

			// 1. Create Reward SOs
			var rCoin1000 = GetOrCreateReward("Reward_Coin_1000", "coin_1000", "1,000 Coins", lib != null ? lib.RewardCoinSprite : null, "1,000");
			var rCoin3000 = GetOrCreateReward("Reward_Coin_3000", "coin_3000", "3,000 Coins", lib != null ? lib.RewardCoinSprite : null, "3,000");
			var rCoin7000 = GetOrCreateReward("Reward_Coin_7000", "coin_7000", "7,000 Coins", lib != null ? lib.RewardCoinSprite : null, "7,000");
			var rCoin15000 = GetOrCreateReward("Reward_Coin_15000", "coin_15000", "15,000 Coins", lib != null ? lib.RewardCoinSprite : null, "15,000");

			var rInfHeart30m = GetOrCreateReward("Reward_InfHeart_30m", "inf_heart_30m", "30m Unlimited Heart", lib != null ? lib.RewardInfinityHeartSprite : null, "30m");
			var rInfHeart1h = GetOrCreateReward("Reward_InfHeart_1h", "inf_heart_1h", "1h Unlimited Heart", lib != null ? lib.RewardInfinityHeartSprite : null, "1h");
			var rInfHeart2h = GetOrCreateReward("Reward_InfHeart_2h", "inf_heart_2h", "2h Unlimited Heart", lib != null ? lib.RewardInfinityHeartSprite : null, "2h");
			var rInfHeart6h = GetOrCreateReward("Reward_InfHeart_6h", "inf_heart_6h", "6h Unlimited Heart", lib != null ? lib.RewardInfinityHeartSprite : null, "6h");

			var rUndo3 = GetOrCreateReward("Reward_Undo_3", "undo_3", "x3 Undo", lib != null ? lib.RewardUndoSprite : null, "x3");
			var rUndo5 = GetOrCreateReward("Reward_Undo_5", "undo_5", "x5 Undo", lib != null ? lib.RewardUndoSprite : null, "x5");
			var rUndo10 = GetOrCreateReward("Reward_Undo_10", "undo_10", "x10 Undo", lib != null ? lib.RewardUndoSprite : null, "x10");
			var rUndo20 = GetOrCreateReward("Reward_Undo_20", "undo_20", "x20 Undo", lib != null ? lib.RewardUndoSprite : null, "x20");

			var rShuffle5 = GetOrCreateReward("Reward_Shuffle_5", "shuffle_5", "x5 Shuffle", lib != null ? lib.RewardShuffleSprite : null, "x5");
			var rShuffle10 = GetOrCreateReward("Reward_Shuffle_10", "shuffle_10", "x10 Shuffle", lib != null ? lib.RewardShuffleSprite : null, "x10");
			var rShuffle20 = GetOrCreateReward("Reward_Shuffle_20", "shuffle_20", "x20 Shuffle", lib != null ? lib.RewardShuffleSprite : null, "x20");

			var rPath10 = GetOrCreateReward("Reward_Path_10", "path_10", "x10 Path", lib != null ? lib.RewardPathSprite : null, "x10");

			// 2. Create Bundle SOs
			var beginBundle = GetOrCreateBundle("Bundle_Beginner", "beginbundle", "BEGINNER BUNDLE", "$4.99", "SAVE 60%",
				new[] { "begin", "beginner", "begin_bundle" },
				new[] { rCoin1000, rInfHeart30m, rUndo3 });

			var removeAdBundle = GetOrCreateBundle("Bundle_NoAds", "removead", "NO ADS BUNDLE", "$5.99", "BEST VALUE!",
				new[] { "removeads", "remove_ad", "removeadbundle" },
				new[] { rCoin1000, rInfHeart1h });

			var megaBundle = GetOrCreateBundle("Bundle_Mega", "megabundle", "MEGA BUNDLE", "$9.99", "SAVE 50%",
				new[] { "mega", "mega_bundle" },
				new[] { rCoin3000, rInfHeart1h, rUndo5, rShuffle5 });

			var ultraBundle = GetOrCreateBundle("Bundle_Ultra", "ultrabundle", "ULTRA BUNDLE", "$19.99", "SAVE 50%",
				new[] { "ultra", "ultra_bundle" },
				new[] { rCoin7000, rInfHeart2h, rUndo10, rShuffle10, rPath10 });

			var masterBundle = GetOrCreateBundle("Bundle_Master", "masterbundle", "MASTER BUNDLE", "$39.99", "SAVE 50%",
				new[] { "master", "master_bundle" },
				new[] { rCoin15000, rInfHeart6h, rUndo20, rShuffle20 });

			var legendaryBundle = GetOrCreateBundle("Bundle_Legendary", "legendarybundle", "LEGENDARY BUNDLE", "$99.99", "SAVE 50%",
				new[] { "legendary", "legendary_bundle" },
				new[] { rCoin15000, rInfHeart6h, rUndo20, rShuffle20 });

			// 3. Create or update Database SO
			string dbAssetPath = Path.Combine(DatabasePath, "ShopBundleDatabase_SO.asset").Replace("\\", "/");
			var db = AssetDatabase.LoadAssetAtPath<ShopBundleDatabaseSO>(dbAssetPath);
			if (db == null)
			{
				db = ScriptableObject.CreateInstance<ShopBundleDatabaseSO>();
				AssetDatabase.CreateAsset(db, dbAssetPath);
			}

			db.Clear();
			db.RegisterBundle(beginBundle);
			db.RegisterBundle(removeAdBundle);
			db.RegisterBundle(megaBundle);
			db.RegisterBundle(ultraBundle);
			db.RegisterBundle(masterBundle);
			db.RegisterBundle(legendaryBundle);
			EditorUtility.SetDirty(db);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log("[ShopBundleAssetGenerator] Successfully generated default Bundle & Reward SO assets and Database at " + dbAssetPath);
		}

		private static void EnsureDirectory(string path)
		{
			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}
		}

		private static ShopRewardSO GetOrCreateReward(string fileName, string id, string displayName, Sprite icon, string defaultAmount)
		{
			string assetPath = $"{RewardsPath}/{fileName}.asset";
			var asset = AssetDatabase.LoadAssetAtPath<ShopRewardSO>(assetPath);
			if (asset == null)
			{
				asset = ScriptableObject.CreateInstance<ShopRewardSO>();
				AssetDatabase.CreateAsset(asset, assetPath);
			}

			asset.Initialize(id, displayName, icon, defaultAmount);
			EditorUtility.SetDirty(asset);
			return asset;
		}

		private static ShopBundleSO GetOrCreateBundle(string fileName, string productId, string title, string oldPrice, string ribbonText, string[] aliases, ShopRewardSO[] rewards)
		{
			string assetPath = $"{BundlesPath}/{fileName}.asset";
			var asset = AssetDatabase.LoadAssetAtPath<ShopBundleSO>(assetPath);
			if (asset == null)
			{
				asset = ScriptableObject.CreateInstance<ShopBundleSO>();
				AssetDatabase.CreateAsset(asset, assetPath);
			}

			asset.Initialize(productId, title, oldPrice, ribbonText, aliases);
			for (int i = 0; i < rewards.Length; i++)
			{
				if (rewards[i] != null)
				{
					asset.AddReward(rewards[i]);
				}
			}

			EditorUtility.SetDirty(asset);
			return asset;
		}
	}
}
#endif

