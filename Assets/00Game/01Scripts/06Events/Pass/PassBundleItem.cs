using Cysharp.Threading.Tasks;
using ShopUI;
using Snui;
using UnityEngine;

public class PassBundleItem : ShopItemBase
{
    public override void Init(string type, IShopDelegate shopDelegate)
    {
        base.Init(type, shopDelegate);
        if (_buyBtn != null) _buyBtn.interactable = Managers.Pass != null && Managers.Pass.CanPurchaseVIP();
        if (_price != null) _price.GetComponent<TMPro.TMP_Text>().text = "ACTIVATE";
    }
    protected override void OnClickBuyBtn() => PassPurchaseDialogContent.Show(
        GetComponentInParent<Canvas>()?.rootCanvas.transform, "shop_pass").Forget();
}
