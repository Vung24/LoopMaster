#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using A = LoopLevelAuthoring;

/// <summary>Explains structural difficulty; it does not prove solvability or player difficulty.</summary>
public sealed class LoopLevelDifficultyAnalysis
{
    public float Score { get; private set; }
    public int Colors { get; private set; }
    public int EmptyBoxes { get; private set; }
    public int FreeSlots { get; private set; }
    public int MixedBoxes { get; private set; }
    public int BuriedSplitColors { get; private set; }
    public int MutualDependencies { get; private set; }
    public int HiddenCubes { get; private set; }
    public int Mechanisms { get; private set; }
    public int Capacity { get; private set; }
    public float MeanBurial { get; private set; }

    public int Band
    {
        get
        {
            // Rounded structural-score quartiles of the 500-level reference snapshot.
            if (Score < 58) return 0;
            if (Score < 62) return 1;
            if (Score < 66) return 2;
            return 3;
        }
    }

    /// <summary>Measures color burial, fragmentation, mutual blocking, mechanics and buffer pressure.</summary>
    public static LoopLevelDifficultyAnalysis Evaluate(JObject root)
    {
        var result = new LoopLevelDifficultyAnalysis();
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");
        var boxesPerColor = new Dictionary<int, HashSet<int>>();
        var bottomBoxes = new Dictionary<int, HashSet<int>>();
        var shallowest = new Dictionary<int, int>();
        var dependencies = new HashSet<long>();
        int cubesTotal = 0, transitions = 0, adjacentSlots = 0, occupied = 0;
        foreach (JObject box in boxes)
        {
            int boxIndex = boxes.IndexOf(box);
            JArray cubes = A.Array(box, "Cubes");
            if (A.Int(box, "BoxType") == 0)
            {
                result.FreeSlots += Math.Max(0, 4 - cubes.Count);
                if (cubes.Count == 0) result.EmptyBoxes++;
            }
            if (cubes.Count > 0) occupied++;
            adjacentSlots += Math.Max(0, cubes.Count - 1);
            var distinct = new HashSet<int>();
            for (int index = 0; index < cubes.Count; index++)
            {
                JObject cube = (JObject)cubes[index];
                int color = A.Int(cube, "Type");
                if (color <= 0) continue;
                cubesTotal++;
                distinct.Add(color);
                AddBox(boxesPerColor, color, boxIndex);
                if (index < 2 && index < cubes.Count - 1) AddBox(bottomBoxes, color, boxIndex);
                int depth = cubes.Count - index - 1;
                if (!shallowest.TryGetValue(color, out int previous) || depth < previous) shallowest[color] = depth;
                if (index > 0 && color != A.Int((JObject)cubes[index - 1], "Type")) transitions++;
                if (A.Int(cube, "IsSecret") > 0) result.HiddenCubes++;
                if (A.Int(cube, "GroupKeyId") > 0 || A.Int(cube, "NumLock") > 0 ||
                    A.Int(cube, "LinkedPairId") > 0 || A.Int(cube, "IsFixed") > 0) result.Mechanisms++;
                for (int above = index + 1; above < cubes.Count; above++)
                {
                    int blocking = A.Int((JObject)cubes[above], "Type");
                    if (blocking > 0 && blocking != color) dependencies.Add(((long)color << 32) | (uint)blocking);
                }
            }
            if (distinct.Count > 1) result.MixedBoxes++;
            if (A.Int(box, "BoxType") != 0 || A.Int(box, "LockColor") > 0 || A.Int(box, "NumLock") > 0 ||
                A.Int(box, "LimitedColor") > 0 || A.Int(box, "IsEnterOnly") > 0 ||
                A.Int(box, "IsAdjacentLocked") > 0 || A.Array(box, "MultiColorLock").Count > 0) result.Mechanisms++;
        }
        result.Mechanisms += A.Array(layout, "Groups").Count + A.Array(layout, "Covers").Count;
        result.Capacity = A.Int(layout, "PathLength");
        result.Colors = boxesPerColor.Count;
        int spread = 0, burial = 0;
        foreach (KeyValuePair<int, HashSet<int>> color in boxesPerColor)
        {
            spread += color.Value.Count - 1;
            burial += shallowest[color.Key];
            if (shallowest[color.Key] > 0 && bottomBoxes.TryGetValue(color.Key, out HashSet<int> bottoms) && bottoms.Count >= 2)
                result.BuriedSplitColors++;
        }
        foreach (long dependency in dependencies)
        {
            int lower = (int)(dependency >> 32), upper = (int)dependency;
            long reverse = ((long)upper << 32) | (uint)lower;
            if (lower < upper && dependencies.Contains(reverse)) result.MutualDependencies++;
        }
        result.MeanBurial = (float)burial / Math.Max(1, result.Colors);
        float mixing = (float)transitions / Math.Max(1, adjacentSlots);
        float fragmentation = (float)spread / Math.Max(1, cubesTotal - result.Colors);
        float buried = Math.Min(1f, result.MeanBurial / 2f);
        float split = (float)result.BuriedSplitColors / Math.Max(1, result.Colors);
        float cycles = Math.Min(1f, (float)result.MutualDependencies / Math.Max(1, result.Colors));
        float mechanics = Math.Min(1f, (float)(result.HiddenCubes + result.Mechanisms * 2) / Math.Max(1, cubesTotal));
        float roadPressure = Math.Min(1f, (float)occupied / Math.Max(1, result.Capacity * 2));
        result.Score = Math.Max(0f, Math.Min(100f, 20 * mixing + 20 * fragmentation + 20 * buried +
            10 * split + 10 * cycles + 15 * mechanics + 5 * roadPressure - result.FreeSlots));
        return result;
    }

    public string Describe()
    {
        return "Điểm cấu trúc: " + Score.ToString("F1") + "/100 · " + Colors + " màu · " + MixedBoxes + " hộp trộn\n" +
            "Độ chôn màu TB: " + MeanBurial.ToString("F2") + " lớp · " + BuriedSplitColors + " màu bị chôn ở nhiều đáy hộp · " +
            MutualDependencies + " cặp phụ thuộc chéo\n" + HiddenCubes + " Hidden · " + Mechanisms + " ràng buộc cơ chế · Sức chứa đường: " +
            Capacity + "\nHộp trống: " + EmptyBoxes + " · Ô trống hộp thường: " + FreeSlots;
    }

    private static void AddBox(Dictionary<int, HashSet<int>> lookup, int color, int box)
    {
        if (!lookup.TryGetValue(color, out HashSet<int> indices))
        {
            indices = new HashSet<int>();
            lookup.Add(color, indices);
        }
        indices.Add(box);
    }
}
#endif
