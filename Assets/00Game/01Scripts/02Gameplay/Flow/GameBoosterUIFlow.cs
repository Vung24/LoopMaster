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

    public GameBoosterUIFlow(GameController controller)
    {
        _controller = controller;
    }

    public void Request(string key, bool canRequest, Func<bool> use)
    {
        RequestAsync(key, canRequest, use).Forget();
    }

    private async UniTask RequestAsync(string key, bool canRequest, Func<bool> use)
    {
        if (!canRequest || IsOpen || _controller == null) return;
        if (InventoryManager.Ensure().CanUseBooster(key))
        {
            use?.Invoke();
            return;
        }

        GameData data = _controller.Data;
        Transform parent = ResolveDialogParent();

        int version = _version;
        _loading = true;
        try
        {
            bool IsSessionValidPredicate() => IsSessionValid(version, data);
            var dialog = await BuyToolDialog.Open(key, parent, IsSessionValidPredicate);
            if (version != _version)
            {
                if (dialog != null) dialog.Cancel();
                return;
            }
            _dialog = dialog;
            if (_dialog != null)
            {
                _dialog.Closed += result => HandleDialogClosed(result, version, data, use);
            }
        }
        finally
        {
            if (version == _version) _loading = false;
        }
    }

    private bool IsSessionValid(int version, GameData expectedData)
    {
        if (version != _version) return false;
        if (_controller == null || !_controller.isActiveAndEnabled) return false;
        if (_controller.Data != expectedData) return false;
        return _controller.EndState == GameEndState.Playing;
    }

    private void HandleDialogClosed(int result, int version, GameData expectedData, Func<bool> use)
    {
        _dialog = null;
        if (result != BuyToolDialogContent.RESULT_PURCHASED) return;
        if (!IsSessionValid(version, expectedData)) return;
        use?.Invoke();
    }

    private Transform ResolveDialogParent()
    {
        if (_controller.DialogParent != null) return _controller.DialogParent;
        GameResultUI resultUI = _controller.GetComponent<GameResultUI>();
        if (resultUI != null && resultUI.DialogParent != null) return resultUI.DialogParent;
        if (UICanvas.Current != null) return UICanvas.Current.transform;
        return Snui.Dialog.DefaultParent;
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
