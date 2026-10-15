using UnityEngine;

public sealed class StreakLineLoseItemProvider : ILoseItemProvider
{
    public const string ProviderId = "StreakLine";
    public string Id => ProviderId;
    public int Priority => 30;
    public bool IsAvailable()
    {
        var streak = Managers.StreakLine;
        if (streak == null || !streak.IsOngoing) return false;
        int checkpoint = 0;
        var chests = streak.GetChests();
        if (chests != null)
        {
            foreach (var chest in chests)
            {
                if (chest != null && chest.Level <= streak.GetCurrLevel())
                    checkpoint = Mathf.Max(checkpoint, chest.Level);
            }
        }
        return streak.GetCurrLevel() > checkpoint;
    }
    public GameObject GetPrefab(ILoseItemContainer container) => container != null ? container.LoseStreakLinePrefab : null;
    public void OnItemInstantiated(GameObject instance) { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void AutoRegister()
    {
        LoseItemRegistry.Register(new StreakLineLoseItemProvider());
    }
}
