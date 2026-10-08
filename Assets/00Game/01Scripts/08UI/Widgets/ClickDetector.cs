using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ClickDetector : MonoBehaviour
{
	public UnityEvent OnClick { get; } = new UnityEvent();

	private void Update()
	{
		if (Input.touchCount > 0)
		{
			if (Input.GetTouch(0).phase == TouchPhase.Ended) OnClick.Invoke();
			return;
		}
		if (Input.mousePresent && Input.GetMouseButtonUp(0)) OnClick.Invoke();
	}
}
