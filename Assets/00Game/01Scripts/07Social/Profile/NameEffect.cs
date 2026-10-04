using TMPro;
using UnityEngine;

namespace Snui.Profile
{
	public class NameEffect : MonoBehaviour
	{
		public const string DEFAULT = "default";
		public const string PASS_VIP = "pass";

		[SerializeField] private TMP_Text _text;
		[SerializeField] private TMP_Text _vipText;

		public TMP_Text Text => _text;

		private void Awake()
		{
			if (_text == null)
			{
				_text = GetComponent<TMP_Text>();
			}
		}

		public void SetPlayerInfo(PlayerInfo player)
		{
			if (player == null) return;
			SetName(player.Name);
			SetEffect(player.NameEffect);
		}

		public void SetName(string name)
		{
			if (_text != null) _text.text = name;
			if (_vipText != null) _vipText.text = name;
		}

		public void SetEffect(string effect)
		{
			bool isVip = string.Equals(effect, PASS_VIP, System.StringComparison.OrdinalIgnoreCase);
			if (_vipText != null) _vipText.gameObject.SetActive(isVip);
			if (_text != null) _text.gameObject.SetActive(!isVip);
		}
	}
}