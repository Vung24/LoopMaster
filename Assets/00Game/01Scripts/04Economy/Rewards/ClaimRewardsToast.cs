using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui
{
	public class ClaimRewardsToast : MonoBehaviour
	{
		public static UniTask<ClaimRewardsToast> Show(List<RewardItem> rewards, Transform parent, float x = 0f, float y = 0f)
		{
			if (parent == null || rewards == null || rewards.Count == 0)
			{
				return UniTask.FromResult<ClaimRewardsToast>(null);
			}

			var go = new GameObject("ClaimRewardsToast", typeof(RectTransform), typeof(CanvasGroup));
			go.transform.SetParent(parent, false);
			go.transform.localPosition = new Vector3(x, y, 0f);

			var toast = go.AddComponent<ClaimRewardsToast>();
			Destroy(go, 2.5f);
			return UniTask.FromResult(toast);
		}
	}
}