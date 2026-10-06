using System.Collections.Generic;
using Sirenix.OdinInspector;
using Snui;
using UnityEngine;

namespace ShopUI
{
	[CreateAssetMenu(fileName = "ShopBundleDatabase", menuName = "Shop/Bundle Database")]
	public class ShopBundleDatabaseSO : SingletonScriptableObject<ShopBundleDatabaseSO>
	{
		[Title("Shop Bundle Packages Database", "Centralized Odin Catalog of all bundles")]
		[ListDrawerSettings(ShowIndexLabels = true, Expanded = true)]
		[InlineEditor(InlineEditorObjectFieldModes.Foldout)]
		[SerializeField] private List<ShopBundleSO> _bundles = new List<ShopBundleSO>();

		public IReadOnlyList<ShopBundleSO> Bundles => _bundles;

		public ShopBundleSO FindBundle(string type)
		{
			if (string.IsNullOrEmpty(type) || _bundles == null) return null;
			for (int i = 0; i < _bundles.Count; i++)
			{
				if (_bundles[i] != null && _bundles[i].Matches(type))
				{
					return _bundles[i];
				}
			}
			return null;
		}

		public void RegisterBundle(ShopBundleSO bundle)
		{
			if (bundle == null) return;
			if (_bundles == null) _bundles = new List<ShopBundleSO>();
			if (!_bundles.Contains(bundle))
			{
				_bundles.Add(bundle);
			}
		}

		public void Clear()
		{
			if (_bundles != null)
			{
				_bundles.Clear();
			}
		}
	}
}

