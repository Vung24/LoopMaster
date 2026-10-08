using Cysharp.Threading.Tasks;
using System;

// Keeps purchase UI separate from BoosterFlow's board operations.
public sealed class GameBoosterUIFlow
{
    private readonly GameController _controller;
    private BuyToolDialog _dialog;
    private bool _loading;
    private int _version;
    public bool IsOpen => _loading || _dialog != null;

    public GameBoosterUIFlow(GameController controller) => _controller = controller;

    public void Request(string key, bool canRequest, Func<bool> use)
    {
        RequestAsync(key, canRequest, use).Forget();
    }

    private async UniTask RequestAsync(string key, bool canRequest, Func<bool> use)
    {
        if (!canRequest || IsOpen || _controller == null) return;
        if (InventoryManager.Ensure().GetAmount(key) > 0) { use(); return; }
        GameData data = _controller.Data;
        var resultUI = _controller.GetComponent<GameResultUI>();
        int version = _version;
        _loading = true;
        try
        {
            var dialog = await BuyToolDialog.Open(key, resultUI == null ? null : resultUI.DialogParent,
                () => version == _version && _controller != null && _controller.isActiveAndEnabled && _controller.Data == data &&
                    _controller.EndState == GameEndState.Playing && _controller.CanReplayLevel);
            if (version != _version) { if (dialog != null) dialog.Cancel(); return; }
            _dialog = dialog;
            if (_dialog != null) _dialog.Closed += OnClosed;
        }
        finally { if (version == _version) _loading = false; }
        // Native PrepareUsedTool returns false after opening the shop.
        // Buying replenishes inventory; the next tap performs the board operation.
    }

    private void OnClosed(int result)
    {
        if (_dialog != null) _dialog.Closed -= OnClosed;
        _dialog = null;
    }

    public void Cancel()
    {
        _version++;
        _loading = false;
        var dialog = _dialog;
        _dialog = null;
        if (dialog == null) return;
        dialog.Closed -= OnClosed;
        dialog.Cancel();
    }
}
