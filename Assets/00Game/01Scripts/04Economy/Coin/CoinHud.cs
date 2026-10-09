using UnityEngine;

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

	private void OnClicked()
	{
		if (!CanClick()) return;
		var home = HomeController.ForScene(gameObject.scene);
		if (home != null) home.JumpToPage(HomeController.SHOP_PAGE_INDEX);
		else PlayShakeAnimation();
	}
	public override void PlayAddNumberAnimation(int num) => base.PlayAddNumberAnimation(num);
	public bool CanClick() => _button != null && _button.IsInteractable();

	protected override void OnDestroy()
	{
		if (_button != null) _button.onClick.RemoveListener(OnClicked);
		base.OnDestroy();
	}
}
