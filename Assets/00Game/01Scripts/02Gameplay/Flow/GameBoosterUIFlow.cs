using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

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
        Transform parent = _controller.DialogParent;
        if (parent == null)
        {
            var resultUI = _controller.GetComponent<GameResultUI>();
            parent = resultUI != null ? resultUI.DialogParent : null;
        }
        if (parent == null)
        {
            parent = UICanvas.Current != null ? UICanvas.Current.transform : Snui.Dialog.DefaultParent;
        }

        int version = _version;
        _loading = true;
        try
        {
            var dialog = await BuyToolDialog.Open(key, parent,
                () => version == _version && _controller != null && _controller.isActiveAndEnabled &&
                    _controller.Data == data && _controller.EndState == GameEndState.Playing);
            if (version != _version) { if (dialog != null) dialog.Cancel(); return; }
            _dialog = dialog;
            if (_dialog != null)
            {
                _dialog.Closed += (result) =>
                {
                    _dialog = null;
                    if (version == _version && result == BuyToolDialogContent.RESULT_PURCHASED &&
                        _controller != null && _controller.isActiveAndEnabled &&
                        _controller.EndState == GameEndState.Playing)
                    {
                        use?.Invoke();
                    }
                };
            }
        }
        finally { if (version == _version) _loading = false; }
    }

    public void Cancel()
    {
        _version++;
        _loading = false;
        var dialog = _dialog;
        _dialog = null;
        if (dialog == null) return;
        dialog.Cancel();
    }
}
