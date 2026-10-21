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
			_extensionTween = AnimateExtension(_capacityText, transform)
				.OnComplete(() => _extensionTween = null);
		}
	}

	private void ResetExtensionAnimation()
	{
		if (_extensionTween == null) return;
		_extensionTween.Kill();
		_extensionTween = null;
	}

	internal static Tween AnimateExtension(TMP_Text label, Transform container = null)
	{
		if (label == null) return null;
		Vector3 textScale = label.transform.localScale;
		Color color = label.color;
		label.color = new Color(0.45f, 1f, 0.55f, color.a);

		Sequence seq = DOTween.Sequence();
		seq.Append(label.transform.DOScale(textScale * 1.5f, 0.2f).SetEase(Ease.OutQuad))
			.Append(label.transform.DOScale(textScale * 0.96f, 0.13f).SetEase(Ease.OutQuad))
			.Append(label.transform.DOScale(textScale * 1.02f, 0.13f).SetEase(Ease.OutQuad))
			.Append(label.transform.DOScale(textScale, 0.13f).SetEase(Ease.OutQuad))
			.Join(label.DOColor(color, 0.3f));

		if (container != null)
		{
			Vector3 containerScale = container.localScale;
			Sequence containerSeq = DOTween.Sequence()
				.Append(container.DOScale(containerScale * 0.88f, 0.10f))
				.Append(container.DOScale(containerScale * 1.08f, 0.27f))
				.Append(container.DOScale(containerScale * 0.96f, 0.13f))
				.Append(container.DOScale(containerScale * 1.02f, 0.13f))
				.Append(container.DOScale(containerScale, 0.10f))
				.OnKill(() =>
				{
					if (container != null) container.localScale = containerScale;
				});
			seq.Join(containerSeq);
		}

		seq.OnKill(() =>
		{
			if (label == null) return;
			label.transform.localScale = textScale;
			label.color = color;
		});

		return seq;
	}

	private void OnDisable() => ResetExtensionAnimation();
}
