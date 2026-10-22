#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using A = LoopLevelAuthoring;

public readonly struct LinkedCubePairRef
{
    public readonly int Id;
    public readonly int BoxA;
    public readonly int CubeA;
    public readonly int BoxB;
    public readonly int CubeB;

    public LinkedCubePairRef(int id, int boxA, int cubeA, int boxB, int cubeB)
    {
        Id = id;
        BoxA = boxA;
        CubeA = cubeA;
        BoxB = boxB;
        CubeB = cubeB;
    }
}

public static class LoopLinkedCubeAuthoring
{
    public static bool HasLinkedCube(JObject box)
    {
        if (box == null) return false;
        JArray cubes = box["Cubes"] as JArray;
        if (cubes == null) return false;
        for (int i = 0; i < cubes.Count; i++)
        {
            if (cubes[i] is JObject cube && A.Int(cube, "LinkedPairId") > 0) return true;
        }
        return false;
    }

    public static bool TryGetLinkedPair(JObject layout, out int id, out int boxA, out int cubeA, out int boxB, out int cubeB)
    {
        bool found = TryGetLinkedPair(layout, out LinkedCubePairRef pairRef);
        id = pairRef.Id;
        boxA = pairRef.BoxA;
        cubeA = pairRef.CubeA;
        boxB = pairRef.BoxB;
        cubeB = pairRef.CubeB;
        return found;
    }

    public static bool TryGetLinkedPair(JObject layout, out LinkedCubePairRef pairRef)
    {
        int id = 0, boxA = -1, cubeA = -1, boxB = -1, cubeB = -1;
        pairRef = new LinkedCubePairRef(id, boxA, cubeA, boxB, cubeB);
        if (layout == null) return false;
        JArray boxes = A.Array(layout, "Boxes");
        for (int b = 0; b < boxes.Count; b++)
        {
            JArray cubes = A.Array((JObject)boxes[b], "Cubes");
            for (int c = 0; c < cubes.Count; c++)
            {
                int pairId = A.Int((JObject)cubes[c], "LinkedPairId");
                if (pairId <= 0) continue;
                if (id == 0)
                {
                    id = pairId;
                    boxA = b;
                    cubeA = c;
                }
                else if (pairId == id)
                {
                    boxB = b;
                    cubeB = c;
                    pairRef = new LinkedCubePairRef(id, boxA, cubeA, boxB, cubeB);
                    return true;
                }
            }
        }
        pairRef = new LinkedCubePairRef(id, boxA, cubeA, boxB, cubeB);
        return false;
    }

    /// <summary>Returns every complete pair for canvas previews and selection.</summary>
    public static List<LinkedCubePairRef> GetLinkedPairs(JObject layout)
    {
        var firstMembers = new Dictionary<int, LinkedCubePairRef>();
        var pairs = new List<LinkedCubePairRef>();
        if (layout == null) return pairs;
        JArray boxes = A.Array(layout, "Boxes");
        for (int box = 0; box < boxes.Count; box++)
        {
            JArray cubes = A.Array((JObject)boxes[box], "Cubes");
            for (int cube = 0; cube < cubes.Count; cube++)
            {
                int id = A.Int((JObject)cubes[cube], "LinkedPairId");
                if (id <= 0) continue;
                if (firstMembers.TryGetValue(id, out LinkedCubePairRef first))
                    pairs.Add(new LinkedCubePairRef(id, first.BoxA, first.CubeA, box, cube));
                else firstMembers.Add(id, new LinkedCubePairRef(id, box, cube, -1, -1));
            }
        }
        return pairs;
    }

    /// <summary>Selects the pair belonging to the selected box, if it has one.</summary>
    public static bool TryGetLinkedPairForBox(JObject layout, int box, out LinkedCubePairRef pairRef)
    {
        foreach (LinkedCubePairRef pair in GetLinkedPairs(layout))
        {
            if (pair.BoxA != box && pair.BoxB != box) continue;
            pairRef = pair;
            return true;
        }
        pairRef = default;
        return false;
    }

    public static bool TryLinkTopCubes(JObject root, int boxA, int boxB, out string error)
    {
        error = null;
        if (root == null) { error = "Dữ liệu màn chơi rỗng."; return false; }
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");
        if (boxA == boxB) { error = "Chọn hai hộp khác nhau."; return false; }
        if (boxA < 0 || boxA >= boxes.Count || boxB < 0 || boxB >= boxes.Count) { error = "Chỉ số hộp không hợp lệ."; return false; }

        JObject bA = (JObject)boxes[boxA];
        JObject bB = (JObject)boxes[boxB];
        if (A.Int(bA, "BoxType") != 0 || A.Int(bB, "BoxType") != 0)
        {
            error = "Cube đôi chỉ hỗ trợ khay hộp thường (Normal).";
            return false;
        }

        JArray cubesA = A.Array(bA, "Cubes");
        JArray cubesB = A.Array(bB, "Cubes");
        if (cubesA.Count == 0 || cubesB.Count == 0)
        {
            error = "Cả hai hộp đều phải có ít nhất một khối màu.";
            return false;
        }

        int cubeA = cubesA.Count - 1;
        int cubeB = cubesB.Count - 1;
        return TryLinkCubes(root, boxA, cubeA, boxB, cubeB, out error);
    }

    public static bool TryLinkCubes(JObject root, int firstBox, int firstCube, int secondBox, int secondCube, out string error)
    {
        error = null;
        try
        {
            Link(root, firstBox, firstCube, secondBox, secondCube);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static void Link(JObject root, int firstBox, int firstCube, int secondBox, int secondCube)
    {
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        if (firstBox == secondBox) throw new FormatException("Chọn hai khối ở hai hộp khác nhau.");
        JObject first = GetCube(boxes, firstBox, firstCube), second = GetCube(boxes, secondBox, secondCube);

		if (HasLinkedCube((JObject)boxes[firstBox]) || HasLinkedCube((JObject)boxes[secondBox]))
			throw new FormatException("Unlink the selected boxes before creating a different pair.");
		int nextId = 1;
		foreach (JObject box in boxes)
		foreach (JObject cube in A.Array(box, "Cubes"))
			nextId = Math.Max(nextId, checked(A.Int(cube, "LinkedPairId") + 1));

        int oldFirst = A.Int(first, "LinkedPairId"), oldSecond = A.Int(second, "LinkedPairId");
        int oldSecretA = A.Int(first, "IsSecret"), oldSecretB = A.Int(second, "IsSecret");
        int oldKeyA = A.Int(first, "GroupKeyId"), oldKeyB = A.Int(second, "GroupKeyId");
        int oldNumA = A.Int(first, "NumLock"), oldNumB = A.Int(second, "NumLock");
        int oldFixA = A.Int(first, "IsFixed"), oldFixB = A.Int(second, "IsFixed");

        first["GroupKeyId"] = 0; first["NumLock"] = 0; first["IsFixed"] = 0;
        second["GroupKeyId"] = 0; second["NumLock"] = 0; second["IsFixed"] = 0;

		first["LinkedPairId"] = nextId;
		second["LinkedPairId"] = nextId;
        try
        {
            LinkedCubePair.Validate(ExportedPuzzleJsonDecoder.Decode(root.ToString(Newtonsoft.Json.Formatting.None)));
        }
        catch
        {
            first["LinkedPairId"] = oldFirst; second["LinkedPairId"] = oldSecond;
            first["IsSecret"] = oldSecretA; second["IsSecret"] = oldSecretB;
            first["GroupKeyId"] = oldKeyA; second["GroupKeyId"] = oldKeyB;
            first["NumLock"] = oldNumA; second["NumLock"] = oldNumB;
            first["IsFixed"] = oldFixA; second["IsFixed"] = oldFixB;
            throw;
        }
    }

    private static JObject GetCube(JArray boxes, int boxIndex, int cubeIndex)
    {
        if (boxIndex < 0 || boxIndex >= boxes.Count) throw new FormatException("Hộp được chọn không còn tồn tại.");
        JArray cubes = A.Array((JObject)boxes[boxIndex], "Cubes");
        if (cubeIndex < 0 || cubeIndex >= cubes.Count) throw new FormatException("Khối được chọn không còn tồn tại.");
        return (JObject)cubes[cubeIndex];
    }

    public static void Unlink(JObject root, int id)
    {
        if (id <= 0 || root == null || !(root["layout"] is JObject layout)) return;
        foreach (JObject box in A.Array(layout, "Boxes"))
            foreach (JObject cube in A.Array(box, "Cubes"))
                if (A.Int(cube, "LinkedPairId") == id) cube["LinkedPairId"] = 0;
    }

    public static void BeforeDeleteCube(JObject box, int cubeIndex)
    {
        JArray cubes = A.Array(box, "Cubes");
        if (cubeIndex < 0 || cubeIndex >= cubes.Count) return;
        Unlink(box.Root as JObject, A.Int((JObject)cubes[cubeIndex], "LinkedPairId"));
    }

    // Remap one import source/duplicated selection at a time, then append it before the next source.
    // Combining multiple sources by their old IDs first would connect unrelated members.
    public static void RemapCopiedMembers(JObject destinationLayout, IList<JObject> copiedBoxes)
    {
        int next = 0;
        foreach (JObject box in A.Array(destinationLayout, "Boxes"))
            foreach (JObject cube in A.Array(box, "Cubes")) next = Math.Max(next, A.Int(cube, "LinkedPairId"));
        var members = new Dictionary<int, List<JObject>>();
        foreach (JObject box in copiedBoxes)
            foreach (JObject cube in A.Array(box, "Cubes"))
            {
                int id = A.Int(cube, "LinkedPairId");
                if (id <= 0) continue;
                if (!members.TryGetValue(id, out List<JObject> pair)) { pair = new List<JObject>(); members.Add(id, pair); }
                pair.Add(cube);
            }
        foreach (List<JObject> pair in members.Values)
        {
            int id = 0;
            if (pair.Count == 2 && pair[0].Parent != pair[1].Parent) id = checked(++next);
            foreach (JObject cube in pair) cube["LinkedPairId"] = id;
        }
    }

    public static Vector3 MemberPosition(JObject box, int cubeIndex)
    {
        return A.Vector(box, "Position") + Quaternion.Euler(A.Vector(box, "Rotation")) *
            new Vector3(0f, .85f, -1.2f + cubeIndex * .8f);
    }
}

public sealed partial class LoopLevelEditorWindow
{
    [SerializeField] private int _linkedFirstBox = -1, _linkedFirstCube = -1;

    private void DrawLinkedCubeControls(JObject box, JObject cube, int cubeIndex)
    {
        int id = A.Int(cube, "LinkedPairId");
        using (new UnityEditor.EditorGUILayout.HorizontalScope())
        {
            UnityEditor.EditorGUILayout.LabelField(id > 0 ? "Cube đôi #" + id : "Cube thường");
            if (id > 0)
            {
                if (GUILayout.Button("Tháo liên kết")) LoopLinkedCubeAuthoring.Unlink(box.Root as JObject, id);
                return;
            }
            if (GUILayout.Button(_linkedFirstBox < 0 ? "Chọn làm đầu nối" : "Nối với khối đã chọn"))
            {
                if (_linkedFirstBox < 0) { _linkedFirstBox = _box; _linkedFirstCube = cubeIndex; }
                else
                {
                    try
                    {
                        LoopLinkedCubeAuthoring.Link(box.Root as JObject, _linkedFirstBox, _linkedFirstCube, _box, cubeIndex);
                        _status = "Đã liên kết hai cube. Khối có thể bị che; phải dọn cả hai trước khi thả.";
                    }
                    catch (Exception error) { _status = error.Message; }
                    _linkedFirstBox = _linkedFirstCube = -1;
                }
            }
            if (_linkedFirstBox >= 0 && GUILayout.Button("Hủy chọn", GUILayout.Width(75)))
                _linkedFirstBox = _linkedFirstCube = -1;
        }
    }

    private void DrawLinkedCubePreview(JObject layout)
    {
        var first = new Dictionary<int, Vector3>();
        UnityEditor.Handles.color = new Color(1f, .55f, .35f);
        foreach (JObject box in A.Array(layout, "Boxes"))
        {
            JArray cubes = A.Array(box, "Cubes");
            for (int i = 0; i < cubes.Count; i++)
            {
                int id = A.Int((JObject)cubes[i], "LinkedPairId");
                if (id <= 0) continue;
                Vector3 point = LoopLinkedCubeAuthoring.MemberPosition(box, i);
                UnityEditor.Handles.Label(point + Vector3.up * .25f, "↔ " + id);
                if (first.TryGetValue(id, out Vector3 partner)) UnityEditor.Handles.DrawDottedLine(partner, point, 3f);
                else first.Add(id, point);
            }
        }
    }
}
#endif
