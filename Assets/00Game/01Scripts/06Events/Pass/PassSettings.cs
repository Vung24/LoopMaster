using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "PassSettings", menuName = "Snui/Events/PassSettings")]
public class PassSettings : ScriptableObject
{
    public AssetReference PassInfoDialog, PassPurchaseDialogContent, PassPurchaseSuccDialog, PassFinalBundleRewardDialog;
    public AssetReferenceSprite[] BoxSprites;
    public Sprite[] ResidentBoxSprites;
    public Sprite GetResidentBoxSprite(int stage, bool isVip) => ResidentBoxSprites == null || ResidentBoxSprites.Length == 0 ? null : ResidentBoxSprites[Mathf.Clamp(stage / 10 + (isVip ? 1 : 0), 0, ResidentBoxSprites.Length - 1)];
    public AssetReference DefaultDetailDialog, DefaultOpenDialog, DefaultEndDialog, DefaultIcon;
    public AssetReference DefaultFailBanner, DefaultNoHeartBanner, DefaultBanner, DefaultShopItem;
    public AssetReference ChristmasDetailDialog, ChristmasOpenDialog, ChristmasEndDialog, ChristmasIcon;
    public AssetReference ChristmasFailBanner, ChristmasNoHeartBanner, ChristmasBanner, ChristmasShopItem;

    public AssetReferenceSprite GetRewardBoxSprite(int stage, bool isVip)
    {
        if (BoxSprites == null || BoxSprites.Length == 0) return null;
        return BoxSprites[Mathf.Clamp(stage / 10 + (isVip ? 1 : 0), 0, BoxSprites.Length - 1)] ?? BoxSprites[0];
    }
}
