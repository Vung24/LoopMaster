using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShopUI
{
	public class ShopBundleItem : ShopItemBase
	{
		[Title("UI Elements")]
		[SerializeField] private TMP_Text _desc;
		[SerializeField] private TMP_Text _priceOld;
		[SerializeField] private TMP_Text _ribbonText;
		[SerializeField] private Transform _rewardsRoot;
		[SerializeField] private bool _usePrefabDetails;

		[BoxGroup("Bundle Config")]
		[ListDrawerSettings(ShowIndexLabels = true, Expanded = true)]
		[InlineEditor(InlineEditorObjectFieldModes.Foldout)]
		[SerializeField] private List<ShopBundleSO> _bundleConfigs = new List<ShopBundleSO>();

		[BoxGroup("Bundle Config")]
		[InlineEditor]
		[SerializeField] private ShopBundleDatabaseSO _bundleDatabase;

		[BoxGroup("Bundle Config")]
		[InlineEditor]
		[SerializeField] private ShopBundleSO _currentBundle;

		public List<ShopBundleSO> BundleConfigs => _bundleConfigs;
		public ShopBundleSO CurrentBundle => _currentBundle;

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
			// Native ShopBundleItem only inherits Init; its rewards and labels are authored in each prefab.
			if (_usePrefabDetails) return;
			ResolveBundleConfig(type);
			ConfigureBundleDetails();
			PopulateRewards();
		}

		private void ResolveBundleConfig(string type)
		{
			// 1. If currently assigned bundle matches, keep it
			if (_currentBundle != null && _currentBundle.Matches(type))
			{
				return;
			}

			// 2. Search local inspector list of bundle SOs
			if (_bundleConfigs != null)
			{
				for (int i = 0; i < _bundleConfigs.Count; i++)
				{
					var bundle = _bundleConfigs[i];
					if (bundle != null && bundle.Matches(type))
					{
						_currentBundle = bundle;
						return;
					}
				}
			}

			// 3. Search assigned database
			if (_bundleDatabase != null)
			{
				var bundle = _bundleDatabase.FindBundle(type);
				if (bundle != null)
				{
					_currentBundle = bundle;
					return;
				}
			}

			// 4. Search global singleton database
			var globalDb = ShopBundleDatabaseSO.Instance;
			if (globalDb != null)
			{
				var bundle = globalDb.FindBundle(type);
				if (bundle != null)
				{
					_currentBundle = bundle;
					return;
				}
			}

			_currentBundle = null;
		}

		private void ConfigureBundleDetails()
		{
			if (_currentBundle == null) return;

			if (_desc != null) _desc.text = _currentBundle.Title;
			if (_priceOld != null) _priceOld.text = _currentBundle.OldPrice;
			if (_ribbonText != null) _ribbonText.text = _currentBundle.RibbonText;
		}

		private void PopulateRewards()
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

			// Remove old items immediately from layout, including during editor preview.
			for (int i = _rewardsRoot.childCount - 1; i >= 0; i--)
			{
				var child = _rewardsRoot.GetChild(i).gameObject;
				if (Application.isPlaying)
				{
					child.SetActive(false);
					Destroy(child);
				}
#if UNITY_EDITOR
				else
				{
					UnityEditor.Undo.DestroyObjectImmediate(child);
				}
#endif
			}

			if (_currentBundle == null) return;

			var rewards = _currentBundle.GetRewardPairs();
			for (int i = 0; i < rewards.Count; i++)
			{
				var (sprite, amount) = rewards[i];
				CreateRewardItem(lib.ShopRewardItemPrefab, sprite, amount);
			}
		}

		private void CreateRewardItem(GameObject prefab, Sprite sprite, string amount)
		{
			if (prefab == null || _rewardsRoot == null) return;
			var go = Instantiate(prefab, _rewardsRoot, false);
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Preview Bundle Rewards");
			}
#endif
			var icon = HierarchyReferenceCache.ChildComponent<Image>(go.transform, "Icon")
				?? HierarchyReferenceCache.DescendantComponent<Image>(go.transform, "Icon");
			if (icon != null)
			{
				icon.sprite = sprite;
				icon.preserveAspect = true;
				icon.enabled = sprite != null;
			}

			var txt = HierarchyReferenceCache.ChildComponent<TMP_Text>(go.transform, "Text")
				?? HierarchyReferenceCache.DescendantComponent<TMP_Text>(go.transform, "Text");
			if (txt != null)
			{
				txt.text = amount;
			}
		}

#if UNITY_EDITOR
		[Button("Preview Bundle Layout"), BoxGroup("Editor Preview")]
		private void EditorPreviewBundle()
		{
			if (_currentBundle == null && _bundleConfigs != null && _bundleConfigs.Count > 0)
			{
				_currentBundle = _bundleConfigs[0];
			}
			ConfigureBundleDetails();
			PopulateRewards();
		}
#endif
	}
}
