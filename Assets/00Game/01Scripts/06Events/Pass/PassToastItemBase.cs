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
		[SerializeField] public Transform ToastTop;
		[SerializeField] public Transform ToastBottom;
		[SerializeField] protected Button _button;
		protected virtual void Awake()
		{
			if (_button == null) _button = GetComponent<Button>();
			if (ToastTop == null) ToastTop = HierarchyReferenceCache.Child(transform, "ToastTop");
			if (ToastBottom == null) ToastBottom = HierarchyReferenceCache.Child(transform, "ToastBottom");
		}
		protected void Preview(List<RewardItem> rewards)
		{
			RewardDetailToast.Show(rewards, transform.GetComponentInParent<Canvas>()?.rootCanvas.transform,
				(ToastTop ?? transform).position).Forget();
		}
	}
}
