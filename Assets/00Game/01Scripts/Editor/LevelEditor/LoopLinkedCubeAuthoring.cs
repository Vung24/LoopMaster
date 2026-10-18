#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using A = LoopLevelAuthoring;

public static class LoopLinkedCubeAuthoring
{
    [UnityEditor.MenuItem("LoopMaster/Cube đôi/1. Hai khối đã lộ")]
    private static void PlayExposed() => PlayFixture("01_Exposed");

    [UnityEditor.MenuItem("LoopMaster/Cube đôi/2. Một bên bị chặn")]
    private static void PlayOneBlocked() => PlayFixture("02_OneBlocked");

    [UnityEditor.MenuItem("LoopMaster/Cube đôi/3. Hai bên bị chặn")]
    private static void PlayBothBlocked() => PlayFixture("03_BothBlocked");

    [UnityEditor.MenuItem("LoopMaster/Cube đôi/4. Cùng màu và ranh giới nhóm")]
    private static void PlaySameColor() => PlayFixture("04_SameColorBoundary");

    [UnityEditor.MenuItem("LoopMaster/Cube đôi/5. Độc lập sau khi thả")]
    private static void PlayIndependent() => PlayFixture("05_IndependentAfterLaunch");

    private static void PlayFixture(string name)
    {
        string path = "Assets/00Game/05LevelData/LinkedCubes/" + name + ".json";
        JObject root = A.Parse(System.IO.File.ReadAllText(path));
        if (!LoopLevelPlaytest.Play(root, out string error)) Debug.LogError(error);
    }

    public static void Link(JObject root, int firstBox, int firstCube, int secondBox, int secondCube)
    {
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        if (firstBox == secondBox) throw new FormatException("Chọn hai khối ở hai hộp khác nhau.");
        JObject first = Cube(boxes, firstBox, firstCube), second = Cube(boxes, secondBox, secondCube);
        foreach (JObject box in boxes)
            foreach (JObject cube in A.Array(box, "Cubes"))
                if (A.Int(cube, "LinkedPairId") != 0) throw new FormatException("Prototype chỉ hỗ trợ một cặp. Tháo cặp hiện tại trước.");
        int oldFirst = A.Int(first, "LinkedPairId"), oldSecond = A.Int(second, "LinkedPairId");
        first["LinkedPairId"] = 1;
        second["LinkedPairId"] = 1;
        try { LinkedCubePair.Validate(ExportedPuzzleJsonDecoder.Decode(root.ToString())); }
        catch
        {
            first["LinkedPairId"] = oldFirst; second["LinkedPairId"] = oldSecond;
            throw;
        }
    }

    private static JObject Cube(JArray boxes, int boxIndex, int cubeIndex)
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
