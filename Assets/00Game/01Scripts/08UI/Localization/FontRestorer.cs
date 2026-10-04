using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-1100)]
public sealed class FontRestorer : MonoBehaviour
{
	public TMP_Text text;

	private void Awake() => RestoreFont();
	private void OnEnable() => RestoreFont();
	public void UpdateFontAssets(int fontType)
	{
		if (text == null) text = GetComponent<TMP_Text>();
		if (text != null && text.font == null) text.font = Snui.ReferenceDataTextFonts.Resolve(fontType);
	}

	public void UpdateAsset(TMP_FontAsset font)
	{
		if (text == null) text = GetComponent<TMP_Text>();
		if (text != null && font != null) text.font = font;
	}

	private void RestoreFont()
	{
		if (text == null) text = GetComponent<TMP_Text>();
		if (text == null || text.font != null) return;
		Snui.Snui_Text owner = GetComponent<Snui.Snui_Text>();
		TMP_FontAsset fallback = Snui.ReferenceDataTextFonts.Resolve(owner != null ? owner.FontType : 0);
		if (fallback != null) text.font = fallback;
	}
}
