using System;
using TMPro;
using UnityEngine;

namespace Snui
{
	[Obsolete("Use TMPSetRealtime instead.")]
	public class OutlineSet : MonoBehaviour
	{
		[ColorUsage(true, true)]
		public Color outlineColor;

		[Range(0f, 1f)]
		public float OutlineWidth;

		[Range(-1f, 1f)]
		public float FaceDilate;

		private void OnEnable()
		{
			var tmp = GetComponent<TMP_Text>();
			if (tmp == null) return;

			var mat = tmp.fontMaterial;
			if (mat == null) return;

			mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
			if (OutlineWidth > 0f)
			{
				mat.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
			}

			if (FaceDilate != 0f)
			{
				mat.SetFloat(ShaderUtilities.ID_FaceDilate, FaceDilate);
			}
		}
	}
}