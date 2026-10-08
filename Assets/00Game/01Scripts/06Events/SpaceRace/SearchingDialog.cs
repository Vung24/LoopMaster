using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui.SpaceRace
{
	public class SearchingDialog : MonoBehaviour
	{
		private const string PREFAB_PATH = "SpaceRace/Prefab/SearchingDialog.prefab";

		public static UniTask<SearchingDialog> Show() => EventPrefabUI.Show<SearchingDialog>(null, "SearchingDialog");

		public void Dismiss()
		{
			if (TryGetComponent<Snui_Panel>(out var panel))
			{
				Snui_Panel.DestroyPanel(gameObject);
			}
			else
			{
				Destroy(gameObject);
			}
		}
	}
}