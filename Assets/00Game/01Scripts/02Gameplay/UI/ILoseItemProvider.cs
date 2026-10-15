using UnityEngine;

public interface ILoseItemContainer
{
    GameObject LoseStarsPrefab { get; }
    GameObject LoseHeartsPrefab { get; }
    GameObject LoseStreakLinePrefab { get; }
    GameObject LoseLavaPrefab { get; }
    GameObject LoseSpecialStreakPrefab { get; }
}

public interface ILoseItemProvider
{
    string Id { get; }
    int Priority { get; }
    bool IsAvailable();
    GameObject GetPrefab(ILoseItemContainer container);
    void OnItemInstantiated(GameObject instance);
}
