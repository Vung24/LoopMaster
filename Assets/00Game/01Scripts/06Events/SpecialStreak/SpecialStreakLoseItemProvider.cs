using UnityEngine;

public sealed class SpecialStreakLoseItemProvider : ILoseItemProvider
{
    public const string ProviderId = "SpecialStreak";
    public string Id => ProviderId;
    public int Priority => 50;
    public bool IsAvailable() => SpecialStreakManager.Ensure().ShouldShowLoseItem();
    public GameObject GetPrefab(ILoseItemContainer container) => container != null ? container.LoseSpecialStreakPrefab : null;
    public void OnItemInstantiated(GameObject instance)
    {
        if (instance == null) return;
        var slider = instance.GetComponentInChildren<SpecialStreakMultiplierSlider>(true);
        if (slider != null)
        {
            slider.Type = SpecialStreakMultiplierSliderType.PreLose;
            slider.Init(SpecialStreakManager.Ensure().MultiplierIndex);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void AutoRegister()
    {
        LoseItemRegistry.Register(new SpecialStreakLoseItemProvider());
    }
}
