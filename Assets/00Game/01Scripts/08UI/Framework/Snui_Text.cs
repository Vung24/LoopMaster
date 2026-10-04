using TMPro;
using UnityEngine;

namespace Snui
{
	[DefaultExecutionOrder(-1000)]
	public class Snui_Text : MonoBehaviour
	{
		public int FontType;
		public bool AutoRTL;
		public bool AutoAlignRTL;

		private TMP_Text tMP_Text;

		public TMP_Text TMP_Text
		{
			get
			{
				CacheTextComponent();
				return tMP_Text;
			}
		}

		private void Awake()
		{
			CacheTextComponent();
			EnsureFontAsset();
		}

		private void OnEnable()
		{
			CacheTextComponent();
			EnsureFontAsset();
		}

		private void CacheTextComponent()
		{
			if (tMP_Text == null) tMP_Text = GetComponent<TMP_Text>();
			if (tMP_Text == null) tMP_Text = GetComponentInChildren<TMP_Text>(true);
		}

		private void EnsureFontAsset()
		{
			if (tMP_Text != null && tMP_Text.font == null)
				tMP_Text.font = ResolveFallbackFont(FontType);
		}

		public static void EnsureFontAssets(Transform root)
		{
			if (root == null) return;

			TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
			for (int i = 0; i < labels.Length; i++)
			{
				TMP_Text label = labels[i];
				if (label == null) continue;
				if (label.font == null)
				{
					Snui_Text owner = label.GetComponent<Snui_Text>();
					TMP_FontAsset fallback = ResolveFallbackFont(owner != null ? owner.FontType : 0);
					if (fallback != null) label.font = fallback;
				}
				if (label.font != null && label.fontSharedMaterial == null)
					label.fontSharedMaterial = label.font.material;
				label.enabled = true;
				label.SetAllDirty();
			}
		}

		private static TMP_FontAsset ResolveFallbackFont(int fontType = 0)
		{
			return RecoveredTextFonts.Resolve(fontType);
		}


		public void SetText(string value)
		{
			MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
			for (int i = 0; i < behaviours.Length; i++)
				if (behaviours[i] != null && behaviours[i].GetType().Name == "LocalizeStringEvent")
					behaviours[i].enabled = false;
			if (TMP_Text != null) TMP_Text.text = value ?? string.Empty;
		}

		public void SetFontSize(int fontSize)
		{
			if (TMP_Text != null) TMP_Text.fontSize = fontSize;
		}

		public void SetColor(Color color)
		{
			if (TMP_Text != null) TMP_Text.color = color;
		}
	}
}
