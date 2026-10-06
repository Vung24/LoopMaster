using TMPro;
using UnityEngine;

namespace Snui
{
	public class HomeEventLock : MonoBehaviour
	{
		[SerializeField] private TMP_Text _text;

		private void Awake()
		{
			if (_text == null)
			{
				_text = GetComponentInChildren<TMP_Text>(true);
			}
		}

		public void SetUnlockLevel(int lv)
		{
			int curLevel = GameProgressStore.NextPlayableLevel;
			bool isLocked = curLevel < lv;
			gameObject.SetActive(isLocked);
			if (isLocked)
			{
				if (_text == null)
				{
					_text = GetComponentInChildren<TMP_Text>(true);
				}
				if (_text != null)
				{
					_text.text = $"Lv.{lv}";
				}
			}
		}
	}
}