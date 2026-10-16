using TMPro;
using UnityEngine;
using DG.Tweening;

public class Capacity : MonoBehaviour
{
	[SerializeField] private Sprite _greenLight;
	[SerializeField] private Sprite _redLight;
	[SerializeField] private TMP_Text _capacityText;
	[SerializeField] private SpriteRenderer _light;
	private int _displayedProgress = -1;
	private int _displayedMaximum = -1;
	private Tween _extensionTween;

	public void UpdateCapacity(int progress, int max)
	{
		int safeMax = Mathf.Max(0, max);
		int safeProgress = Mathf.Clamp(progress, 0, safeMax);
		if (safeProgress == _displayedProgress && safeMax == _displayedMaximum) return;
		bool extended = _displayedMaximum >= 0 && safeMax > _displayedMaximum;
		_displayedProgress = safeProgress;
		_displayedMaximum = safeMax;
		if (_capacityText != null)
			_capacityText.text = safeProgress + "/" + safeMax;
		if (_light != null) _light.sprite = safeProgress + 1 < safeMax ? _greenLight : _redLight;
		if (extended && _capacityText != null && isActiveAndEnabled)
		{
			ResetExtensionAnimation();
			_extensionTween = AnimateExtension(_capacityText)
				.OnComplete(() => _extensionTween = null);
		}
	}

	private void ResetExtensionAnimation()
	{
		if (_extensionTween == null) return;
		_extensionTween.Kill();
		_extensionTween = null;
	}

	internal static Tween AnimateExtension(TMP_Text label)
	{
		Vector3 scale = label.transform.localScale;
		Color color = label.color;
		label.color = new Color(0.45f, 1f, 0.55f, color.a);
		return DOTween.Sequence()
			.Append(label.transform.DOScale(scale * 1.3f, 0.18f).SetEase(Ease.OutBack))
			.Append(label.transform.DOScale(scale, 0.3f).SetEase(Ease.OutQuad))
			.Join(label.DOColor(color, 0.3f))
			.OnKill(() =>
			{
				if (label == null) return;
				label.transform.localScale = scale;
				label.color = color;
			});
	}

	private void OnDisable() => ResetExtensionAnimation();
}
