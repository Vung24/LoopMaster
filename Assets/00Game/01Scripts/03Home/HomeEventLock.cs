using TMPro;
using UnityEngine;

namespace Snui
{
	public class HomeEventLock : MonoBehaviour
	{
		public void SetUnlockLevel(int lv)
		{
			int curLevel = GameProgressStore.NextPlayableLevel;
			bool isLocked = curLevel < lv;
			gameObject.SetActive(isLocked);
			if (isLocked)
			{
				var text = HierarchyReferenceCache.Child(transform, "Text")?.GetComponent<TMP_Text>();
				if (text == null)
				{
					text = GetComponentInChildren<TMP_Text>(true);
				}
				if (text != null)
				{
					text.text = $"Lv.{lv}";
				}
			}
		}
	}
}