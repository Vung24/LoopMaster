using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RetryDialogContent : DialogContent
{
    public const int RESULT_RESTART = 1;

    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Button _btnRetry;
    [SerializeField] private Button _btnClose;
    [SerializeField] private Transform _heart;
    [SerializeField] private Transform _unlimitedHeart;
    private GameController _controller;

    protected override void Awake()
    {
        base.Awake();
        Bind(_controller != null ? _controller : GameController.Current);
    }

    public void Bind(GameController controller)
    {
        _controller = controller;
        BindButton(_btnRetry, OnClickRetry);
        BindButton(_btnClose, OnClickCloseButton);
        if (_heart != null) _heart.gameObject.SetActive(false);
        if (_unlimitedHeart != null) _unlimitedHeart.gameObject.SetActive(false);
        if (_titleText != null)
            _titleText.text = controller != null ? "LEVEL " + controller.CurrentLevelNumber : "RETRY LEVEL";
    }

    public override void Show()
    {
        base.Show();
        Snui_Text.EnsureFontAssets(transform);
        Bind(_controller != null ? _controller : GameController.Current);
    }

    public void OnClickRetry()
    {
        if (_controller != null && _controller.TryReplayLevel()) Dismiss(RESULT_RESTART);
    }

    protected override void OnClickCloseButton() { Dismiss(RESULT_CLOSE); }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null) return;
        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }
}
