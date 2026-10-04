using UnityEngine;

/// <summary>Interactive coin HUD entry point referenced by UICanvas prefabs.</summary>
public sealed class CoinHud : Snui_CoinHud
{
	protected override void Awake()
	{
		base.Awake();
		if (_button != null)
		{
			_button.onClick.RemoveListener(OnClicked);
			_button.onClick.AddListener(OnClicked);
		}
	}

	private void OnClicked() { if (CanClick()) PlayShakeAnimation(); }
	public override void PlayAddNumberAnimation(int num) => base.PlayAddNumberAnimation(num);
	public bool CanClick() => _button != null && _button.IsInteractable();

	protected override void OnDestroy()
	{
		if (_button != null) _button.onClick.RemoveListener(OnClicked);
		base.OnDestroy();
	}
}
