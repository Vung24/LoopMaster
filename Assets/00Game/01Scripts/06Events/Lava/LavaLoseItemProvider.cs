using UnityEngine;

public sealed class LavaLoseItemProvider : ILoseItemProvider
{
    public const string ProviderId = "Lava";
    public string Id => ProviderId;
    public int Priority => 20;
    public bool IsAvailable() => Managers.Lava != null && Managers.Lava.IsUnlocked() && Managers.Lava.IsOngoing() && !Managers.Lava.IsInCd();
    public GameObject GetPrefab(ILoseItemContainer container) => container != null ? container.LoseLavaPrefab : null;
    public void OnItemInstantiated(GameObject instance) { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void AutoRegister()
    {
        LoseItemRegistry.Register(new LavaLoseItemProvider());
    }
}
