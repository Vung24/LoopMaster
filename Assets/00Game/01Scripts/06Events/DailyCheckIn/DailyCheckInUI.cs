using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public static class DailyCheckInUI
	{
		public static GameObject Button(string name, Transform parent, string text, Vector2 size, Vector2 pos, Color color)
		{
			var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
			go.transform.SetParent(parent, false);
			var rect = (RectTransform)go.transform;
			rect.sizeDelta = size;
			rect.anchoredPosition = pos;
			var img = go.GetComponent<Image>();
			img.color = color;

			var textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
			textObj.transform.SetParent(go.transform, false);
			var textRect = (RectTransform)textObj.transform;
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.sizeDelta = Vector2.zero;
			var tmp = textObj.GetComponent<TextMeshProUGUI>();
			tmp.text = text;
			tmp.alignment = TextAlignmentOptions.Center;
			tmp.fontSize = 24;
			tmp.color = Color.white;
			return go;
		}

		public static TMP_Text Text(string name, Transform parent, string text, Vector2 size, Vector2 pos, int fontSize, Color color)
		{
			var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
			go.transform.SetParent(parent, false);
			var rect = (RectTransform)go.transform;
			rect.sizeDelta = size;
			rect.anchoredPosition = pos;
			var tmp = go.GetComponent<TextMeshProUGUI>();
			tmp.text = text;
			tmp.fontSize = fontSize;
			tmp.color = color;
			return tmp;
		}

		public static GameObject Create(Transform parent)
		{
			var dialogPrefab = Resources.Load<GameObject>("DailyRewardsDialog");
			if (dialogPrefab != null)
			{
				return Object.Instantiate(dialogPrefab, parent, false);
			}

			var go = new GameObject("DailyRewardsDialog", typeof(RectTransform), typeof(CanvasGroup), typeof(DailyRewardsDialog));
			go.transform.SetParent(parent, false);
			var rect = (RectTransform)go.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.sizeDelta = Vector2.zero;
			return go;
		}
	}
}

