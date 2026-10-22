#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using A = LoopLevelAuthoring;

/// <summary>Identifies teaching and recovery beats in the authored campaign.</summary>
public static class CampaignLearningCurve
{
    /// <summary>Returns whether this level introduces a mechanic on a small board.</summary>
    public static bool IsIntroduction(int level)
    {
        return level == 6 || level == 15 || level == 21 || level == 23 || level == 29 ||
            level == 37 || level == 41 || level == 49 || level == 53 || level == 56;
    }

    /// <summary>Returns whether this level follows an authored Hard or Expert level.</summary>
    public static bool IsRecovery(int level)
    {
        return level == 11 || level == 21 || level == 26 || level == 31 || level == 36 ||
            level == 41 || level == 46 || level == 56 || level == 61 || level == 63 ||
            level == 65 || level == 67 || level == 69;
    }

    /// <summary>Returns whether the reference solution should stay short and forgiving.</summary>
    public static bool IsGentle(int level)
    {
        if (IsIntroduction(level) || IsRecovery(level)) return true;
        return (level >= 3 && level <= 9) || (level >= 16 && level <= 19) || level == 22 || level == 24 || level == 38 ||
            level == 42 || level == 50 || level == 54 || level == 57;
    }

    /// <summary>Packs the easy board's stock while retaining one actual empty normal box.</summary>
    public static void ArrangeSingleBuffer(JArray boxes)
    {
        var normalBoxes = new List<JObject>();
        var stock = new List<JToken>();
        foreach (JObject box in boxes)
        {
            if (A.Int(box, "BoxType") != 0) continue;
            normalBoxes.Add(box);
            JArray cubes = A.Array(box, "Cubes");
            foreach (JToken cube in cubes) stock.Add(cube);
            cubes.Clear();
        }
        int occupied = normalBoxes.Count - 1;
        if (occupied < 1 || stock.Count > occupied * 4 || stock.Count < occupied)
            throw new FormatException("Easy level stock must fit all but one normal box.");
        int next = 0;
        for (int index = 0; index < occupied; index++)
        {
            int remainingBoxes = occupied - index - 1;
            int count = Math.Min(4, stock.Count - next - remainingBoxes);
            JArray cubes = A.Array(normalBoxes[index], "Cubes");
            for (int cube = 0; cube < count; cube++) cubes.Add(stock[next++]);
        }
    }
}
#endif
