using TMPro;
using UnityEngine;

namespace Snui.Lava
{
	public class LavaPrize : MonoBehaviour
	{
		[SerializeField] private TMP_Text _coinText;

		private void Awake()
		{
			if (_coinText == null)
			{
				_coinText = GetComponentInChildren<TMP_Text>(true);
			}
		}

		private void Start()
		{
			if (_coinText != null && Managers.Lava != null)
			{
				_coinText.text = Managers.Lava.GetCoinRewardCount().ToString();
			}
		}
	}
}