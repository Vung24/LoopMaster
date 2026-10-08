using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;

public class PassInfoDialog : EventInfoDialogFourRow
{
    public static UniTask<PassInfoDialog> Show(Transform parent) => EventPrefabUI.Show<PassInfoDialog>(parent, "PassInfoDialog");
    protected override void Awake()
    {
        base.Awake();
        PassUI.Bind(transform, "ContinueButton", OnClickClose);
        PassUI.Bind(transform, "ContinueBtn", OnClickClose);
        Snui_Text.EnsureFontAssets(transform);
    }
}
