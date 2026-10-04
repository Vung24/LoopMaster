using Snui;
using UnityEngine;

[RequireComponent(typeof(Snui_Button))]
public class BtnClickScaleAnim : MonoBehaviour
{
	private Snui_Button _snuiBtn;
	public float ShrinkScale = 0.95f;
	public float ExpandScale = 1f;

	private void Awake()
	{
		_snuiBtn = GetComponent<Snui_Button>();
		_snuiBtn.OnPointerDownEvent.AddListener(Shrink);
		_snuiBtn.OnPointerUpEvent.AddListener(Expand);
	}

	private void Shrink() => transform.localScale = Vector3.one * Mathf.Max(0f, ShrinkScale);
	private void Expand() => transform.localScale = Vector3.one * Mathf.Max(0f, ExpandScale);

	private void OnDisable() => Expand();

	private void OnDestroy()
	{
		if (_snuiBtn == null) return;
		_snuiBtn.OnPointerDownEvent.RemoveListener(Shrink);
		_snuiBtn.OnPointerUpEvent.RemoveListener(Expand);
	}
}
