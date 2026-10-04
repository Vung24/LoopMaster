using TMPro;
using UnityEngine;

namespace Snui
{
	public class HomeEventLock : MonoBehaviour
	{
		[SerializeField] private TMP_Text _unlockLevelText;

		private void Awake()
		{
			if (_unlockLevelText == null)
			{
				_unlockLevelText = GetComponentInChildren<TMP_Text>(true);
			}
		}

		public void SetUnlockLevel(int lv)
		{
			if (_unlockLevelText != null)
			{
				_unlockLevelText.text = $"Lv.{lv}";
			}
		}
	}
}