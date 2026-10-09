using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(TMP_Text))]
[ExecuteAlways]
public sealed class TMPSetRealtime : MonoBehaviour
{
	[ColorUsage(true, true), Header("Outline"), FormerlySerializedAs("outlineColor")]
	public Color OutlineColor = Color.black;
	[Range(0f, 1f)] public float OutlineWidth;
	[Range(-1f, 1f)] public float FaceDilate;
	[Header("Underlay")] public bool EnableUnderlay;
	[ColorUsage(true, true), FormerlySerializedAs("underlayColor")] public Color UnderlayColor = Color.black;
	[FormerlySerializedAs("underlayOffsetX"), Range(-1f, 1f)] public float UnderlayOffsetX;
	[FormerlySerializedAs("underlayOffsetY"), Range(-1f, 1f)] public float UnderlayOffsetY;
	[FormerlySerializedAs("underlaySoftness"), Range(0f, 1f)] public float UnderlaySoftness;
	[FormerlySerializedAs("underlayDilate"), Range(-1f, 1f)] public float UnderlayDilate;
	[Header("Glow")] public bool EnableGlow;
	[ColorUsage(true, true)] public Color GlowColor = Color.cyan;
	[Range(0f, 1f)] public float GlowOffset;
	[Range(0f, 1f)] public float GlowInner = 0.05f;
	[Range(0f, 1f)] public float GlowOuter = 0.05f;
	[Range(0f, 1f)] public float GlowPower = 0.75f;

	private TMP_Text _textComponent;

	private void OnEnable() => ApplyFontSettings();
	private void OnValidate() { if (isActiveAndEnabled) ApplyFontSettings(); }

	public void ApplyFontSettings()
	{
		if (_textComponent == null) _textComponent = GetComponent<TMP_Text>();
		if (_textComponent == null || _textComponent.fontSharedMaterial == null) return;
		UpdateMaterial(_textComponent.fontMaterial);
		TMP_SubMeshUI[] subMeshes = GetComponentsInChildren<TMP_SubMeshUI>(true);
		for (int i = 0; i < subMeshes.Length; i++)
			if (subMeshes[i] != null && subMeshes[i].sharedMaterial != null) UpdateMaterial(subMeshes[i].material);
		_textComponent.UpdateMeshPadding();
		_textComponent.SetVerticesDirty();
		_textComponent.SetMaterialDirty();
	}

	private void UpdateMaterial(Material material)
	{
		if (material == null) return;
		SetColor(material, "_OutlineColor", OutlineColor);
		SetFloat(material, "_OutlineWidth", OutlineWidth);
		SetFloat(material, "_FaceDilate", FaceDilate);
		SetColor(material, "_UnderlayColor", UnderlayColor);
		SetFloat(material, "_UnderlayOffsetX", UnderlayOffsetX);
		SetFloat(material, "_UnderlayOffsetY", UnderlayOffsetY);
		SetFloat(material, "_UnderlaySoftness", UnderlaySoftness);
		SetFloat(material, "_UnderlayDilate", UnderlayDilate);
		SetColor(material, "_GlowColor", GlowColor);
		SetFloat(material, "_GlowOffset", GlowOffset);
		SetFloat(material, "_GlowInner", GlowInner);
		SetFloat(material, "_GlowOuter", GlowOuter);
		SetFloat(material, "_GlowPower", GlowPower);
		SetKeyword(material, "UNDERLAY_ON", EnableUnderlay);
		SetKeyword(material, "GLOW_ON", EnableGlow);
	}

	private static void SetFloat(Material m, string property, float value) { if (m.HasProperty(property)) m.SetFloat(property, value); }
	private static void SetColor(Material m, string property, Color value) { if (m.HasProperty(property)) m.SetColor(property, value); }
	private static void SetKeyword(Material m, string keyword, bool enabled) { if (enabled) m.EnableKeyword(keyword); else m.DisableKeyword(keyword); }
}
