#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using A = LoopLevelAuthoring;

/// <summary>Balances known colors by target capacity while preserving mechanic-bearing boxes.</summary>
public static class LoopLevelColorBalance
{
    /// <summary>Plans local additions/removals; reports unresolved colors without claiming solvability.</summary>
    public static void Balance(JObject root, out int modified, out string report)
    {
        modified = 0;
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");
        var counts = new SortedDictionary<int, int>();
        var editable = new List<JObject>();
        for (int b = 0; b < boxes.Count; b++)
        {
            JObject box = (JObject)boxes[b];
            if (LoopLevelCubeEditing.CanAdjustContents(box) && !LoopLevelCubeEditing.IsGrouped(layout, b))
                editable.Add(box);
            // Single queues and protected cubes still contribute to the level's total colors.
            foreach (JObject cube in A.Array(box, "Cubes"))
            {
                int color = A.Int(cube, "Type");
                if (!IsKnownColor(color)) continue;
                counts.TryGetValue(color, out int count);
                counts[color] = count + 1;
            }
        }
        var lines = new List<string>();
        foreach (KeyValuePair<int, int> pair in counts)
        {
            int capacity = LoopLevelCommands.ColorTargetCapacity(pair.Key);
            int remainder = pair.Value % capacity;
            if (remainder == 0) continue;
            int needed = capacity - remainder;
            int free = CountFreeSlots(editable, pair.Key);
            int removable = CountRemovable(editable, pair.Key);
            bool add = free >= needed && (needed <= remainder || removable < remainder);
            if (add)
            {
                AddCubes(editable, pair.Key, needed);
                modified += needed;
                lines.Add("Màu " + pair.Key + ": thêm " + needed + " → " + (pair.Value + needed) + " (bộ " + capacity + ")");
                continue;
            }
            if (removable >= remainder)
            {
                RemoveCubes(editable, pair.Key, remainder);
                modified += remainder;
                lines.Add("Màu " + pair.Key + ": bớt " + remainder + " → " + (pair.Value - remainder) + " (bộ " + capacity + ")");
                continue;
            }
            lines.Add("Màu " + pair.Key + ": CHƯA cân bằng; cần thêm " + needed + " hoặc bớt " + remainder + ", không đủ ô/cube có thể sửa an toàn.");
        }
        if (lines.Count == 0) lines.Add("Các màu thường/dài đã tròn bộ 4/6 hoặc không có màu cần cân bằng.");
        lines.Add("Giữ nguyên hộp có cơ chế. Cân bằng số lượng không chứng minh có lời giải; màu đặc biệt cần chơi thử.");
        report = string.Join("\n", lines);
    }

    private static bool IsKnownColor(int color)
    {
        return color >= 1 && color <= 14 || color >= 101 && color <= 105;
    }

    private static int CountFreeSlots(List<JObject> boxes, int color)
    {
        int free = 0;
        foreach (JObject box in boxes)
        {
            if (!BottleRules.IsColorCompatible((BoxType)A.Int(box, "BoxType"), color)) continue;
            free += A.CubeCapacity(box) - A.Array(box, "Cubes").Count;
        }
        return free;
    }

    private static int CountRemovable(List<JObject> boxes, int color)
    {
        int count = 0;
        foreach (JObject box in boxes)
            foreach (JObject cube in A.Array(box, "Cubes"))
                if (A.Int(cube, "Type") == color) count++;
        return count;
    }

    private static void AddCubes(List<JObject> boxes, int color, int remaining)
    {
        foreach (JObject box in boxes)
        {
            if (!BottleRules.IsColorCompatible((BoxType)A.Int(box, "BoxType"), color)) continue;
            JArray cubes = A.Array(box, "Cubes");
            int added = Math.Min(remaining, A.CubeCapacity(box) - cubes.Count);
            for (int i = 0; i < added; i++) cubes.Add(A.NewCube(color));
            box["Cubes"] = cubes;
            remaining -= added;
            if (remaining == 0) return;
        }
    }

    private static void RemoveCubes(List<JObject> boxes, int color, int remaining)
    {
        for (int b = boxes.Count - 1; b >= 0; b--)
        {
            JArray cubes = A.Array(boxes[b], "Cubes");
            for (int c = cubes.Count - 1; c >= 0; c--)
            {
                if (A.Int((JObject)cubes[c], "Type") != color) continue;
                cubes.RemoveAt(c);
                remaining--;
                if (remaining == 0) return;
            }
        }
    }
}
#endif
