using UnityEngine;

public sealed class StarsLoseItemProvider : ILoseItemProvider
{
    public const string ProviderId = "Stars";
    public string Id => ProviderId;
    public int Priority => 10;
    public bool IsAvailable() => true;
    public GameObject GetPrefab(ILoseItemContainer container) => container != null ? container.LoseStarsPrefab : null;
    public void OnItemInstantiated(GameObject instance) { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void AutoRegister()
    {
        LoseItemRegistry.Register(new StarsLoseItemProvider());
    }
}
