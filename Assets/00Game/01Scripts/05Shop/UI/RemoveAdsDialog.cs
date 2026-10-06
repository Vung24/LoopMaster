using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ShopUI
{
	public class RemoveAdsDialog : MonoBehaviour
	{
		public const string PATH = "ShopUI/Prefabs/RemoveAdsDialog.prefab";
		[SerializeField] private RemoveAdsDialogContent _removeAdsDialogContent;

		public static async UniTask<RemoveAdsDialog> Show(Transform parent, string productId, string route)
		{
			var prefab = Resources.Load<GameObject>("ShopUI/Prefabs/RemoveAdsDialog")
				?? Resources.Load<GameObject>("Shop/Prefab/RemoveAdsDialog")
				?? Snui.DataPrefabLibrary.Current?.Resolve(PATH);

			RemoveAdsDialog dialog = null;
			if (prefab != null)
			{
				var go = Instantiate(prefab, parent);
				dialog = go.GetComponent<RemoveAdsDialog>();
			}

			if (dialog != null)
			{
				dialog.StartAsync(productId, route).Forget();
			}

			await UniTask.Yield();
			return dialog;
		}

		private void Awake()
		{
			if (_removeAdsDialogContent == null)
			{
				_removeAdsDialogContent = HierarchyReferenceCache.ChildComponent<RemoveAdsDialogContent>(transform, "Content")
					?? HierarchyReferenceCache.DescendantComponent<RemoveAdsDialogContent>(transform, "Content")
					?? GetComponentInChildren<RemoveAdsDialogContent>(true);
			}
		}

		private async UniTaskVoid StartAsync(string productId, string route)
		{
			await UniTask.WaitUntil(() => _removeAdsDialogContent != null);
			_removeAdsDialogContent.Init(productId, route);
		}
	}
}