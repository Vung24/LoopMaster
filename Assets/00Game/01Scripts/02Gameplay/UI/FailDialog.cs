using UnityEngine;

public class FailDialog : MonoBehaviour
{
	private GameController _controller;
	private CanvasGroup _canvasGroup;

	private void Awake()
	{
		_canvasGroup = GetComponent<CanvasGroup>();
	}

	public void Bind(GameController controller)
	{
		_controller = controller;
		if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
		PreFailDialogContent preFail = GetComponentInChildren<PreFailDialogContent>(true);
		if (preFail != null) preFail.Bind(controller);
		foreach (FailDialogContent content in GetComponentsInChildren<FailDialogContent>(true))
			if (!(content is PreFailDialogContent)) content.Bind(controller);
	}

	public void Show()
	{
		Snui.Snui_Text.EnsureFontAssets(transform);
		gameObject.SetActive(true);
		if (_canvasGroup != null)
		{
			_canvasGroup.alpha = 1f;
			_canvasGroup.interactable = true;
			_canvasGroup.blocksRaycasts = true;
		}
	}

	public void Hide()
	{
		if (_canvasGroup != null)
		{
			_canvasGroup.interactable = false;
			_canvasGroup.blocksRaycasts = false;
		}
		gameObject.SetActive(false);
	}

	public void OnClickReplayLevel() { if (_controller != null) _controller.TryReplayLevel(); }
	public void OnClickRetryLevel() { OnClickReplayLevel(); }
}
