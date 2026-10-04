using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggle used by the pause prefab; swaps its authored On and Off graphics.
/// </summary>
public sealed class PauseToggle : Toggle
{
	[SerializeField] private GameObject _onObject;
	[SerializeField] private GameObject _offObject;

	protected override void OnEnable()
	{
		base.OnEnable();
		onValueChanged.AddListener(OnToggleChanged);
		RefreshVisual();
	}

	protected override void OnDisable()
	{
		onValueChanged.RemoveListener(OnToggleChanged);
		base.OnDisable();
	}

	public void RefreshVisual()
	{
		if (_onObject != null) _onObject.SetActive(isOn);
		if (_offObject != null) _offObject.SetActive(!isOn);
	}

	private void OnToggleChanged(bool value)
	{
		RefreshVisual();
	}

}
