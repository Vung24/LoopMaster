using UnityEngine;
using UnityEngine.AddressableAssets;

public class ShopUISettings_SO : SingletonScriptableObject<ShopUISettings_SO>
{
	public AssetReference ShopDialogContentPrefab;
	public AssetReference PurchaseFailDialogContent;
}