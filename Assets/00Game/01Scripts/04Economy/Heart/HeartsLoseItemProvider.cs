using UnityEngine;

public sealed class HeartsLoseItemProvider : ILoseItemProvider
{
    public const string ProviderId = "Hearts";
    public string Id => ProviderId;
    public int Priority => 40;
    public bool IsAvailable() => Managers.Heart != null && !Managers.Heart.IsInfinite();
    public GameObject GetPrefab(ILoseItemContainer container) => container != null ? container.LoseHeartsPrefab : null;
    public void OnItemInstantiated(GameObject instance) { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void AutoRegister()
    {
        LoseItemRegistry.Register(new HeartsLoseItemProvider());
    }
}
