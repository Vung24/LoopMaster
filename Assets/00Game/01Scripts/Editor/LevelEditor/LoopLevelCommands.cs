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
    public static int ColorTargetCapacity(int color)
    {
        if (color <= 0 || color >= 201 && color <= 299) return 0;
        if (BottleRules.IsColorCompatible(BoxType.Long, color)) return GameData.LongBoxCubeCapacity;
        return GameData.NormalCubeCapacity;
    }

    public static int CountInitialMoves(GameData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        return LoopLevelGenerator.InSimulation(data, CountCurrentInitialMoves);
    }

    private static int CountCurrentInitialMoves()
    {
        GameData data = GameData.Current;
        if (!data.CanCubeJumpToTransfer()) return 0;
        int count = 0;
        foreach (BoxInfo box in data.Boxes)
        {
            CubeInfo top = box.PeekTopCube();
            if (top != null && top.LinkedPairId > 0)
            {
                if (data.LinkedPair != null && box == data.LinkedPair.FirstBox && LinkedCubeLaunch.CanBegin(data, box, out _)) count++;
                continue;
            }
            if (box.State != BoxState.Free || box.IsEmpty() || !box.CanPopup()) continue;
            if (box.GetTopCubes(false).Count > 0) count++;
        }
        return count;
    }

    public static bool IsCapacity(JObject layout, int index)
    {
        JArray map = A.Array(layout, "MapData");
        return index >= 0 && index < map.Count && map[index] is JObject item && item.Value<string>("Name") == "Capacity";
    }

    public static int AddCapacity(JObject root)
    {
        JObject layout = A.Layout(root);
        JArray map = layout["MapData"] as JArray;
        if (map == null && layout["MapData"] != null && layout["MapData"].Type != JTokenType.Null)
            throw new FormatException("MapData phải là mảng trước khi thêm Capacity. Dữ liệu hiện có được giữ nguyên.");
        if (map == null) { map = new JArray(); layout["MapData"] = map; }
        for (int i = 0; i < map.Count; i++)
            if (IsCapacity(layout, i)) return i;
        Bounds bounds = LoopLevelBoardLayout.BoardBounds(layout, true);
        map.Add(new JObject
        {
            ["Name"] = "Capacity",
            ["Position"] = A.Vector(new Vector3(bounds.center.x, .2f, bounds.max.z + 1.5f)),
            ["Rotation"] = A.Vector(Vector3.zero), ["Scale"] = A.Vector(Vector3.one)
        });
        return map.Count - 1;
    }

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
        var copiedBoxes = new List<JObject>();
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
            mapping[i] = boxes.Count; boxes.Add(copy); copiedBoxes.Add(copy);
        }
        LoopLinkedCubeAuthoring.RemapCopiedMembers(layout, copiedBoxes);
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
            Vector3 position = A.Vector((JObject)arranged[n], "Position") + anchor;
            position.y = A.Vector((JObject)boxes[indices[n]], "Position").y;
            boxes[indices[n]]["Position"] = A.Vector(position);
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

    public static bool IsEnter(JObject layout, int index)
    {
        JArray map = A.Array(layout, "MapData");
        if (index < 0 || index >= map.Count || !(map[index] is JObject item)) return false;
        string name = item.Value<string>("Name");
        return string.Equals(name, "Enter", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "MapEnter", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExit(JObject layout, int index)
    {
        JArray map = A.Array(layout, "MapData");
        if (index < 0 || index >= map.Count || !(map[index] is JObject item)) return false;
        string name = item.Value<string>("Name");
        return string.Equals(name, "Exit", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "MapExit", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMapItem(JObject layout, int index)
    {
        return IsMapItem(layout, index, out _);
    }

    public static bool IsMapItem(JObject layout, int index, out string name)
    {
        name = null;
        JArray map = A.Array(layout, "MapData");
        if (index < 0 || index >= map.Count || !(map[index] is JObject item)) return false;
        name = item.Value<string>("Name");
        return !string.IsNullOrEmpty(name);
    }

    public static void DeleteMapItem(JObject layout, int index)
    {
        JArray map = A.Array(layout, "MapData");
        if (index >= 0 && index < map.Count)
        {
            map.RemoveAt(index);
        }
    }

    public static int AddBox(JObject root, int boxType, float defaultYaw = 0f)
    {
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");
        Vector3 newPos = Vector3.zero;
        float yaw = defaultYaw;

        if (boxes.Count > 0)
        {
            JObject lastBox = (JObject)boxes[boxes.Count - 1];
            Vector3 lastPos = A.Vector(lastBox, "Position");
            Vector3 lastRot = A.Vector(lastBox, "Rotation");
            if (Mathf.Abs(lastRot.y - 270f) < 10f || Mathf.Abs(lastRot.y - 90f) < 10f)
            {
                newPos = lastPos + new Vector3(0, 0, -1.7f);
                yaw = lastRot.y;
            }
            else
            {
                newPos = lastPos + new Vector3(2.4f, 0, 0);
                yaw = lastRot.y;
            }
        }

        JObject box = A.NewBox(newPos);
        box["BoxType"] = boxType;
        box["Rotation"] = A.Vector(new Vector3(0, yaw, 0));
        boxes.Add(box);
        return boxes.Count - 1;
    }

    public static void BalanceCubesToSetsOfFour(JObject root, out int modified, out string report)
    {
        modified = 0;
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");
        if (boxes.Count == 0)
        {
            report = "Chưa có hộp nào để cân bằng.";
            return;
        }

        var colorCounts = new Dictionary<int, int>();
        for (int b = 0; b < boxes.Count; b++)
        {
            JObject box = (JObject)boxes[b];
            if (A.Int(box, "BoxType") == 1) continue;
            JArray cubes = A.Array(box, "Cubes");
            for (int c = 0; c < cubes.Count; c++)
            {
                int type = A.Int((JObject)cubes[c], "Type");
                if (type <= 0 || (type >= 201 && type <= 299)) continue;
                if (!colorCounts.ContainsKey(type)) colorCounts[type] = 0;
                colorCounts[type]++;
            }
        }

        var reportLines = new List<string>();
        foreach (var kv in colorCounts)
        {
            int color = kv.Key;
            int count = kv.Value;
            int rem = count % 4;
            if (rem == 0) continue;

            int needed = (4 - rem) % 4;
            if (needed <= 2)
            {
                int added = 0;
                for (int b = 0; b < boxes.Count && added < needed; b++)
                {
                    JObject box = (JObject)boxes[b];
                    if (A.Int(box, "BoxType") != 0) continue;
                    JArray cubes = A.Array(box, "Cubes");
                    if (cubes.Count < 4)
                    {
                        cubes.Add(A.NewCube(color));
                        box["Cubes"] = cubes;
                        added++;
                        modified++;
                    }
                }
                if (added == needed)
                {
                    reportLines.Add("Màu " + color + ": thêm " + added + " khối -> tròn " + (count + added));
                }
                else
                {
                    // Rollback partial adds and remove remainder instead
                    for (int b = boxes.Count - 1; b >= 0 && added > 0; b--)
                    {
                        JObject box = (JObject)boxes[b];
                        if (A.Int(box, "BoxType") != 0) continue;
                        JArray cubes = A.Array(box, "Cubes");
                        if (cubes.Count > 0 && A.Int((JObject)cubes[cubes.Count - 1], "Type") == color)
                        {
                            cubes.RemoveAt(cubes.Count - 1);
                            added--;
                            modified--;
                        }
                    }
                    int removed = 0;
                    for (int b = boxes.Count - 1; b >= 0 && removed < rem; b--)
                    {
                        JObject box = (JObject)boxes[b];
                        if (A.Int(box, "BoxType") != 0) continue;
                        JArray cubes = A.Array(box, "Cubes");
                        for (int c = cubes.Count - 1; c >= 0 && removed < rem; c--)
                        {
                            if (A.Int((JObject)cubes[c], "Type") == color)
                            {
                                cubes.RemoveAt(c);
                                removed++;
                                modified++;
                            }
                        }
                        if (cubes.Count > 0)
                        {
                            ((JObject)cubes[cubes.Count - 1])["IsSecret"] = 0;
                        }
                    }
                    reportLines.Add("Màu " + color + ": bớt " + removed + " khối (do bàn đầy) -> tròn " + (count - removed));
                }
            }
            else
            {
                int removed = 0;
                for (int b = boxes.Count - 1; b >= 0 && removed < rem; b--)
                {
                    JObject box = (JObject)boxes[b];
                    if (A.Int(box, "BoxType") != 0) continue;
                    JArray cubes = A.Array(box, "Cubes");
                    for (int c = cubes.Count - 1; c >= 0 && removed < rem; c--)
                    {
                        if (A.Int((JObject)cubes[c], "Type") == color)
                        {
                            cubes.RemoveAt(c);
                            removed++;
                            modified++;
                        }
                    }
                    if (cubes.Count > 0)
                    {
                        ((JObject)cubes[cubes.Count - 1])["IsSecret"] = 0;
                    }
                }
                reportLines.Add("Màu " + color + ": bớt " + removed + " khối -> tròn " + (count - removed));
            }
        }

        if (reportLines.Count == 0) report = "Tất cả các màu đã tròn bộ 4 (Hợp lệ).";
        else report = string.Join("; ", reportLines);
    }

    public static void DistributeSolvableCubes(JObject root, IEnumerable<int> selection, int colorCount, int seed, int secretPercent = 0, int setsOfFour = -1)
    {
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        int[] indices = Selection(root, selection);
        if (indices.Length == 0)
        {
            indices = new int[boxes.Count];
            for (int i = 0; i < boxes.Count; i++) indices[i] = i;
        }

        var normalIndices = new List<int>();
        for (int i = 0; i < indices.Length; i++)
        {
            if (A.Int((JObject)boxes[indices[i]], "BoxType") == 0)
                normalIndices.Add(indices[i]);
        }
        if (normalIndices.Count == 0) return;

        colorCount = Mathf.Clamp(colorCount, 1, 14);
        if (setsOfFour <= 0)
        {
            if (colorCount > 0 && colorCount < normalIndices.Count)
            {
                setsOfFour = colorCount;
            }
            else
            {
                setsOfFour = Mathf.Max(1, normalIndices.Count - 1);
            }
        }

        var allCubes = new List<int>();
        for (int s = 0; s < setsOfFour; s++)
        {
            int color = (s % colorCount) + 1;
            for (int k = 0; k < 4; k++)
            {
                allCubes.Add(color);
            }
        }

        var rand = new System.Random(seed);
        for (int i = allCubes.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            int tmp = allCubes[i];
            allCubes[i] = allCubes[j];
            allCubes[j] = tmp;
        }

        int totalCubes = allCubes.Count;
        int boxCount = normalIndices.Count;
        int basePerBox = totalCubes / boxCount;
        int extra = totalCubes % boxCount;
        int[] targetCounts = new int[boxCount];
        for (int i = 0; i < boxCount; i++)
        {
            targetCounts[i] = basePerBox + (i < extra ? 1 : 0);
        }
        for (int i = targetCounts.Length - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            int temp = targetCounts[i];
            targetCounts[i] = targetCounts[j];
            targetCounts[j] = temp;
        }

        int cubeIdx = 0;
        for (int n = 0; n < normalIndices.Count; n++)
        {
            JObject box = (JObject)boxes[normalIndices[n]];
            int target = targetCounts[n];
            int boxLockColor = A.Int(box, "LockColor");
            var cubes = new JArray();
            for (int c = 0; c < target && cubeIdx < allCubes.Count; c++)
            {
                if (boxLockColor > 0 && allCubes[cubeIdx] == boxLockColor)
                {
                    for (int swap = cubeIdx + 1; swap < allCubes.Count; swap++)
                    {
                        if (allCubes[swap] != boxLockColor)
                        {
                            int swapVal = allCubes[cubeIdx];
                            allCubes[cubeIdx] = allCubes[swap];
                            allCubes[swap] = swapVal;
                            break;
                        }
                    }
                }

                JObject cube = A.NewCube(allCubes[cubeIdx++]);
                bool canBeSecret = secretPercent > 0 && c < target - 1 && c < 2;
                if (canBeSecret && rand.Next(100) < secretPercent)
                {
                    cube["IsSecret"] = 1;
                }
                cubes.Add(cube);
            }
            box["Cubes"] = cubes;
        }
    }

    public static void FillSelectedBoxes(JObject root, IEnumerable<int> selection, int color)
    {
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        int[] indices = Selection(root, selection);
        if (indices.Length == 0) return;
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            A.FillBox(box, color);
        }
    }

    public static void ClearSelectedBoxes(JObject root, IEnumerable<int> selection)
    {
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        int[] indices = Selection(root, selection);
        if (indices.Length == 0)
        {
            indices = new int[boxes.Count];
            for (int i = 0; i < boxes.Count; i++) indices[i] = i;
        }
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            box["Cubes"] = new JArray();
        }
    }

    public static void SetBottomSecretCubes(JObject root, IEnumerable<int> selection, int layers = 1)
    {
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        int[] indices = Selection(root, selection);
        if (indices.Length == 0)
        {
            indices = new int[boxes.Count];
            for (int i = 0; i < boxes.Count; i++) indices[i] = i;
        }
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            JArray cubes = A.Array(box, "Cubes");
            for (int c = 0; c < cubes.Count; c++)
            {
                JObject cube = (JObject)cubes[c];
                bool isHidden = layers > 0 && c < layers && c < cubes.Count - 1;
                cube["IsSecret"] = isHidden ? 1 : 0;
            }
        }
    }

    public static void SetSelectedBoxesColorLock(JObject root, IEnumerable<int> selection, int lockColor)
    {
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        int[] indices = Selection(root, selection);
        if (indices.Length == 0) return;
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            box["LockColor"] = lockColor;
        }
    }

    public static void SetupScaffoldBoard(JObject root, int boxCount, int roadShape, int contentMode, int seed)
    {
        if (root == null) return;
        JObject layout = A.Layout(root);
        boxCount = Mathf.Clamp(boxCount, 2, 16);
        roadShape = Mathf.Clamp(roadShape, 0, 5);
        contentMode = Mathf.Clamp(contentMode, 0, 3);

        JArray boxes = A.Array(layout, "Boxes");
        boxes.Clear();
        A.Array(layout, "Groups").Clear();
        layout.Remove("Covers");

        for (int i = 0; i < boxCount; i++)
        {
            boxes.Add(A.NewBox(Vector3.zero));
        }

        if (roadShape == 3)
        {
            if (boxCount <= 4)
            {
                LoopLevelBoardLayout.ArrangeSingleColumn(boxes, null, 0.4f, 270f);
            }
            else
            {
                LoopLevelBoardLayout.ArrangeGrid(boxes, null, 2, 0.4f, 0.6f);
            }
        }
        else if (roadShape >= 4)
        {
            // Open winding or S-curve weaving through middle: boxes face inward towards center path
            LoopLevelBoardLayout.ArrangeTwoBanks(boxes, null, 0.4f, 4.2f);
        }
        else if (boxCount <= 3)
        {
            LoopLevelBoardLayout.ArrangeSingleRow(boxes, null, 0.4f);
        }
        else
        {
            // Closed perimeter loop: boxes face outward so transfer arrows point directly into road
            LoopLevelBoardLayout.ArrangeTwoColumnsFacingOutward(boxes, null, 0.4f, 0.4f);
        }

        Bounds content = LoopLevelBoardLayout.BoardBounds(layout);
        Vector3 shift = new Vector3(content.center.x, 0, content.center.z);
        for (int i = 0; i < boxes.Count; i++)
        {
            JObject box = (JObject)boxes[i];
            box["Position"] = A.Vector(A.Vector(box, "Position") - shift);
        }
        content = LoopLevelBoardLayout.BoardBounds(layout);

        Vector3 center = new Vector3(content.center.x, 0.2f, content.center.z);
        float clearance = 0.75f;
        float width = content.size.x + 2f * clearance;
        float depth = content.size.z + 2f * clearance;

        if (roadShape == 0)
        {
            float radius = Mathf.Clamp(clearance * 0.85f, 0.4f, Mathf.Min(width, depth) * 0.25f);
            layout["Paths"] = new JArray(A.RoundedRectangleRoad(center, width, depth, radius));
            A.SyncEnterExitGates(layout);
        }
        else if (roadShape == 1)
        {
            float halfW = content.size.x * 0.5f;
            float halfD = content.size.z * 0.5f;
            float diag = Mathf.Sqrt(halfW * halfW + halfD * halfD);
            float scale = (diag + clearance + 0.05f) / Mathf.Max(0.001f, diag);
            layout["Paths"] = new JArray(A.OvalRoad(center, 2f * halfW * scale, 2f * halfD * scale));
            A.SyncEnterExitGates(layout);
        }
        else if (roadShape == 2)
        {
            float maxDim = Mathf.Max(width, depth);
            layout["Paths"] = new JArray(A.OvalRoad(center, maxDim, maxDim));
            A.SyncEnterExitGates(layout);
        }
        else if (roadShape == 3)
        {
            A.FitStraightRoadBesideBoxes(layout, clearance, true);
        }
        else if (roadShape == 4)
        {
            layout["Paths"] = new JArray(A.SCurveRoad(center, width, depth));
            A.SyncEnterExitGates(layout);
        }
        else if (roadShape == 5)
        {
            layout["Paths"] = new JArray(A.OpenWindingRoad(center, width, depth));
            A.SyncEnterExitGates(layout);
        }

        layout["PathLength"] = Mathf.Clamp(boxCount, 4, 8);
        AddCapacity(root);

        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);

        if (contentMode == 0)
        {
            int sets = Math.Max(1, boxCount - 1);
            DistributeSolvableCubes(root, null, sets, seed, 0);
        }
        else if (contentMode == 1)
        {
            ClearSelectedBoxes(root, null);
        }
        else if (contentMode == 2)
        {
            int sets = Math.Max(2, boxCount - 1);
            DistributeSolvableCubes(root, null, sets, seed, 0);

            var unlockedColors = new HashSet<int>();
            for (int b = 1; b < boxes.Count; b++)
            {
                JArray cubes = A.Array((JObject)boxes[b], "Cubes");
                for (int c = 0; c < cubes.Count; c++)
                {
                    unlockedColors.Add(A.Int((JObject)cubes[c], "Type"));
                }
            }

            int keyColor = 0;
            foreach (int c in unlockedColors)
            {
                if (c > 0)
                {
                    keyColor = c;
                    break;
                }
            }

            if (keyColor > 0)
            {
                JObject box0 = (JObject)boxes[0];
                box0["LockColor"] = keyColor;

                JArray box0Cubes = A.Array(box0, "Cubes");
                for (int c = 0; c < box0Cubes.Count; c++)
                {
                    if (A.Int((JObject)box0Cubes[c], "Type") == keyColor)
                    {
                        for (int b = 1; b < boxes.Count; b++)
                        {
                            JArray otherCubes = A.Array((JObject)boxes[b], "Cubes");
                            for (int oc = 0; oc < otherCubes.Count; oc++)
                            {
                                int oType = A.Int((JObject)otherCubes[oc], "Type");
                                if (oType != keyColor)
                                {
                                    box0Cubes[c]["Type"] = oType;
                                    otherCubes[oc]["Type"] = keyColor;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        else if (contentMode == 3)
        {
            int sets = Math.Max(1, boxCount - 1);
            DistributeSolvableCubes(root, null, sets, seed, 25);
        }

        Bounds whole = LoopLevelBoardLayout.BoardBounds(layout, true);
        layout["Size"] = Mathf.Max(whole.size.x, whole.size.z) + 1;
    }
}
#endif
