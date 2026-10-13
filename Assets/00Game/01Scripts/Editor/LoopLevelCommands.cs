#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using A = LoopLevelAuthoring;

// Commands preserve JSON tokens and never reorder the box array for display/layout.
public static class LoopLevelCommands
{
    public static int[] Selection(JObject root, IEnumerable<int> selection) => selection
        .Where(i => i >= 0 && i < A.Array(A.Layout(root), "Boxes").Count).Distinct().OrderBy(i => i).ToArray();

    public static void DeleteGroup(JObject layout, int id)
    {
        JArray groups = A.Array(layout, "Groups");
        for (int i = groups.Count - 1; i >= 0; i--) if (A.Int((JObject)groups[i], "Id") == id) groups.RemoveAt(i);
        foreach (JObject box in A.Array(layout, "Boxes"))
            foreach (JObject cube in A.Array(box, "Cubes"))
                if (A.Int(cube, "GroupKeyId") == id) cube["GroupKeyId"] = 0;
    }

    public static void RenameGroup(JObject layout, JObject group, int id)
    {
        int previous = A.Int(group, "Id");
        if (id <= 0 || A.Array(layout, "Groups").OfType<JObject>().Any(g => g != group && A.Int(g, "Id") == id))
            throw new FormatException("ID nhóm phải lớn hơn 0 và không trùng.");
        group["Id"] = id;
        foreach (JObject box in A.Array(layout, "Boxes"))
            foreach (JObject cube in A.Array(box, "Cubes"))
                if (A.Int(cube, "GroupKeyId") == previous) cube["GroupKeyId"] = id;
    }

    public static void DeleteBoxes(JObject root, IEnumerable<int> selection)
    {
        int[] removed = Selection(root, selection);
        int[] ids = A.Array(A.Layout(root), "Groups").OfType<JObject>().Select(g => A.Int(g, "Id")).ToArray();
        foreach (int index in removed.Reverse()) A.RemoveBox(root, index);
        JObject layout = A.Layout(root);
        foreach (int id in ids)
            if (!A.Array(layout, "Groups").OfType<JObject>().Any(g => A.Int(g, "Id") == id)) DeleteGroup(layout, id);
    }

    public static int[] Duplicate(JObject root, IEnumerable<int> selection, Vector3 offset, bool wholeMechanism)
    {
        JObject layout = A.Layout(root); JArray boxes = A.Array(layout, "Boxes");
        int[] indices = Selection(root, selection);
        var mapping = new Dictionary<int, int>(); var groups = new List<JObject>();
        var groupMap = new Dictionary<int, int>();
        if (wholeMechanism)
        {
            int next = A.Array(layout, "Groups").OfType<JObject>().Select(g => A.Int(g, "Id")).DefaultIfEmpty().Max();
            foreach (JObject group in A.Array(layout, "Groups"))
            {
                int id = A.Int(group, "Id");
                bool member = A.Array(group, "BoxIndexes").Any(t => indices.Contains(t.Value<int>()));
                bool key = indices.Any(i => A.Array((JObject)boxes[i], "Cubes").OfType<JObject>().Any(c => A.Int(c, "GroupKeyId") == id));
                if (!member && !key) continue;
                if (!member || !key || A.Array(group, "BoxIndexes").Any(t => !indices.Contains(t.Value<int>())))
                    throw new FormatException("Chọn đủ hộp và hộp chứa chìa khóa của nhóm " + id + " trước khi nhân bản cụm cơ chế.");
                groups.Add((JObject)group.DeepClone()); groupMap[id] = ++next;
            }
        }
        foreach (int i in indices)
        {
            JObject copy = (JObject)boxes[i].DeepClone();
            copy["Position"] = A.Vector(A.Vector(copy, "Position") + offset);
            // A new object gets a new automatic exit; never clone an old slot index.
            copy["PathIndexV2"] = 0;
            foreach (JObject cube in A.Array(copy, "Cubes"))
                if (A.Int(cube, "GroupKeyId") > 0)
                    cube["GroupKeyId"] = wholeMechanism && groupMap.TryGetValue(A.Int(cube, "GroupKeyId"), out int id) ? id : 0;
            mapping[i] = boxes.Count; boxes.Add(copy);
        }
        foreach (JObject group in groups)
        {
            group["Id"] = groupMap[A.Int(group, "Id")];
            group["BoxIndexes"] = new JArray(A.Array(group, "BoxIndexes").Select(t => mapping[t.Value<int>()]));
            A.Array(layout, "Groups").Add(group);
        }
        return indices.Select(i => mapping[i]).ToArray();
    }

    public static int CreateGroup(JObject root, IEnumerable<int> selection, int color)
    {
        int[] indices = Selection(root, selection); if (indices.Length == 0) throw new FormatException("Chọn hộp cho nhóm trước.");
        JObject layout = A.Layout(root); JArray groups = A.Array(layout, "Groups");
        if (groups.OfType<JObject>().Any(g => A.Array(g, "BoxIndexes").Any(t => indices.Contains(t.Value<int>()))))
            throw new FormatException("Một hộp chỉ thuộc một nhóm. Bỏ khỏi nhóm cũ trước khi tạo nhóm mới.");
        int id = groups.OfType<JObject>().Select(g => A.Int(g, "Id")).DefaultIfEmpty().Max() + 1;
        groups.Add(new JObject { ["Id"] = id, ["LockedByColor"] = color, ["BoxIndexes"] = new JArray(indices) });
        layout["Groups"] = groups; return id;
    }

    public static void Move(JObject root, IEnumerable<int> selection, Vector3 delta)
    {
        foreach (int i in Selection(root, selection))
        {
            JObject box = (JObject)A.Array(A.Layout(root), "Boxes")[i];
            box["Position"] = A.Vector(A.Vector(box, "Position") + delta);
        }
    }

    public static void Rotate(JObject root, IEnumerable<int> selection, float degrees)
    {
        int[] indices = Selection(root, selection); if (indices.Length == 0) return;
        JArray boxes = A.Array(A.Layout(root), "Boxes"); Vector3 center = Vector3.zero;
        foreach (int i in indices) center += A.Vector((JObject)boxes[i], "Position"); center /= indices.Length;
        Quaternion rotation = Quaternion.Euler(0, degrees, 0);
        foreach (int i in indices)
        {
            JObject box = (JObject)boxes[i];
            box["Position"] = A.Vector(center + rotation * (A.Vector(box, "Position") - center));
            if (A.Int(box, "BoxType") != 1) box["Rotation"] = A.Vector((rotation * Quaternion.Euler(A.Vector(box, "Rotation"))).eulerAngles);
        }
    }

    public static void Align(JObject root, IEnumerable<int> selection, bool xAxis, bool distribute)
    {
        int[] indices = Selection(root, selection); if (indices.Length < 2) return;
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        Func<int, float> coordinate = i => xAxis ? A.Vector((JObject)boxes[i], "Position").x : A.Vector((JObject)boxes[i], "Position").z;
        int[] order = indices.OrderBy(coordinate).ToArray();
        float low = coordinate(order[0]), high = coordinate(order[order.Length - 1]);
        float mean = indices.Average(coordinate);
        for (int n = 0; n < order.Length; n++)
        {
            JObject box = (JObject)boxes[order[n]]; Vector3 position = A.Vector(box, "Position");
            float value = distribute ? Mathf.Lerp(low, high, n / (float)(order.Length - 1)) : mean;
            if (xAxis) position.x = value; else position.z = value;
            box["Position"] = A.Vector(position);
        }
    }

    public static void Mirror(JObject root, IEnumerable<int> selection)
    {
        int[] indices = Selection(root, selection); if (indices.Length == 0) return;
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        if (indices.Any(i => Mathf.Abs(A.Vector((JObject)boxes[i], "Rotation").x) > .001f || Mathf.Abs(A.Vector((JObject)boxes[i], "Rotation").z) > .001f))
            throw new FormatException("Phản chiếu chỉ hỗ trợ hộp xoay quanh Y; dùng Scene cho hộp nghiêng.");
        float center = indices.Average(i => A.Vector((JObject)boxes[i], "Position").x);
        foreach (int i in indices)
        {
            JObject box = (JObject)boxes[i]; Vector3 position = A.Vector(box, "Position"), rotation = A.Vector(box, "Rotation");
            position.x = 2 * center - position.x; box["Position"] = A.Vector(position);
            if (A.Int(box, "BoxType") != 1) { rotation.y = -rotation.y; box["Rotation"] = A.Vector(rotation); }
        }
    }

    public static void MoveCube(JObject box, int source, int target)
    {
        int count = A.Array(box, "Cubes").Count;
        if (source < 0 || source >= count || target < 0 || target >= count) return;
        int direction = source < target ? 1 : -1;
        for (int i = source; i != target; i += direction) A.SwapCubes(box, i, i + direction);
    }

    public static void ArrangeBoxesOnly(JObject root, IEnumerable<int> selection, LoopLevelGeneratorSettings settings, int seed)
    {
        int[] indices = Selection(root, selection); if (indices.Length == 0) return;
        JObject layout = A.Layout(root), temporary = (JObject)layout.DeepClone(); JArray boxes = A.Array(layout, "Boxes");
        temporary["Boxes"] = new JArray(indices.Select(i => boxes[i].DeepClone()));
        Vector3 anchor = LoopLevelBoardLayout.BoardBounds(temporary).center; anchor.y = 0;
        LoopLevelBoardLayout.Arrange(temporary, settings, seed);
        JArray arranged = A.Array(temporary, "Boxes");
        for (int n = 0; n < indices.Length; n++)
        {
            boxes[indices[n]]["Position"] = A.Vector(A.Vector((JObject)arranged[n], "Position") + anchor);
            boxes[indices[n]]["Rotation"] = arranged[n]["Rotation"].DeepClone();
        }
    }

    public static void SwapColors(JObject root, int first, int second)
    {
        if (first < 1 || first > 14 || second < 1 || second > 14) throw new FormatException("Chọn hai màu thường 1–14.");
        JObject layout = A.Layout(root);
        Action<JObject, string> swap = (obj, key) => {
            int value = A.Int(obj, key); if (value == first) obj[key] = second; else if (value == second) obj[key] = first;
        };
        foreach (JObject box in A.Array(layout, "Boxes"))
        {
            foreach (string field in new[] { "LockColor", "LimitedColor" }) swap(box, field);
            foreach (JObject cube in A.Array(box, "Cubes")) swap(cube, "Type");
            if (box["MultiColorLock"] is JArray colors)
                for (int i = 0; i < colors.Count; i++) { int c = colors[i].Value<int>(); if (c == first) colors[i] = second; else if (c == second) colors[i] = first; }
        }
        foreach (JObject group in A.Array(layout, "Groups")) swap(group, "LockedByColor");
        foreach (JObject surprise in A.Array(layout, "SurpriseDatas"))
            foreach (JObject cube in A.Array(surprise, "NormalCubes")) swap(cube, "Type");
    }

    public static void ShuffleContents(JObject root, IEnumerable<int> selection, int seed)
    {
        int[] indices = Selection(root, selection); JArray boxes = A.Array(A.Layout(root), "Boxes");
        if (indices.Any(i => A.Int((JObject)boxes[i], "BoxType") != 0 || A.Array((JObject)boxes[i], "LinkedCubes").Count > 0))
            throw new FormatException("Trộn nội dung chỉ hỗ trợ hộp thường không có LinkedCubes.");
        var tokens = indices.SelectMany(i => A.Array((JObject)boxes[i], "Cubes")).Select(t => t.DeepClone()).ToList();
        var random = new System.Random(seed);
        for (int i = tokens.Count - 1; i > 0; i--) { int j = random.Next(i + 1); JToken t = tokens[i]; tokens[i] = tokens[j]; tokens[j] = t; }
        int offset = 0;
        foreach (int i in indices)
        {
            JObject box = (JObject)boxes[i]; JArray cubes = A.Array(box, "Cubes");
            for (int c = 0; c < cubes.Count; c++) cubes[c] = tokens[offset++];
            if (cubes.Count > 0) cubes[cubes.Count - 1]["IsSecret"] = 0;
        }
    }

    public static void RemapBindings(JObject before, JObject after, IEnumerable<int> moved = null)
    {
        JObject oldLayout = A.Layout(before), layout = A.Layout(after);
        bool roadChanged = !JToken.DeepEquals(oldLayout["Paths"], layout["Paths"]) ||
            !JToken.DeepEquals(oldLayout["PathSectionDistance"], layout["PathSectionDistance"]);
        int[] targets = moved == null ? Array.Empty<int>() : Selection(after, moved);
        if (!roadChanged && targets.Length == 0) return;
        List<Vector3> oldSlots = roadChanged ? A.Slots(before) : null, newSlots = A.Slots(after);
        if (newSlots.Count == 0) return;
        JArray boxes = A.Array(layout, "Boxes"), oldBoxes = A.Array(oldLayout, "Boxes");
        for (int i = 0; i < boxes.Count; i++)
        {
            JObject box = (JObject)boxes[i]; int binding = A.Int(box, "PathIndexV2");
            if (binding <= 0) continue;
            if (targets.Contains(i)) { box["PathIndexV2"] = A.NearestSlot(box, newSlots); continue; }
            // Explicit user edits win over automatic remapping.
            if (!roadChanged || i >= oldBoxes.Count || binding != A.Int((JObject)oldBoxes[i], "PathIndexV2") || binding > oldSlots.Count) continue;
            Vector3 anchor = oldSlots[binding - 1]; int nearest = 0; float distance = float.MaxValue;
            for (int n = 0; n < newSlots.Count; n++)
            {
                float d = (newSlots[n] - anchor).sqrMagnitude;
                if (d < distance) { nearest = n; distance = d; }
            }
            box["PathIndexV2"] = nearest + 1;
        }
    }

    public static List<string> DesignWarnings(JObject root)
    {
        var result = new List<string>(); JObject layout = A.Layout(root); JArray boxes = A.Array(layout, "Boxes");
        Puzzle puzzle = ExportedPuzzleJsonDecoder.Decode(root.ToString());
        foreach (JObject group in A.Array(layout, "Groups"))
        {
            int id = A.Int(group, "Id");
            if (!boxes.OfType<JObject>().Any(b => A.Array(b, "Cubes").OfType<JObject>().Any(c => A.Int(c, "GroupKeyId") == id)))
                result.Add("Nhóm " + id + ": chưa có chìa khóa. Chọn công cụ Chìa khóa rồi bấm một khối.");
        }
        for (int i = 0; i < boxes.Count; i++)
        {
            JObject box = (JObject)boxes[i];
            if (A.Int(box, "IsAdjacentLocked") > 0 && AdjacentLockRules.GetSourceIndexes(puzzle.Boxes, i).Count == 0)
                result.Add("Hộp " + i + ": khóa liền kề không có hộp mở khóa cùng hướng ở khoảng cách phù hợp.");
            JArray cubes = A.Array(box, "Cubes");
            if (cubes.Count > 0 && A.Int((JObject)cubes[cubes.Count - 1], "IsSecret") > 0) result.Add("Hộp " + i + ": khối thả đầu tiên đang bị ẩn.");
        }
        return result;
    }
}
#endif
