using System.Collections.Generic;

public static class BoosterTutorialRegistry
{
    private static readonly Dictionary<string, IBoosterTutorialStep> Steps =
        new Dictionary<string, IBoosterTutorialStep>
        {
            { InventoryKeys.TOOL_UNDO, new UndoTutorialStep() },
            { InventoryKeys.TOOL_SHUFFLE, new ShuffleTutorialStep() },
            { InventoryKeys.TOOL_PATH, new PathTutorialStep() }
        };
    private static readonly IBoosterTutorialStep Default = new DefaultBoosterTutorialStep();

    public static IBoosterTutorialStep GetStep(string key)
    {
        if (Steps.TryGetValue(key, out IBoosterTutorialStep step)) return step;
        return Default;
    }

    public static bool HasInteractiveGuide(string key)
    {
        return !string.IsNullOrEmpty(key) && Steps.ContainsKey(key);
    }
}
