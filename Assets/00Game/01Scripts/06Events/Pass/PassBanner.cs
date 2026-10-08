using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;

public class PassBanner : MonoBehaviour
{
    private void Awake()
    {
        var button = GetComponentInChildren<UnityEngine.UI.Button>(true);
        if (button != null) button.onClick.AddListener(() => PassPurchaseDialogContent.Show(
            GetComponentInParent<Canvas>()?.rootCanvas.transform, "pass_banner").Forget());
    }
    private void OnEnable()
    {
        var button = GetComponentInChildren<UnityEngine.UI.Button>(true);
        if (button != null) button.interactable = Managers.Pass != null && Managers.Pass.CanPurchaseVIP();
    }
}
