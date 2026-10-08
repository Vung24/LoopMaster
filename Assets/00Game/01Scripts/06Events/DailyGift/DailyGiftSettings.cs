using UnityEngine;
using UnityEngine.AddressableAssets;

public class DailyGiftSettings : SingletonScriptableObject<DailyGiftSettings>
{
	public AssetReference Icon;
	public AssetReference Dialog;
	public AssetReference DailyGiftDialogAsset => Dialog;
}