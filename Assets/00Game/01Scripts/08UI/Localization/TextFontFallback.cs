using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-1100)]
public sealed class TextFontFallback : MonoBehaviour
{
	public TMP_Text text;

	private void Awake() => EnsureFont();
	private void OnEnable() => EnsureFont();
	public void UpdateFontAssets(int fontType)
	{
		if (text == null) text = GetComponent<TMP_Text>();
		if (text != null && text.font == null) text.font = Snui.TextFontCatalog.Resolve(fontType);
	}

	public void UpdateAsset(TMP_FontAsset font)
	{
		if (text == null) text = GetComponent<TMP_Text>();
		if (text != null && font != null) text.font = font;
	}

	private void EnsureFont()
	{
		if (text == null) text = GetComponent<TMP_Text>();
		if (text == null || text.font != null) return;
		Snui.Snui_Text owner = GetComponent<Snui.Snui_Text>();
		TMP_FontAsset fallback = Snui.TextFontCatalog.Resolve(owner != null ? owner.FontType : 0);
		if (fallback != null) text.font = fallback;
	}
}
