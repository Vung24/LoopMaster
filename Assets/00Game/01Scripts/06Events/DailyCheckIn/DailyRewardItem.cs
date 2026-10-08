using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class DailyRewardItem : MonoBehaviour
	{
		public static string Path = "Common/Prefab/DailyRewardItem";

		[SerializeField]
		private Image _rewardImg;

		[SerializeField]
		private TMP_Text _text;

		private void Awake()
		{
			if (_rewardImg == null)
			{
				_rewardImg = HierarchyReferenceCache.ChildComponent<Image>(transform, "RewardImg");
				if (_rewardImg == null) _rewardImg = GetComponentInChildren<Image>(true);
			}

			if (_text == null)
			{
				_text = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Count");
				if (_text == null) _text = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Text");
				if (_text == null) _text = GetComponentInChildren<TMP_Text>(true);
			}

			// Use a fallback when the reward-item prefab is unavailable.
			if (_rewardImg == null)
			{
				var icon = new GameObject("RewardImg", typeof(RectTransform), typeof(Image));
				icon.transform.SetParent(transform, false);
				_rewardImg = icon.GetComponent<Image>();
				_rewardImg.rectTransform.sizeDelta = new Vector2(56f, 56f);
				_rewardImg.rectTransform.anchoredPosition = new Vector2(0f, 12f);
			}
			if (_text == null)
			{
				var count = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
				count.transform.SetParent(transform, false);
				_text = count.GetComponent<TMP_Text>();
				_text.rectTransform.sizeDelta = new Vector2(80f, 28f);
				_text.rectTransform.anchoredPosition = new Vector2(0f, -30f);
				_text.fontSize = 22f;
				_text.alignment = TextAlignmentOptions.Center;
			}
			_rewardImg.preserveAspect = true;
			foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
		}

		public void Init(RewardItem dailyRewardsContent)
		{
			if (dailyRewardsContent == null) return;
			Snui_Text.EnsureFontAssets(transform);

			if (_text != null)
			{
				if (dailyRewardsContent.Type != null && dailyRewardsContent.Type.IsShowInfinityIcon())
				{
					int minutes = dailyRewardsContent.Count / 60;
					_text.text = minutes > 0 ? $"{minutes}m" : $"{dailyRewardsContent.Count}s";
				}
				else
				{
					_text.text = $"{dailyRewardsContent.Count}";
				}
			}

			if (_rewardImg != null && dailyRewardsContent.Type != null)
			{
				var config = DailyCheckInManager.Instance.DataProvider?.GetRewardConfig(dailyRewardsContent.Type);
				string iconPath = config?.DailyRewardImgPath;
				if (string.IsNullOrEmpty(iconPath)) iconPath = dailyRewardsContent.Type.GetIconPath();
				Sprite sprite = PrefabLibrary.Current?.ResolveRewardIcon(iconPath);
				if (sprite == null && !string.IsNullOrEmpty(iconPath))
					sprite = Resources.Load<Sprite>(System.IO.Path.ChangeExtension(iconPath, null));
				_rewardImg.sprite = sprite;
				_rewardImg.enabled = sprite != null;
				if (sprite == null && _text != null)
				{
					_text.text = dailyRewardsContent.Type.Key.ToUpperInvariant() + " " + _text.text;
					_text.rectTransform.sizeDelta = new Vector2(160f, 50f);
					_text.rectTransform.anchoredPosition = Vector2.zero;
					_text.enableAutoSizing = true;
					_text.fontSizeMin = 14f;
					_text.fontSizeMax = 24f;
				}
			}
		}
	}
}
