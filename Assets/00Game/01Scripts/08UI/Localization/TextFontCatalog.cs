using TMPro;
using UnityEngine;

namespace Snui
{
    public sealed class TextFontCatalog : ScriptableObject
    {
        public TMP_FontAsset DefaultFont;
        public TMP_FontAsset LuckyFont;
        private static TextFontCatalog catalog;

        public static TMP_FontAsset Resolve(int fontType = 0)
        {
            if (catalog == null) catalog = Resources.Load<TextFontCatalog>("TextFontCatalog");
            if (catalog != null)
            {
                TMP_FontAsset font = fontType == 1 ? catalog.LuckyFont : catalog.DefaultFont;
                if (font != null) return font;
                if (catalog.DefaultFont != null) return catalog.DefaultFont;
            }
            if (GameController.Current != null && GameController.Current.FallbackUIFontAsset != null)
                return GameController.Current.FallbackUIFontAsset;
            if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }
    }
}
