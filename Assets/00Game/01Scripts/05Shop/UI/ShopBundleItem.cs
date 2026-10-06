using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShopUI
{
	public class ShopBundleItem : ShopItemBase
	{
		[SerializeField] private TMP_Text _desc;
		[SerializeField] private TMP_Text _priceOld;
		[SerializeField] private TMP_Text _ribbonText;
		[SerializeField] private Transform _rewardsRoot;

		protected override void Awake()
		{
			base.Awake();
			if (_desc == null)
			{
				_desc = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Desc")
					?? HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Desc");
			}

			if (_priceOld == null)
			{
				_priceOld = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "PriceOld")
					?? HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "PriceOld");
			}

			if (_ribbonText == null)
			{
				var ribbon = HierarchyReferenceCache.Child(transform, "Ribbon")
					?? HierarchyReferenceCache.Descendant(transform, "Ribbon");
				if (ribbon != null)
				{
					_ribbonText = ribbon.GetComponentInChildren<TMP_Text>(true);
				}
			}

			if (_rewardsRoot == null)
			{
				_rewardsRoot = HierarchyReferenceCache.Child(transform, "Rewards")
					?? HierarchyReferenceCache.Descendant(transform, "Rewards");
			}
		}

		public override void Init(string type, IShopDelegate shopDelegate)
		{
			base.Init(type, shopDelegate);
			SetPrice(type);
			ConfigureBundleDetails(type);
			PopulateRewards(type);
		}

		private void ConfigureBundleDetails(string type)
		{
			string t = (type ?? "").ToLowerInvariant();
			string title = "SPECIAL BUNDLE";
			string oldPrice = "$4.99";
			string ribbon = "LIMITED OFFER!";

			if (t.Contains("begin"))
			{
				title = "BEGINNER BUNDLE";
				oldPrice = "$4.99";
				ribbon = "SAVE 60%";
			}
			else if (t.Contains("removead"))
			{
				title = "NO ADS BUNDLE";
				oldPrice = "$5.99";
				ribbon = "BEST VALUE!";
			}
			else if (t.Contains("mega"))
			{
				title = "MEGA BUNDLE";
				oldPrice = "$9.99";
				ribbon = "SAVE 50%";
			}
			else if (t.Contains("ultra"))
			{
				title = "ULTRA BUNDLE";
				oldPrice = "$19.99";
				ribbon = "SAVE 50%";
			}
			else if (t.Contains("master"))
			{
				title = "MASTER BUNDLE";
				oldPrice = "$39.99";
				ribbon = "SAVE 50%";
			}
			else if (t.Contains("legendary"))
			{
				title = "LEGENDARY BUNDLE";
				oldPrice = "$99.99";
				ribbon = "SAVE 50%";
			}

			if (_desc != null) _desc.text = title;
			if (_priceOld != null) _priceOld.text = oldPrice;
			if (_ribbonText != null) _ribbonText.text = ribbon;
		}

		private void PopulateRewards(string type)
		{
			if (_rewardsRoot == null) return;

			var lib = RecoveryPrefabLibrary.Current;
			if (lib == null || lib.ShopRewardItemPrefab == null) return;

			// Ensure layout group exists so items are aligned nicely
			var layout = _rewardsRoot.GetComponent<HorizontalLayoutGroup>();
			if (layout == null)
			{
				layout = _rewardsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
				layout.childAlignment = TextAnchor.MiddleCenter;
				layout.spacing = 15f;
				layout.childControlWidth = false;
				layout.childControlHeight = false;
				layout.childForceExpandWidth = false;
				layout.childForceExpandHeight = false;
			}

			// Clear existing dynamic reward items if any
			for (int i = _rewardsRoot.childCount - 1; i >= 0; i--)
			{
				Destroy(_rewardsRoot.GetChild(i).gameObject);
			}

			string t = (type ?? "").ToLowerInvariant();
			if (t.Contains("begin"))
			{
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardCoinSprite, "1,000");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardInfinityHeartSprite, "30m");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardUndoSprite, "x3");
			}
			else if (t.Contains("removead"))
			{
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardCoinSprite, "1,000");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardInfinityHeartSprite, "1h");
			}
			else if (t.Contains("mega"))
			{
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardCoinSprite, "3,000");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardInfinityHeartSprite, "1h");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardUndoSprite, "x5");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardShuffleSprite, "x5");
			}
			else if (t.Contains("ultra"))
			{
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardCoinSprite, "7,000");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardInfinityHeartSprite, "2h");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardUndoSprite, "x10");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardShuffleSprite, "x10");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardPathSprite, "x10");
			}
			else
			{
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardCoinSprite, "15,000");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardInfinityHeartSprite, "6h");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardUndoSprite, "x20");
				CreateRewardItem(lib.ShopRewardItemPrefab, lib.RewardShuffleSprite, "x20");
			}
		}

		private void CreateRewardItem(GameObject prefab, Sprite sprite, string amount)
		{
			if (prefab == null || _rewardsRoot == null) return;
			var go = Instantiate(prefab, _rewardsRoot, false);
			var icon = HierarchyReferenceCache.ChildComponent<Image>(go.transform, "Icon")
				?? HierarchyReferenceCache.DescendantComponent<Image>(go.transform, "Icon");
			if (icon != null && sprite != null)
			{
				icon.sprite = sprite;
			}

			var txt = HierarchyReferenceCache.ChildComponent<TMP_Text>(go.transform, "Text")
				?? HierarchyReferenceCache.DescendantComponent<TMP_Text>(go.transform, "Text");
			if (txt != null)
			{
				txt.text = amount;
			}
		}
	}
}