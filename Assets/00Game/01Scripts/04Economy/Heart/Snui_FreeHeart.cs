using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	[RequireComponent(typeof(Button))]
	[RequireComponent(typeof(Snui_HeartHud))]
	public sealed class Snui_FreeHeart : MonoBehaviour
	{
		public GameObject FreeHeartGo;
		private TMP_Text _freeHeartCountText;
		private Snui_HeartHud _heartHud;
		private Button _button;

		private void Awake()
		{
			_heartHud = GetComponent<Snui_HeartHud>();
			_button = GetComponent<Button>();
			if (FreeHeartGo != null) _freeHeartCountText = FreeHeartGo.GetComponentInChildren<TMP_Text>(true);
		}

		private void Start()
		{
			if (_button != null) { _button.onClick.RemoveListener(OnClicked); _button.onClick.AddListener(OnClicked); }
			UpdateFreeHeart();
		}

		private void UpdateFreeHeart()
		{
			int count = Mathf.Max(0, GamePrefs.GetInt(GamePrefKeys.FreeHearts, 0));
			if (FreeHeartGo != null) FreeHeartGo.SetActive(count > 0);
			if (_freeHeartCountText != null) _freeHeartCountText.SetText(count.ToString());
		}

		private void OnClicked()
		{
			int count = Mathf.Max(0, GamePrefs.GetInt(GamePrefKeys.FreeHearts, 0));
			if (count <= 0) { _heartHud?.PlayShakeAnimation(); return; }
			GamePrefs.SetInt(GamePrefKeys.FreeHearts, count - 1);
			GamePrefs.SetInt(GamePrefKeys.BonusHeartCount, GamePrefs.GetInt(GamePrefKeys.BonusHeartCount, 5) + 1);
			GamePrefs.Save();
			_heartHud?.OnHeartCountChanged();
			UpdateFreeHeart();
		}

		private void OnDestroy() { if (_button != null) _button.onClick.RemoveListener(OnClicked); }
	}
}
