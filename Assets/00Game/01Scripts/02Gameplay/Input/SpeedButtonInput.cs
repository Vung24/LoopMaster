using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class SpeedButtonInput : MonoBehaviour
{
    private GameController _controller;
    private Button _button;

    public void Bind(GameController controller)
    {
        _controller = controller;
        if (_button != null) _button.onClick.RemoveListener(Toggle);
        _button = GetComponent<Button>();
        if (_button != null) _button.onClick.AddListener(Toggle);
    }

    private void Toggle()
    {
        if (_controller == null) _controller = GameController.Current;
        if (_button == null || !_button.IsInteractable() || _controller == null || !_controller.IsGameplayActive) return;
        _controller.SetSpeedUp(!_controller.IsSpeedUpActive);
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(Toggle);
    }
}