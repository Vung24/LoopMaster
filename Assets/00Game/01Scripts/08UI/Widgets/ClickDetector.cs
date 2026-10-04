using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ClickDetector : MonoBehaviour
{
	public UnityEvent OnClick { get; } = new UnityEvent();

	private void Update()
	{
		// Native ClickDetector.Update invokes once when the first touch ends.
		if (Input.touchCount > 0)
		{
			if (Input.GetTouch(0).phase == TouchPhase.Ended) OnClick.Invoke();
			return;
		}
		// Also support the recovered desktop/editor flow without doubling touch input.
		if (Input.mousePresent && Input.GetMouseButtonUp(0)) OnClick.Invoke();
	}
}
