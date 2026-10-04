using TMPro;
using UnityEngine;

public class Capacity : MonoBehaviour
{
	[SerializeField] private Sprite _greenLight;
	[SerializeField] private Sprite _redLight;
	[SerializeField] private TMP_Text _capacityText;
	[SerializeField] private SpriteRenderer _light;

	public void UpdateCapacity(int progress, int max)
	{
		int safeMax = Mathf.Max(0, max);
		int safeProgress = Mathf.Clamp(progress, 0, safeMax);
		if (_capacityText != null)
			_capacityText.text = safeProgress + "/" + safeMax;
		if (_light != null) _light.sprite = safeProgress + 1 < safeMax ? _greenLight : _redLight;
	}
}
