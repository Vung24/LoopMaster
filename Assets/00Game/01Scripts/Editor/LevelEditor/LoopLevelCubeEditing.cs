#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using A = LoopLevelAuthoring;

/// <summary>Non-destructive cube additions and mixing for manual level authoring.</summary>
public static class LoopLevelCubeEditing
{
    /// <summary>Adds the brush color in free slots without replacing existing cubes.</summary>
    public static void AddColor(JObject root, IEnumerable<int> selection, int color, int count)
    {
        int[] indices = LoopLevelCommands.Selection(root, selection);
        if (indices.Length == 0) throw new FormatException("Chọn ít nhất một hộp để thêm màu.");
        if (color < 1 || color > 14 || count < 1) throw new FormatException("Chọn màu 1–14 và số khối lớn hơn 0.");
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        // Validate the whole selection before editing any box.
        foreach (int index in indices)
        {
            JObject box = (JObject)boxes[index];
            if (HasLinkedCubes(box)) throw new FormatException("Hộp " + index + ": gỡ liên kết Cube đôi trước khi thêm khối.");
            if (A.Array(box, "Cubes").Count >= A.CubeCapacity(box))
                throw new FormatException("Hộp " + index + " đã đầy. Xóa bớt khối hoặc chọn hộp khác.");
        }
        foreach (int index in indices)
        {
            JObject box = (JObject)boxes[index];
            JArray cubes = A.Array(box, "Cubes");
            int added = Math.Min(count, A.CubeCapacity(box) - cubes.Count);
            for (int i = 0; i < added; i++) cubes.Add(A.NewCube(A.BrushColor(box, color)));
            box["Cubes"] = cubes;
        }
    }

    /// <summary>Swaps colors across plain boxes, preserving counts, empty boxes and cube metadata.</summary>
    public static void Mix(JObject root, IEnumerable<int> selection, int seed, int swaps)
    {
        Mix(root, selection, seed, swaps, null);
    }

    /// <summary>Mixes the selection while leaving editor-pinned boxes unchanged.</summary>
    public static void Mix(JObject root, IEnumerable<int> selection, int seed, int swaps, ICollection<int> pinnedBoxes)
    {
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");
        var eligible = new List<int>();
        foreach (int index in LoopLevelCommands.Selection(root, selection))
        {
            if (pinnedBoxes != null && pinnedBoxes.Contains(index)) continue;
            JObject box = (JObject)boxes[index];
            if (CanMix(box) && !IsGrouped(layout, index)) eligible.Add(index);
        }
        JToken before = boxes.DeepClone();
        var random = new Random(seed);
        bool changed = false;
        for (int step = 0; step < swaps; step++)
        {
            // Reservoir sampling chooses a valid cross-box swap without unbounded retries.
            int candidates = 0;
            JObject first = null, second = null;
            for (int a = 0; a < eligible.Count; a++)
            {
                JArray firstCubes = A.Array((JObject)boxes[eligible[a]], "Cubes");
                for (int b = a + 1; b < eligible.Count; b++)
                {
                    JArray secondCubes = A.Array((JObject)boxes[eligible[b]], "Cubes");
                    foreach (JObject left in firstCubes)
                    {
                        foreach (JObject right in secondCubes)
                        {
                            if (A.Int(left, "Type") == A.Int(right, "Type")) continue;
                            candidates++;
                            if (random.Next(candidates) != 0) continue;
                            first = left;
                            second = right;
                        }
                    }
                }
            }
            if (first == null) break;
            int color = A.Int(first, "Type");
            first["Type"] = A.Int(second, "Type");
            second["Type"] = color;
            changed = true;
        }
        if (!changed) throw new FormatException("Chọn ít nhất 2 hộp thường có màu khác nhau, không có khóa/nhóm/liên kết hoặc khối đặc biệt.");
        if (JToken.DeepEquals(before, boxes))
            throw new FormatException("Các lượt đổi triệt tiêu nhau; không có thay đổi. Đổi seed hoặc số lượt trộn.");
    }

    /// <summary>Moves into an append slot or swaps occupied slots across boxes, preserving cube data.</summary>
    public static void TransferCube(JObject layout, int sourceBox, int sourceCube, int targetBox, int targetCube)
    {
        JArray boxes = A.Array(layout, "Boxes");
        if (sourceBox < 0 || sourceBox >= boxes.Count || targetBox < 0 || targetBox >= boxes.Count)
            throw new FormatException("Hộp nguồn hoặc đích không còn tồn tại.");
        JObject source = (JObject)boxes[sourceBox], target = (JObject)boxes[targetBox];
        JArray sourceCubes = A.Array(source, "Cubes"), targetCubes = A.Array(target, "Cubes");
        if (sourceCube < 0 || sourceCube >= sourceCubes.Count || targetCube < 0 || targetCube > targetCubes.Count)
            throw new FormatException("Ô cube nguồn hoặc đích không hợp lệ.");
        if (sourceBox == targetBox)
        {
            if (targetCube < targetCubes.Count) LoopLevelCommands.MoveCube(source, sourceCube, targetCube);
            return;
        }
        if (HasLinkedCubes(source) || HasLinkedCubes(target))
            throw new FormatException("Gỡ liên kết cube ở hai hộp trước khi chuyển chéo.");
        JObject moving = (JObject)sourceCubes[sourceCube];
        bool append = targetCube == targetCubes.Count;
        if (A.Int(moving, "IsFixed") != 0) throw new FormatException("Không thể kéo cube cố định sang hộp khác.");
        if (!BottleRules.IsColorCompatible((BoxType)A.Int(target, "BoxType"), A.Int(moving, "Type")))
            throw new FormatException("Màu cube không tương thích với loại hộp đích.");
        if (append)
        {
            if (targetCubes.Count >= A.CubeCapacity(target)) throw new FormatException("Hộp đích đã đầy.");
            targetCubes.Add(moving.DeepClone());
            target["Cubes"] = targetCubes;
            sourceCubes.RemoveAt(sourceCube);
            return;
        }
        JObject replaced = (JObject)targetCubes[targetCube];
        if (A.Int(replaced, "IsFixed") != 0) throw new FormatException("Không thể đổi chỗ cube cố định.");
        if (!BottleRules.IsColorCompatible((BoxType)A.Int(source, "BoxType"), A.Int(replaced, "Type")))
            throw new FormatException("Màu cube đích không tương thích với loại hộp nguồn.");
        JToken copy = moving.DeepClone();
        sourceCubes[sourceCube] = replaced.DeepClone();
        targetCubes[targetCube] = copy;
    }

    private static bool CanMix(JObject box)
    {
        return A.Int(box, "BoxType") == 0 && CanAdjustContents(box);
    }

    internal static bool CanAdjustContents(JObject box)
    {
        int type = A.Int(box, "BoxType");
        if ((type != 0 && type != 2) || HasLinkedCubes(box)) return false;
        if (A.Int(box, "LockColor") != 0 || A.Int(box, "NumLock") != 0 ||
            A.Int(box, "LimitedColor") != 0 || A.Int(box, "IsEnterOnly") != 0 ||
            A.Int(box, "IsAdjacentLocked") != 0 || A.Array(box, "MultiColorLock").Count > 0) return false;
        JArray cubes = A.Array(box, "Cubes");
        if (cubes.Count > A.CubeCapacity(box)) return false;
        foreach (JObject cube in cubes)
        {
            int color = A.Int(cube, "Type");
            bool normal = color >= 1 && color <= 14;
            bool longColor = color >= 101 && color <= 105;
            if ((!normal && !longColor) || !BottleRules.IsColorCompatible((BoxType)type, color) || A.Int(cube, "IsFixed") != 0 ||
                A.Int(cube, "IsSecret") != 0 || A.Int(cube, "GroupKeyId") != 0 ||
                A.Int(cube, "NumLock") != 0) return false;
        }
        return true;
    }

    private static bool HasLinkedCubes(JObject box)
    {
        if (A.Array(box, "LinkedCubes").Count > 0) return true;
        foreach (JObject cube in A.Array(box, "Cubes"))
            if (A.Int(cube, "LinkedPairId") != 0) return true;
        return false;
    }

    internal static bool IsGrouped(JObject layout, int index)
    {
        foreach (string collection in new[] { "Groups", "Covers" })
            foreach (JObject group in A.Array(layout, collection))
                foreach (JToken member in A.Array(group, "BoxIndexes"))
                    if (member.Value<int>() == index) return true;
        return false;
    }
}
#endif
