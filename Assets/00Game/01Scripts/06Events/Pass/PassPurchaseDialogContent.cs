using Cysharp.Threading.Tasks;
using ShopUI;
using Snui;
using UnityEngine;
using UnityEngine.UI;

public sealed class PassPurchaseDialogContent : DialogContent
{
    private string _route;
    private Button _purchaseBtn;
    private bool _busy;
    public static async UniTask<PassPurchaseDialogContent> Show(Transform parent, string route = "pass")
    {
        if (Managers.Pass == null || !Managers.Pass.CanPurchaseVIP()) return null;
        var dialog = await EventPrefabUI.Show<PassPurchaseDialogContent>(parent, "PassPurchaseDialogContent");
        dialog._route = route; return dialog;
    }
    protected override void Awake()
    {
        _closeBtn = PassUI.Node<Button>(transform, "CloseButton");
        base.Awake();
        _purchaseBtn = PassUI.Bind(transform, "PurchaseBtn", Purchase);
        Snui_Text.EnsureFontAssets(transform);
    }
    protected override void Start()
    {
        base.Start();
        PassUI.Node<Snui.Timer>(transform, "Timer")?.CountdownUntil(Managers.Pass.GetEndTime());
        PassUI.Text(transform, "Snui_Price", "ACTIVATE");
        PassUI.Text(transform, "Name", "You");
        PassUI.Active(transform, "Item1", Managers.Pass.IsHeartEnable());
        PassUI.Active(transform, "Item3", Managers.Pass.IsTeamEnable());
        var special = HierarchyReferenceCache.Descendant(transform, "Special");
        PassUI.Text(special, "Snui_Text", "Offline - no payment");
        PassUI.Animate(transform);
        Refresh();
    }
    private void Refresh() { if (_purchaseBtn != null) _purchaseBtn.interactable = !_busy && Managers.Pass.CanPurchaseVIP(); }
    private void Purchase() => PurchaseAsync().Forget();
    private async UniTask PurchaseAsync()
    {
        if (_busy || !Managers.Pass.CanPurchaseVIP()) return;
        _busy = true; Refresh();
        try
        {
            OfflineShopPurchases.Purchase(Managers.Pass.GetProductId(), _route ?? "pass");
            if (!Managers.Pass.HasActiveVip) return;
            var parent = GetComponentInParent<Canvas>()?.rootCanvas.transform ?? transform.parent;
            Dismiss(1);
            await PassPurchaseSuccDialog.Show(parent);
        }
        finally { _busy = false; if (this != null) Refresh(); }
    }
}
