using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Snui
{
	public class PassToastItemBase : MonoBehaviour
	{
		public UnityEvent<PassToastItemBase, string, List<RewardItem>, object[]> ShowToastHandle = new UnityEvent<PassToastItemBase, string, List<RewardItem>, object[]>();
		public Transform ToastTop, ToastBottom;
		protected Button _button;
		protected virtual void Awake()
		{
			_button = GetComponent<Button>();
			ToastTop = HierarchyReferenceCache.Child(transform, "ToastTop");
			ToastBottom = HierarchyReferenceCache.Child(transform, "ToastBottom");
		}
		protected void Preview(List<RewardItem> rewards)
		{
			RewardDetailToast.Show(rewards, transform.GetComponentInParent<Canvas>()?.rootCanvas.transform,
				(ToastTop ?? transform).position).Forget();
		}
	}
}
