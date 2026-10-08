using Snui;
using UnityEngine;
using UnityEngine.UI;

public enum PauseConfirmType { Replay, Quit }

public class PauseConfirmDialogContent : DialogContent
{
	public const int RESULT_CONFIRM = 1;
	[SerializeField] private Button _confirmButton;
	[SerializeField] private Button _cancelButton;
	[SerializeField] private Button _closeButton;
	[SerializeField] private LoseContainer _loseContainer;
	protected override void Awake()
	{
		base.Awake();
		DialogUI.Bind(_confirmButton, OnClickConfirmButton);
		DialogUI.Bind(_cancelButton, OnClickCloseButton);
		DialogUI.Bind(_closeButton, OnClickCloseButton);
	}
	public void OnClickConfirmButton() => Dismiss(RESULT_CONFIRM);
	public override void Show()
	{
		base.Show();
		Snui_Text.EnsureFontAssets(transform);
		if (_loseContainer != null) _loseContainer.Show();
	}
}
