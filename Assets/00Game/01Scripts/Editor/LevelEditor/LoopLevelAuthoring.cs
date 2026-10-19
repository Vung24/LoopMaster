#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class LoopLevelAuthoring
{
    public const string LevelFolder = "Assets/00Game/Resources/LevelData";
    public const int MaximumSlots = 50000;

    public static JObject Parse(string json)
    {
        var root = JObject.Parse(json);
        if (!(root["layout"] is JObject))
            throw new FormatException("Dữ liệu màn chơi phải có đối tượng \u0027layout\u0027.");
        return root;
    }

    public static JObject Layout(JObject root) => (JObject)root["layout"];
    public static JArray Array(JObject obj, string key)
    {
        if (obj[key] is JArray array) return array;
        return new JArray();
    }
    public static int Int(JObject obj, string key, int fallback = 0) => obj[key]?.Value<int>() ?? fallback;
    public static float Float(JObject obj, string key, float fallback = 0f) => obj[key]?.Value<float>() ?? fallback;
    public static Vector3 Vector(JObject obj, string key)
    {
        var array = obj[key] as JArray;
        return array == null || array.Count != 3 ? Vector3.zero :
            new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
    }
    public static JArray Vector(Vector3 value) => new JArray(value.x, value.y, value.z);

    public static JObject NewLevel(int number)
    {
        var root = new JObject { ["level_number"] = number, ["puzzle_id"] = number,
            ["layout"] = new JObject { ["Version"] = 4, ["HardLevel"] = 0, ["Size"] = 10f,
                ["PathLength"] = 8, ["PathSectionDistance"] = 0.2f,
                ["Boxes"] = new JArray(), ["Paths"] = new JArray(RectanglePath()),
                ["Groups"] = new JArray(),
                ["Covers"] = new JArray(), ["SurpriseDatas"] = new JArray() } };
        return root;
    }

    public static JObject NewBox(Vector3 position) => new JObject {
        ["BoxType"] = 0, ["PathIndex"] = 0, ["PathIndexV2"] = 0,
        ["Position"] = Vector(position), ["Rotation"] = Vector(Vector3.zero),
        ["Cubes"] = new JArray(), ["LinkedCubes"] = new JArray(), ["LockColor"] = 0,
        ["IsEnterOnly"] = 0, ["NumLock"] = 0, ["LimitedColor"] = 0,
        ["IsAdjacentLocked"] = 0, ["MultiColorLock"] = JValue.CreateNull() };

    public static JObject FromTemplate(JObject source, int levelNumber, bool preserveMap = false)
    {
        if (levelNumber < 1) throw new ArgumentOutOfRangeException(nameof(levelNumber));
        JObject draft = (JObject)source.DeepClone();
        draft["level_number"] = levelNumber; draft["puzzle_id"] = levelNumber;
        if (!preserveMap)
        {
            Layout(draft).Remove("MapData");
            SyncEnterExitGates(Layout(draft));
        }
        if (draft["editor_generator"] is JObject proof)
        { proof["verified"] = false; proof.Remove("solution_clicks"); }
        return draft;
    }

    public static bool ApplyCubeBrush(JObject box, int index, int paletteColor, int mode, int groupId)
    {
        if (mode == 0) return PaintCube(box, index, paletteColor);
        JArray cubes = Array(box, "Cubes");
        if (index < 0 || index >= cubes.Count || (mode != 1 && mode != 2)) return false;
        JObject cube = (JObject)cubes[index];
        if (mode == 1) { cube["IsSecret"] = Int(cube, "IsSecret") > 0 ? 0 : 1; return true; }
        if (groupId < 0 || Int(cube, "GroupKeyId") == groupId) return false;
        cube["GroupKeyId"] = groupId; return true;
    }

    public static JObject NewCube(int color) => new JObject { ["Type"] = color,
        ["IsSecret"] = 0, ["GroupKeyId"] = 0, ["NumLock"] = 0, ["IsFixed"] = 0 };

    public static int CubeCapacity(JObject box) => Int(box, "BoxType") == 1 ? 64 : Int(box, "BoxType") == 2 ? 6 : 4;

    public static int BrushColor(JObject box, int paletteColor) =>
        Int(box, "BoxType") == 2 ? 100 + Mathf.Clamp(paletteColor, 1, 5) : Mathf.Clamp(paletteColor, 1, 14);

    public static bool PaintCube(JObject box, int index, int paletteColor)
    {
        JArray cubes = Array(box, "Cubes");
        if (index < 0 || index > cubes.Count || index >= CubeCapacity(box)) return false;
        int color = BrushColor(box, paletteColor);
        if (index == cubes.Count) { cubes.Add(NewCube(color)); box["Cubes"] = cubes; return true; }
        JObject cube = (JObject)cubes[index];
        if (Int(cube, "Type") == color) return false;
        cube["Type"] = color; return true;
    }

    public static void FillBox(JObject box, int paletteColor)
    {
        int target = Int(box, "BoxType") == 1 ? Math.Max(1, Array(box, "Cubes").Count) : CubeCapacity(box);
        for (int i = 0; i < target; i++) PaintCube(box, i, paletteColor);
    }
    public static JObject Point(Vector3 position, Vector3 incoming, Vector3 outgoing) =>
        new JObject { ["Position"] = Vector(position), ["InTangent"] = Vector(incoming),
            ["OutTangent"] = Vector(outgoing) };

    public static JObject RectanglePath()
    {
        var points = new JArray();
        foreach (Vector3 point in new[] { new Vector3(-4, 0.2f, -4), new Vector3(-4, 0.2f, 4),
            new Vector3(4, 0.2f, 4), new Vector3(4, 0.2f, -4), new Vector3(-4, 0.2f, -4) })
            points.Add(Point(point, point, point));
        return new JObject { ["IsCircle"] = 1, ["ControlPoints"] = points };
    }

    public static JObject CirclePath(float radius = 4f)
    {
        var points = new JArray();
        float handle = radius * 0.55228475f;
        for (int i = 0; i <= 4; i++)
        {
            float angle = i * Mathf.PI * 0.5f;
            var position = new Vector3(Mathf.Cos(angle) * radius, 0.2f, Mathf.Sin(angle) * radius);
            var tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * handle;
            points.Add(Point(position, position - tangent, position + tangent));
        }
        points[4] = points[0].DeepClone();
        return new JObject { ["IsCircle"] = 1, ["ControlPoints"] = points };
    }

    public static JObject RoadFromPositions(IReadOnlyList<Vector3> positions, bool closed, float smoothing)
    {
        var points = new JArray();
        foreach (Vector3 position in positions)
        {
            if (points.Count > 0 && (Vector((JObject)points[points.Count - 1], "Position") - position).sqrMagnitude < .000001f) continue;
            points.Add(Point(position, position, position));
        }
        if (closed && points.Count > 1 && (Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f)
            points.RemoveAt(points.Count - 1);
        if (points.Count < (closed ? 3 : 2)) throw new FormatException(closed ? "Vòng kín cần ít nhất ba điểm khác nhau." : "Đường cần ít nhất hai điểm khác nhau.");
        if (points.Count > (closed ? 127 : 128)) throw new FormatException("Đường có tối đa 128 điểm điều khiển, gồm điểm khép kín.");
        var path = new JObject { ["IsCircle"] = closed ? 1 : 0, ["ControlPoints"] = points };
        SmoothRoad(path, smoothing);
        return path;
    }

    public static void SmoothRoad(JObject path, float smoothing)
    {
        JArray points = Array(path, "ControlPoints");
        if (points.Count < 2) return;
        bool repeated = (Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        bool closed = Int(path, "IsCircle") > 0 || repeated;
        if (closed && !repeated)
        {
            if (points.Count >= 128) throw new FormatException("Xóa một điểm trước khi khép kín đường (giới hạn 128 điểm).");
            points.Add(points[0].DeepClone());
        }
        int count = points.Count - (closed ? 1 : 0);
        float factor = Mathf.Clamp01(smoothing) / 3f;
        for (int i = 0; i < count; i++)
        {
            JObject point = (JObject)points[i]; Vector3 current = Vector(point, "Position");
            Vector3 previous = Vector((JObject)points[closed ? (i + count - 1) % count : Math.Max(0, i - 1)], "Position");
            Vector3 next = Vector((JObject)points[closed ? (i + 1) % count : Math.Min(count - 1, i + 1)], "Position");
            float before = Vector3.Distance(previous, current), after = Vector3.Distance(current, next);
            Vector3 direction = ((current - previous).normalized + (next - current).normalized).normalized;
            float length = !closed && i == 0 ? after : !closed && i == count - 1 ? before : Mathf.Min(before, after);
            Vector3 handle = direction * length * factor;
            point["InTangent"] = Vector(current - handle); point["OutTangent"] = Vector(current + handle);
        }
        if (closed)
        {
            var seam = (JObject)points[points.Count - 1]; var first = (JObject)points[0];
            foreach (string key in new[] { "Position", "InTangent", "OutTangent" }) seam[key] = first[key].DeepClone();
        }
    }

    public static bool RemoveRoadPoint(JObject path, int index)
    {
        JArray points = Array(path, "ControlPoints");
        if (index < 0 || index >= points.Count || points.Count < 2) return false;
        bool repeated = (Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        bool closed = Int(path, "IsCircle") > 0 || repeated;
        int anchors = points.Count - (repeated ? 1 : 0);
        if (anchors <= (closed ? 3 : 2)) return false;
        JObject seam = repeated ? (JObject)points[points.Count - 1].DeepClone() : null;
        if (repeated)
        {
            if (index == points.Count - 1) index = 0;
            points.RemoveAt(points.Count - 1);
        }
        points.RemoveAt(index);
        if (closed)
        {
            JObject first = (JObject)points[0];
            if (seam == null) seam = (JObject)first.DeepClone();
            foreach (string key in new[] { "Position", "InTangent", "OutTangent" }) seam[key] = first[key].DeepClone();
            points.Add(seam);
        }
        return true;
    }

    public static JObject OvalRoad(Vector3 center, float width, float depth)
    {
        CheckRoadSize(width, depth);
        JObject path = CirclePath(1f);
        foreach (JObject point in Array(path, "ControlPoints"))
            foreach (string key in new[] { "Position", "InTangent", "OutTangent" })
            {
                Vector3 p = Vector(point, key);
                point[key] = Vector(center + new Vector3(p.x * width * .5f, 0, p.z * depth * .5f));
            }
        return path;
    }

    public static JObject RoundedRectangleRoad(Vector3 center, float width, float depth, float radius)
    {
        CheckRoadSize(width, depth);
        float x = width * .5f, z = depth * .5f;
        radius = Mathf.Clamp(radius, 0, Mathf.Min(x, z) * .999f);
        if (radius < .0001f)
            return RoadFromPositions(new[] { center + new Vector3(-x, 0, -z), center + new Vector3(-x, 0, z),
                center + new Vector3(x, 0, z), center + new Vector3(x, 0, -z) }, true, 0);
        var positions = new[] { new Vector3(-x, 0, -z + radius), new Vector3(-x, 0, z - radius),
            new Vector3(-x + radius, 0, z), new Vector3(x - radius, 0, z),
            new Vector3(x, 0, z - radius), new Vector3(x, 0, -z + radius),
            new Vector3(x - radius, 0, -z), new Vector3(-x + radius, 0, -z) };
        var directions = new[] { Vector3.forward, Vector3.forward, Vector3.right, Vector3.right,
            Vector3.back, Vector3.back, Vector3.left, Vector3.left };
        var points = new JArray();
        float arcHandle = radius * .55228475f;
        for (int i = 0; i < positions.Length; i++)
        {
            int previous = (i + positions.Length - 1) % positions.Length, next = (i + 1) % positions.Length;
            float incoming = i % 2 == 0 ? arcHandle : Vector3.Distance(positions[i], positions[previous]) / 3f;
            float outgoing = i % 2 == 0 ? Vector3.Distance(positions[i], positions[next]) / 3f : arcHandle;
            Vector3 p = center + positions[i];
            points.Add(Point(p, p - directions[i] * incoming, p + directions[i] * outgoing));
        }
        points.Add(points[0].DeepClone());
        return new JObject { ["IsCircle"] = 1, ["ControlPoints"] = points };
    }
    public static void ReverseRoad(JObject path, float smoothing = -1f)
    {
        JArray points = Array(path, "ControlPoints");
        if (points.Count < 2) return;
        bool repeated = (Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        bool closed = Int(path, "IsCircle") > 0 || repeated;
        int count = points.Count - (repeated ? 1 : 0);
        var reversed = new List<JObject>();
        for (int i = count - 1; i >= 0; i--)
        {
            var orig = (JObject)points[i];
            var item = new JObject
            {
                ["Position"] = orig["Position"]?.DeepClone(),
                ["InTangent"] = orig["OutTangent"]?.DeepClone(),
                ["OutTangent"] = orig["InTangent"]?.DeepClone()
            };
            reversed.Add(item);
        }
        if (closed)
        {
            reversed.Add((JObject)reversed[0].DeepClone());
        }
        points.Clear();
        foreach (var p in reversed) points.Add(p);
        if (smoothing >= 0f) SmoothRoad(path, smoothing);
    }

    public static void ToggleRoadClosed(JObject path, float smoothing = 0.6f)
    {
        JArray points = Array(path, "ControlPoints");
        if (points.Count < 2) return;
        bool repeated = (Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        bool closed = Int(path, "IsCircle") > 0 || repeated;
        if (closed)
        {
            path["IsCircle"] = 0;
            if (repeated && points.Count > 2) points.RemoveAt(points.Count - 1);
            if (smoothing >= 0f) SmoothRoad(path, smoothing);
        }
        else
        {
            if (points.Count < 3) throw new FormatException("Cần ít nhất 3 điểm để khép kín vòng.");
            path["IsCircle"] = 1;
            if (!repeated) points.Add(points[0].DeepClone());
            if (smoothing >= 0f) SmoothRoad(path, smoothing);
        }
    }

    public static void ToggleRoadClosed(JObject layout, JObject path, float smoothing = 0.6f)
    {
        ToggleRoadClosed(path, smoothing);
        if (layout != null) SyncEnterExitGates(layout);
    }

    public static void CenterRoad(JObject path, Vector3 targetCenter)
    {
        JArray points = Array(path, "ControlPoints");
        if (points.Count == 0) return;
        Vector3 sum = Vector3.zero;
        int count = 0;
        bool repeated = (Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        int limit = points.Count - (repeated ? 1 : 0);
        for (int i = 0; i < limit; i++)
        {
            sum += Vector((JObject)points[i], "Position");
            count++;
        }
        if (count == 0) return;
        Vector3 delta = targetCenter - (sum / count);
        delta.y = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var p = (JObject)points[i];
            p["Position"] = Vector(Vector(p, "Position") + delta);
            p["InTangent"] = Vector(Vector(p, "InTangent") + delta);
            p["OutTangent"] = Vector(Vector(p, "OutTangent") + delta);
        }
    }

    public static int InsertPointAfter(JObject path, int index, Vector3? targetPos = null, float smoothing = 0.6f)
    {
        JArray points = Array(path, "ControlPoints");
        if (points.Count >= 128) throw new FormatException("Đường đã đạt tối đa 128 điểm.");
        bool repeated = points.Count > 1 && (Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        int maxIndex = points.Count - (repeated ? 2 : 1);
        int targetIndex = Mathf.Clamp(index, 0, Mathf.Max(0, maxIndex));
        Vector3 pos;
        if (targetPos.HasValue)
        {
            pos = targetPos.Value;
        }
        else
        {
            Vector3 cur = Vector((JObject)points[targetIndex], "Position");
            Vector3 next = (targetIndex + 1 < points.Count) ? Vector((JObject)points[targetIndex + 1], "Position") : cur + Vector3.right * 2f;
            pos = (cur + next) * 0.5f;
        }
        int insertPos = targetIndex + 1;
        points.Insert(insertPos, Point(pos, pos, pos));
        if (repeated)
        {
            points[points.Count - 1] = points[0].DeepClone();
        }
        if (smoothing >= 0f) SmoothRoad(path, smoothing);
        return insertPos;
    }

    public static JObject UShapeRoad(Vector3 center, float width, float depth, float smoothing = 0.6f)
    {
        CheckRoadSize(width, depth);
        float hx = width * 0.5f, hz = depth * 0.5f;
        var list = new[]
        {
            center + new Vector3(-hx, 0.2f, hz),
            center + new Vector3(-hx, 0.2f, -hz),
            center + new Vector3(0, 0.2f, -hz),
            center + new Vector3(hx, 0.2f, -hz),
            center + new Vector3(hx, 0.2f, hz)
        };
        return RoadFromPositions(list, false, smoothing);
    }

    public static JObject StraightRoad(Vector3 start, Vector3 end)
    {
        var list = new[] { start, end };
        return RoadFromPositions(list, false, 0f);
    }

    public static JObject StraightRoadCentered(Vector3 center, float length, bool horizontal = true)
    {
        float half = length * 0.5f;
        Vector3 start = horizontal ? center + new Vector3(-half, 0.2f, 0) : center + new Vector3(0, 0.2f, half);
        Vector3 end = horizontal ? center + new Vector3(half, 0.2f, 0) : center + new Vector3(0, 0.2f, -half);
        return StraightRoad(start, end);
    }

    public static JObject SCurveRoad(Vector3 center, float width, float depth, float smoothing = 0.6f)
    {
        CheckRoadSize(width, depth);
        float hx = width * 0.5f, hz = depth * 0.5f;
        var list = new[]
        {
            center + new Vector3(-hx, 0.2f, hz),
            center + new Vector3(-hx * 0.4f, 0.2f, hz * 0.4f),
            center + new Vector3(0f, 0.2f, 0f),
            center + new Vector3(hx * 0.4f, 0.2f, -hz * 0.4f),
            center + new Vector3(hx, 0.2f, -hz)
        };
        return RoadFromPositions(list, false, smoothing);
    }

    public static JObject OpenWindingRoad(Vector3 center, float width, float depth, float smoothing = 0.6f)
    {
        CheckRoadSize(width, depth);
        float hx = width * 0.5f, hz = depth * 0.5f;
        var list = new[]
        {
            center + new Vector3(-hx, 0.2f, hz),
            center + new Vector3(-hx * 0.5f, 0.2f, 0f),
            center + new Vector3(0f, 0.2f, hz * 0.4f),
            center + new Vector3(hx * 0.5f, 0.2f, -hz * 0.4f),
            center + new Vector3(hx, 0.2f, 0f),
            center + new Vector3(hx, 0.2f, -hz)
        };
        return RoadFromPositions(list, false, smoothing);
    }

    public static void FitStraightRoadBesideBoxes(JObject layout, float clearance = 1.8f, bool onLeft = true)
    {
        JArray boxes = Array(layout, "Boxes");
        if (boxes.Count == 0) throw new InvalidOperationException("Thêm hộp trước khi tạo băng chuyền thẳng.");
        Bounds bounds = LoopLevelBoardLayout.BoardBounds(layout, false);
        float x = onLeft ? (bounds.min.x - clearance) : (bounds.max.x + clearance);
        float zStart = bounds.max.z + 1.2f;
        float zEnd = bounds.min.z - 1.2f;
        Vector3 p0 = new Vector3(x, 0.2f, zStart);
        Vector3 p1 = new Vector3(x, 0.2f, zEnd);
        layout["Paths"] = new JArray(StraightRoad(p0, p1));
        Bounds whole = LoopLevelBoardLayout.BoardBounds(layout, true);
        layout["Size"] = Mathf.Max(whole.size.x, whole.size.z) + 1;
        SyncEnterExitGates(layout);
    }

    public static void FitUShapeRoadAroundBoxes(JObject layout, float clearance = 1.2f)
    {
        JArray boxes = Array(layout, "Boxes");
        if (boxes.Count == 0) throw new InvalidOperationException("Thêm hộp trước khi tạo đường chữ U.");
        Bounds bounds = LoopLevelBoardLayout.BoardBounds(layout, false);
        float requiredClearance = clearance + 0.5f;
        float padding = requiredClearance + 0.05f;

        float leftX = bounds.min.x - padding;
        float rightX = bounds.max.x + padding;
        float bottomZ = bounds.min.z - padding;
        float topZ = bounds.max.z + 1.2f;

        float spanX = rightX - leftX;
        float cornerRatio = Mathf.Clamp(padding / Mathf.Max(1f, spanX), 0.15f, 0.35f);

        var list = new[]
        {
            new Vector3(leftX, 0.2f, topZ),
            new Vector3(leftX, 0.2f, bounds.min.z),
            new Vector3(leftX + spanX * cornerRatio, 0.2f, bottomZ),
            new Vector3(rightX - spanX * cornerRatio, 0.2f, bottomZ),
            new Vector3(rightX, 0.2f, bounds.min.z),
            new Vector3(rightX, 0.2f, topZ)
        };

        layout["Paths"] = new JArray(RoadFromPositions(list, false, 0.45f));
        Bounds whole = LoopLevelBoardLayout.BoardBounds(layout, true);
        layout["Size"] = Mathf.Max(whole.size.x, whole.size.z) + 1f;
        SyncEnterExitGates(layout);
    }

    public static void FitSCurveRoadBetweenBanks(JObject layout, float clearance = 1.0f)
    {
        JArray boxes = Array(layout, "Boxes");
        if (boxes.Count == 0) throw new InvalidOperationException("Thêm hộp trước khi tạo đường chữ S.");
        Bounds bounds = LoopLevelBoardLayout.BoardBounds(layout, false);
        float topZ = bounds.max.z + 1.5f;
        float bottomZ = bounds.min.z - 1.5f;

        float hx = Mathf.Clamp(bounds.size.x * 0.15f, 0.6f, 1.2f);
        var list = new[]
        {
            new Vector3(-hx, 0.2f, topZ),
            new Vector3(-hx * 1.1f, 0.2f, bounds.max.z * 0.5f),
            new Vector3(0f, 0.2f, 0f),
            new Vector3(hx * 1.1f, 0.2f, bounds.min.z * 0.5f),
            new Vector3(hx, 0.2f, bottomZ)
        };

        layout["Paths"] = new JArray(RoadFromPositions(list, false, 0.55f));
        Bounds whole = LoopLevelBoardLayout.BoardBounds(layout, true);
        layout["Size"] = Mathf.Max(whole.size.x, whole.size.z) + 1f;
        SyncEnterExitGates(layout);
    }

    public static void FitOpenWindingRoadAroundBoxes(JObject layout, float clearance = 1.2f)
    {
        JArray boxes = Array(layout, "Boxes");
        if (boxes.Count == 0) throw new InvalidOperationException("Thêm hộp trước khi tạo đường uốn lượn.");
        Bounds bounds = LoopLevelBoardLayout.BoardBounds(layout, false);
        float requiredClearance = clearance + 0.5f;
        float padding = requiredClearance + 0.05f;

        float leftX = bounds.min.x - padding;
        float rightX = bounds.max.x + padding;
        float bottomZ = bounds.min.z - padding;
        float topZ = bounds.max.z + 1.2f;

        float midZ = (bounds.min.z + bounds.max.z) * 0.5f;
        float wave = Mathf.Min(0.6f, padding * 0.35f);

        var list = new[]
        {
            new Vector3(leftX, 0.2f, topZ),
            new Vector3(leftX - wave, 0.2f, (topZ + midZ) * 0.5f),
            new Vector3(leftX, 0.2f, midZ),
            new Vector3(leftX - wave, 0.2f, (midZ + bottomZ) * 0.5f),
            new Vector3(leftX, 0.2f, bounds.min.z),
            new Vector3((leftX + rightX) * 0.5f, 0.2f, bottomZ - wave),
            new Vector3(rightX, 0.2f, bounds.min.z),
            new Vector3(rightX + wave, 0.2f, (midZ + bottomZ) * 0.5f),
            new Vector3(rightX, 0.2f, midZ),
            new Vector3(rightX + wave, 0.2f, (topZ + midZ) * 0.5f),
            new Vector3(rightX, 0.2f, topZ)
        };

        layout["Paths"] = new JArray(RoadFromPositions(list, false, 0.5f));
        Bounds whole = LoopLevelBoardLayout.BoardBounds(layout, true);
        layout["Size"] = Mathf.Max(whole.size.x, whole.size.z) + 1f;
        SyncEnterExitGates(layout);
    }

    public static void SyncEnterExitGates(JObject layout)
    {
        if (layout == null) return;
        JArray paths = Array(layout, "Paths");
        if (paths.Count == 0) return;
        JObject road = (JObject)paths[0];
        JArray points = Array(road, "ControlPoints");
        if (points.Count < 2) return;
        bool isCircle = Int(road, "IsCircle") > 0;
        JArray map = layout["MapData"] as JArray;
        if (map == null)
        {
            map = new JArray();
            layout["MapData"] = map;
        }

        if (isCircle)
        {
            for (int i = map.Count - 1; i >= 0; i--)
            {
                string name = map[i]?.Value<string>("Name");
                if (string.Equals(name, "Enter", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "MapEnter", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Exit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "MapExit", StringComparison.OrdinalIgnoreCase))
                {
                    map.RemoveAt(i);
                }
            }
            return;
        }

        Vector3 p0 = Vector((JObject)points[0], "Position");
        Vector3 p1 = Vector((JObject)points[1], "Position");
        Vector3 pEnd = Vector((JObject)points[points.Count - 1], "Position");
        Vector3 pPrev = Vector((JObject)points[points.Count - 2], "Position");

        Vector3 startDir = p1 - p0;
        startDir.y = 0;
        if (startDir.sqrMagnitude < 0.0001f) startDir = Vector3.forward;
        else startDir.Normalize();

        Vector3 endDir = pEnd - pPrev;
        endDir.y = 0;
        if (endDir.sqrMagnitude < 0.0001f) endDir = Vector3.forward;
        else endDir.Normalize();

        float enterYaw = (Mathf.Atan2(startDir.x, startDir.z) * Mathf.Rad2Deg + 90f + 360f) % 360f;
        float exitYaw = (Mathf.Atan2(endDir.x, endDir.z) * Mathf.Rad2Deg - 90f + 360f) % 360f;

        Vector3 enterPos = new Vector3(p0.x, 0f, p0.z);
        Vector3 exitPos = new Vector3(pEnd.x, 0f, pEnd.z);

        JObject enterItem = null;
        JObject exitItem = null;
        for (int i = 0; i < map.Count; i++)
        {
            string name = map[i]?.Value<string>("Name");
            if (enterItem == null && (string.Equals(name, "Enter", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "MapEnter", StringComparison.OrdinalIgnoreCase)))
            {
                enterItem = (JObject)map[i];
            }
            else if (exitItem == null && (string.Equals(name, "Exit", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "MapExit", StringComparison.OrdinalIgnoreCase)))
            {
                exitItem = (JObject)map[i];
            }
        }

        if (enterItem == null)
        {
            enterItem = new JObject
            {
                ["Name"] = "Enter",
                ["Position"] = Vector(enterPos),
                ["Rotation"] = Vector(new Vector3(0, enterYaw, 0)),
                ["Scale"] = Vector(Vector3.one)
            };
            map.Add(enterItem);
        }
        else
        {
            enterItem["Position"] = Vector(enterPos);
            enterItem["Rotation"] = Vector(new Vector3(0, enterYaw, 0));
        }

        if (exitItem == null)
        {
            exitItem = new JObject
            {
                ["Name"] = "Exit",
                ["Position"] = Vector(exitPos),
                ["Rotation"] = Vector(new Vector3(0, exitYaw, 0)),
                ["Scale"] = Vector(Vector3.one)
            };
            map.Add(exitItem);
        }
        else
        {
            exitItem["Position"] = Vector(exitPos);
            exitItem["Rotation"] = Vector(new Vector3(0, exitYaw, 0));
        }
    }

    public static JObject HourglassRoad(Vector3 center, float width, float depth, float waist = 1.8f, float smoothing = 0.6f)
    {
        CheckRoadSize(width, depth);
        float hx = width * 0.5f, hz = depth * 0.5f;
        float hw = waist * 0.5f;
        var list = new[]
        {
            center + new Vector3(-hw, 0.2f, 0),
            center + new Vector3(-hx, 0.2f, hz * 0.5f),
            center + new Vector3(0, 0.2f, hz),
            center + new Vector3(hx, 0.2f, hz * 0.5f),
            center + new Vector3(hw, 0.2f, 0),
            center + new Vector3(hx, 0.2f, -hz * 0.5f),
            center + new Vector3(0, 0.2f, -hz),
            center + new Vector3(-hx, 0.2f, -hz * 0.5f)
        };
        return RoadFromPositions(list, true, smoothing);
    }

    public static JObject Figure8OverpassRoad(Vector3 center, float width, float depth, float overpassHeight = 1.8f, float smoothing = 0.6f)
    {
        CheckRoadSize(width, depth);
        float hx = width * 0.5f, hz = depth * 0.5f;
        float ground = center.y + 0.2f;
        float peak = ground + overpassHeight;
        float ramp = ground + overpassHeight * 0.5f;
        var list = new[]
        {
            center + new Vector3(0, ground, 0),
            center + new Vector3(-hx, ground, hz * 0.5f),
            center + new Vector3(0, ground, hz),
            center + new Vector3(hx, ramp, hz * 0.5f),
            center + new Vector3(0, peak, 0),
            center + new Vector3(-hx, ramp, -hz * 0.5f),
            center + new Vector3(0, ground, -hz),
            center + new Vector3(hx, ground, -hz * 0.5f)
        };
        return RoadFromPositions(list, true, smoothing);
    }

    public static JObject Figure8Road(Vector3 center, float width, float depth, float smoothing = 0.6f)
    {
        CheckRoadSize(width, depth);
        float hx = width * 0.5f, hz = depth * 0.5f;
        var list = new[]
        {
            center + new Vector3(0, 0.2f, 0),
            center + new Vector3(-hx, 0.2f, hz * 0.5f),
            center + new Vector3(0, 0.2f, hz),
            center + new Vector3(hx, 0.2f, hz * 0.5f),
            center + new Vector3(0, 0.2f, 0),
            center + new Vector3(-hx, 0.2f, -hz * 0.5f),
            center + new Vector3(0, 0.2f, -hz),
            center + new Vector3(hx, 0.2f, -hz * 0.5f)
        };
        return RoadFromPositions(list, true, smoothing);
    }

    public static bool SegmentsIntersect2D(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 hit)
    {
        hit = Vector2.zero;
        float d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);
        if (Mathf.Abs(d) < 0.00001f) return false;
        float u = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / d;
        float v = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / d;
        if (u > 0.05f && u < 0.95f && v > 0.05f && v < 0.95f)
        {
            hit = p1 + u * (p2 - p1);
            return true;
        }
        return false;
    }

    public static List<Vector3> FindSelfIntersections(Vector3[] sampled, float yThreshold = 0.6f)
    {
        var result = new List<Vector3>();
        if (sampled == null || sampled.Length < 10) return result;
        int count = sampled.Length;
        for (int i = 0; i < count - 1; i += 2)
        {
            Vector2 p1 = new Vector2(sampled[i].x, sampled[i].z);
            Vector2 p2 = new Vector2(sampled[i + 1].x, sampled[i + 1].z);
            for (int j = i + 4; j < count - 1; j += 2)
            {
                if (i == 0 && j >= count - 4) continue;
                Vector2 p3 = new Vector2(sampled[j].x, sampled[j].z);
                Vector2 p4 = new Vector2(sampled[j + 1].x, sampled[j + 1].z);
                if (SegmentsIntersect2D(p1, p2, p3, p4, out Vector2 hit2D))
                {
                    float y1 = Mathf.Lerp(sampled[i].y, sampled[i + 1].y, 0.5f);
                    float y2 = Mathf.Lerp(sampled[j].y, sampled[j + 1].y, 0.5f);
                    if (Mathf.Abs(y1 - y2) < yThreshold)
                    {
                        var pt = new Vector3(hit2D.x, (y1 + y2) * 0.5f, hit2D.y);
                        if (!result.Any(r => (new Vector2(r.x, r.z) - hit2D).sqrMagnitude < 0.5f))
                            result.Add(pt);
                    }
                }
            }
        }
        return result;
    }

    public static void Make3DOverpass(JObject path, float overpassHeight = 1.8f)
    {
        JArray points = Array(path, "ControlPoints");
        if (points.Count < 4) return;
        Vector3[] sampled = LoopLevelBoardLayout.SampleRoad(path);
        List<Vector3> hits = FindSelfIntersections(sampled);
        if (hits.Count == 0) return;
        Vector3 hit = hits[0];
        
        int bestIdx = -1; float bestDist = float.PositiveInfinity;
        bool repeated = (Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        int count = points.Count - (repeated ? 1 : 0);
        int half = count / 2;
        for (int i = half; i < count; i++)
        {
            Vector3 pos = Vector((JObject)points[i], "Position");
            float d = (new Vector2(pos.x, pos.z) - new Vector2(hit.x, hit.z)).sqrMagnitude;
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }
        if (bestIdx < 0) return;

        var peakPt = (JObject)points[bestIdx];
        Vector3 peakPos = Vector(peakPt, "Position");
        float baseY = peakPos.y;
        peakPos.y = baseY + overpassHeight;
        MovePoint(points, bestIdx, peakPos);

        int prevIdx = (bestIdx - 1 + count) % count;
        int nextIdx = (bestIdx + 1) % count;
        var prevPt = (JObject)points[prevIdx];
        var nextPt = (JObject)points[nextIdx];
        Vector3 prevPos = Vector(prevPt, "Position");
        Vector3 nextPos = Vector(nextPt, "Position");
        prevPos.y = baseY + overpassHeight * 0.5f;
        nextPos.y = baseY + overpassHeight * 0.5f;
        MovePoint(points, prevIdx, prevPos);
        MovePoint(points, nextIdx, nextPos);
        SmoothRoad(path, 0.6f);
    }

    public static void UntangleRoad(JObject path, float separation = 1.8f)
    {
        JArray points = Array(path, "ControlPoints");
        if (points.Count < 4) return;
        Vector3[] sampled = LoopLevelBoardLayout.SampleRoad(path);
        List<Vector3> hits = FindSelfIntersections(sampled);
        if (hits.Count == 0) return;
        Vector3 hit = hits[0];

        bool repeated = (Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        int count = points.Count - (repeated ? 1 : 0);
        
        int p1 = -1, p2 = -1; float d1 = float.PositiveInfinity, d2 = float.PositiveInfinity;
        for (int i = 0; i < count / 2; i++)
        {
            float d = (new Vector2(Vector((JObject)points[i], "Position").x, Vector((JObject)points[i], "Position").z) - new Vector2(hit.x, hit.z)).sqrMagnitude;
            if (d < d1) { d1 = d; p1 = i; }
        }
        for (int i = count / 2; i < count; i++)
        {
            float d = (new Vector2(Vector((JObject)points[i], "Position").x, Vector((JObject)points[i], "Position").z) - new Vector2(hit.x, hit.z)).sqrMagnitude;
            if (d < d2) { d2 = d; p2 = i; }
        }

        if (p1 >= 0 && p2 >= 0)
        {
            Vector3 pos1 = Vector((JObject)points[p1], "Position");
            Vector3 pos2 = Vector((JObject)points[p2], "Position");
            pos1.x -= separation * 0.5f;
            pos2.x += separation * 0.5f;
            MovePoint(points, p1, pos1);
            MovePoint(points, p2, pos2);
            SmoothRoad(path, 0.6f);
        }
    }

    public static void CheckRoadSize(float width, float depth)
    {
        if (float.IsNaN(width) || float.IsInfinity(width) || float.IsNaN(depth) || float.IsInfinity(depth) || width <= 0 || depth <= 0)
            throw new FormatException("Chiều rộng và sâu của đường phải là số hữu hạn lớn hơn 0.");
    }

    public static void SetTangent(JArray points, int index, string field, Vector3 position)
    {
        if (field != "InTangent" && field != "OutTangent") throw new ArgumentException("Trường tiếp tuyến không hợp lệ.");
        ((JObject)points[index])[field] = Vector(position);
        if (points.Count < 2 || (index != 0 && index != points.Count - 1)) return;
        if ((Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f)
            ((JObject)points[index == 0 ? points.Count - 1 : 0])[field] = Vector(position);
    }

    public static void MovePoint(JArray points, int index, Vector3 position)
    {
        var point = (JObject)points[index];
        Vector3 old = Vector(point, "Position"), delta = position - old;
        point["Position"] = Vector(position);
        point["InTangent"] = Vector(Vector(point, "InTangent") + delta);
        point["OutTangent"] = Vector(Vector(point, "OutTangent") + delta);
        int counterpart = index == 0 ? points.Count - 1 : index == points.Count - 1 ? 0 : -1;
        if (counterpart >= 0 && counterpart != index &&
            (Vector((JObject)points[counterpart], "Position") - old).sqrMagnitude < 0.000001f)
        {
            var seam = (JObject)points[counterpart];
            seam["Position"] = Vector(position);
            seam["InTangent"] = Vector(Vector(seam, "InTangent") + delta);
            seam["OutTangent"] = Vector(Vector(seam, "OutTangent") + delta);
        }
    }

    public static int SplitSegment(JArray points, int index, float progress = .5f)
    {
        if (index < 0 || index >= points.Count - 1) throw new ArgumentOutOfRangeException(nameof(index));
        var a = (JObject)points[index]; var b = (JObject)points[index + 1];
        Vector3 p0 = Vector(a, "Position"), p1 = Vector(a, "OutTangent"),
            p2 = Vector(b, "InTangent"), p3 = Vector(b, "Position");
        float t = Mathf.Clamp(progress, .001f, .999f);
        Vector3 q0 = Vector3.Lerp(p0, p1, t), q1 = Vector3.Lerp(p1, p2, t), q2 = Vector3.Lerp(p2, p3, t);
        Vector3 r0 = Vector3.Lerp(q0, q1, t), r1 = Vector3.Lerp(q1, q2, t);
        a["OutTangent"] = Vector(q0); b["InTangent"] = Vector(q2);
        points.Insert(index + 1, Point(Vector3.Lerp(r0, r1, t), r0, r1));
        return index + 1;
    }

    public static void RemoveBox(JObject root, int index)
    {
        JObject removedBox = (JObject)Array(Layout(root), "Boxes")[index];
        foreach (JObject cube in Array(removedBox, "Cubes"))
            LoopLinkedCubeAuthoring.Unlink(root, Int(cube, "LinkedPairId"));
        JObject layout = Layout(root); Array(layout, "Boxes").RemoveAt(index);
        foreach (string key in new[] { "Groups", "Covers" })
        {
            var entries = Array(layout, key);
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = (JObject)entries[i]; RemapIndices(Array(entry, "BoxIndexes"), index);
                if (Array(entry, "BoxIndexes").Count == 0) entries.RemoveAt(i);
            }
        }
    }
    public static void RemoveCube(JObject box, int index)
    {
        LoopLinkedCubeAuthoring.BeforeDeleteCube(box, index);
        Array(box, "Cubes").RemoveAt(index);
        var links = Array(box, "LinkedCubes");
        for (int i = links.Count - 1; i >= 0; i--)
        {
            if (!(links[i] is JArray link)) continue;
            RemapIndices(link, index); if (link.Count < 2) links.RemoveAt(i);
        }
    }
    public static void SwapCubes(JObject box, int first, int second)
    {
        JArray cubes = Array(box, "Cubes");
        JToken copy = cubes[first].DeepClone(); cubes[first] = cubes[second].DeepClone(); cubes[second] = copy;
        foreach (JArray link in Array(box, "LinkedCubes").OfType<JArray>())
            for (int i = 0; i < link.Count; i++)
            {
                int value = link[i].Value<int>();
                if (value == first) link[i] = second;
                else if (value == second) link[i] = first;
            }
    }
    private static void RemapIndices(JArray indices, int deleted)
    {
        for (int i = indices.Count - 1; i >= 0; i--)
        {
            int value = indices[i].Value<int>();
            if (value == deleted) indices.RemoveAt(i);
            else if (value > deleted) indices[i] = value - 1;
        }
    }

    public static List<Vector3> Slots(JObject root)
    {
        Puzzle puzzle = ExportedPuzzleJsonDecoder.Decode(root.ToString(Formatting.None));
        var result = new List<Vector3>();
        if (!(puzzle.PathSectionDistance > 0f)) return result;
        foreach (GamePath path in puzzle.Paths)
        {
            var sampled = GameUtils.GetEquidistantPoints(path.ControlPoints, 50);
            float length = 0f;
            for (int i = 1; i < sampled.Count; i++) length += Vector3.Distance(sampled[i - 1], sampled[i]);
            double expected = length / (double)puzzle.PathSectionDistance;
            if (expected > MaximumSlots || result.Count + expected > MaximumSlots)
                throw new FormatException("Đường tạo hơn 50.000 ô. Tăng khoảng cách giữa các ô.");
            int count = (int)(length / puzzle.PathSectionDistance);
            if (count == 1 && sampled.Count > 0) result.Add(sampled[0]);
            else if (count > 1 && sampled.Count > 1)
                result.AddRange(GameUtils.ResamplePointsOptimized(sampled, count));
        }
        if (puzzle.Paths.Count == 1 && puzzle.Paths[0].ControlPoints.Count > 0)
        {
            var path = puzzle.Paths[0]; var points = path.ControlPoints;
            if ((path.IsCircle > 0 || Vector3.Distance(points[0].Position, points[points.Count - 1].Position) < 0.01f)
                && result.Count > 0) result.RemoveAt(result.Count - 1);
        }
        return result;
    }
    public static int NearestSlot(JObject box, IReadOnlyList<Vector3> slots)
    {
        if (slots.Count == 0) return 0;
        float offset = Int(box, "BoxType") == 1 ? -0.2f : 2.2f;
        Vector3 exit = Vector(box, "Position") + Quaternion.Euler(Vector(box, "Rotation")) * new Vector3(0, 0, offset);
        int best = 0; float distance = float.MaxValue;
        for (int i = 0; i < slots.Count; i++)
        {
            float dx = slots[i].x - exit.x, dz = slots[i].z - exit.z;
            float candidate = dx * dx + dz * dz;
            if (candidate < distance) { distance = candidate; best = i; }
        }
        return best + 1;
    }

    public static List<string> Validate(JObject root, out List<string> warnings)
    {
        var errors = new List<string>(); warnings = new List<string>();
        try
        {
            if (Int(root, "level_number") < 1) errors.Add("level_number phải lớn hơn 0.");
            if (Int(root, "puzzle_id") < 1) errors.Add("puzzle_id phải lớn hơn 0.");
            JObject layout = Layout(root);
            if (layout == null) { errors.Add("Thiếu đối tượng layout."); return errors; }
            CheckFinite(root, errors, "level");
            foreach (string key in new[] { "Boxes", "Paths" })
                if (!(layout[key] is JArray)) errors.Add("layout." + key + " phải là mảng.");
            foreach (string key in new[] { "Groups", "Covers", "SurpriseDatas" })
                if (layout[key] != null && layout[key].Type != JTokenType.Null && !(layout[key] is JArray))
                    errors.Add("layout." + key + " phải là mảng hoặc null.");
            if (errors.Count > 0) return errors;
            if (Int(layout, "Version", Puzzle.LegacyVersion) < 0 || Int(layout, "Version", Puzzle.LegacyVersion) > 12) errors.Add("Version phải từ 0–12 (0 dùng giá trị mặc định cũ).");
            if (Int(layout, "HardLevel") < 0 || Int(layout, "HardLevel") > 2) errors.Add("HardLevel phải từ 0–2.");
            if (Float(layout, "Size", 10f) <= 0f) errors.Add("Size phải lớn hơn 0.");
            if (Int(layout, "PathLength") < 1) errors.Add("PathLength (sức chứa theo nhóm) phải lớn hơn 0.");
            if (Float(layout, "PathSectionDistance", 0.2f) < 0f) errors.Add("PathSectionDistance không được âm.");
            if (Float(layout, "PathSectionDistance", 0.2f) == 0f) warnings.Add("PathSectionDistance bằng 0 dùng mặc định 0,2 khi chạy game.");
            JArray paths = Array(layout, "Paths"), boxes = Array(layout, "Boxes");
            if (paths.Count == 0) errors.Add("Thêm ít nhất một đường.");
            foreach (JObject path in paths)
            {
                var points = path["ControlPoints"] as JArray;
                if (points == null || points.Count < 2) { errors.Add("Mỗi đường cần ít nhất hai điểm."); continue; }
                if (points.Count > 128) errors.Add("Mỗi đường có tối đa 128 điểm trong trình sửa này.");
                foreach (JObject point in points)
                    foreach (string key in new[] { "Position", "InTangent", "OutTangent" }) CheckVector(point, key, errors);
                if (Int(path, "IsCircle") == 0)
                {
                    JArray map = Array(layout, "MapData");
                    bool hasEnter = false;
                    bool hasExit = false;
                    for (int m = 0; m < map.Count; m++)
                    {
                        if (map[m] is JObject item)
                        {
                            string name = item.Value<string>("Name");
                            if (string.Equals(name, "Enter", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(name, "MapEnter", StringComparison.OrdinalIgnoreCase))
                            {
                                hasEnter = true;
                            }
                            else if (string.Equals(name, "Exit", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(name, "MapExit", StringComparison.OrdinalIgnoreCase))
                            {
                                hasExit = true;
                            }
                        }
                    }
                    if (!hasEnter || !hasExit)
                    {
                        warnings.Add("Đường mở (IsCircle=0) đang thiếu Cổng Vào (Enter) hoặc Cổng Ra (Exit). Hãy bấm 'Cập nhật Cổng' để tự thêm.");
                    }
                }
            }
            foreach (JObject box in boxes)
            {
                int index = boxes.IndexOf(box), type = Int(box, "BoxType");
                if (type < 0 || type > 2) errors.Add("Hộp " + index + ": BoxType không hợp lệ.");
                CheckVector(box, "Position", errors); CheckVector(box, "Rotation", errors);
                var cubes = box["Cubes"] as JArray;
                if (cubes == null) { errors.Add("Hộp " + index + ": Cubes phải là mảng."); continue; }
                int capacity = type == 2 ? 6 : 4;
                if (type != 1 && cubes.Count > capacity) errors.Add("Hộp " + index + ": vượt sức chứa " + capacity + ".");
                if (type == 1 && cubes.Count > 64) errors.Add("Hộp " + index + ": hàng đợi hộp đơn vượt 64 khối.");
                foreach (JObject cube in cubes)
                {
                    if (Int(cube, "Type") <= 0) errors.Add("Hộp " + index + ": Type của khối phải lớn hơn 0.");
                    if (Int(cube, "NumLock") < 0 || Int(cube, "GroupKeyId") < 0) errors.Add("Khóa/chìa khóa của khối không được âm.");
                }
                foreach (string key in new[] { "PathIndexV2", "NumLock", "LockColor", "LimitedColor" })
                    if (Int(box, key) < 0) errors.Add("Hộp " + index + ": " + key + " không được âm.");
                if (box["LinkedCubes"] is JArray links)
                    foreach (JArray link in links)
                        foreach (JToken value in link)
                            if (value.Value<int>() < 0 || value.Value<int>() >= cubes.Count) errors.Add("Hộp " + index + ": chỉ số LinkedCubes không hợp lệ.");
            }
            if (boxes.Count == 0) errors.Add("Thêm ít nhất một hộp.");
            var groupIds = new HashSet<int>();
            foreach (JObject group in Array(layout, "Groups"))
                if (Int(group, "Id") <= 0 || !groupIds.Add(Int(group, "Id"))) errors.Add("ID nhóm phải lớn hơn 0 và không trùng nhau.");
            var membership = new HashSet<int>();
            foreach (JObject group in Array(layout, "Groups"))
                foreach (JToken index in Array(group, "BoxIndexes"))
                    if (!membership.Add(index.Value<int>())) errors.Add("Hộp " + index + ": thuộc nhiều nhóm khóa.");
            foreach (JObject box in boxes)
                foreach (JObject cube in Array(box, "Cubes"))
                    if (Int(cube, "GroupKeyId") > 0 && !groupIds.Contains(Int(cube, "GroupKeyId")))
                        errors.Add("Hộp " + boxes.IndexOf(box) + ": chìa khóa trỏ tới nhóm không tồn tại.");
            foreach (string key in new[] { "Groups", "Covers" })
                foreach (JObject entry in Array(layout, key))
                {
                    var indices = entry["BoxIndexes"] as JArray;
                    if (indices == null || indices.Count == 0) { errors.Add(key + ": BoxIndexes trống."); continue; }
                    var seen = new HashSet<int>();
                    foreach (JToken token in indices)
                    {
                        int i = token.Value<int>();
                        if (i < 0 || i >= boxes.Count || !seen.Add(i)) errors.Add(key + ": chỉ số hộp không hợp lệ hoặc bị trùng.");
                    }
                }
            if (errors.Count == 0)
            {
                var slots = Slots(root);
                if (slots.Count == 0) errors.Add("Đường không có ô băng chuyền.");
                foreach (JObject box in boxes)
                    if (Int(box, "PathIndexV2") > slots.Count) errors.Add("Hộp " + boxes.IndexOf(box) + ": PathIndexV2 nằm ngoài các ô băng chuyền.");
            }
            if (boxes.All(box => ((JObject)box)["Cubes"] is JArray cubes && cubes.Count == 0)) warnings.Add("Mọi hộp đều trống.");
            warnings.Add("Kiểm tra cấu trúc không đảm bảo màn chơi có lời giải. Hãy chơi thử trong Play Mode.");
            Puzzle decoded = ExportedPuzzleJsonDecoder.Decode(root.ToString(Formatting.None));
            LinkedCubePair.Validate(decoded);
            if (decoded.Boxes.Any(box => box.Cubes.Any(cube => cube.LinkedPairId > 0)))
            {
                if (errors.Count == 0)
                {
                    // Resolve automatic entrances using the same path builder as gameplay.
                    // DecodeDetached restores GameData.Current even when validation throws.
                    GameData preview = LoopLevelGenerator.DecodeDetached(root);
                    LinkedCubePair pair = preview.LinkedPair;
                    if (preview.PathPoints.Count < 2 || pair.FirstBox.PathIndex < 0 || pair.SecondBox.PathIndex < 0 ||
                        pair.FirstBox.PathIndex >= preview.PathPoints.Count || pair.SecondBox.PathIndex >= preview.PathPoints.Count)
                        errors.Add("Cube đôi cần hai điểm vào băng chuyền hợp lệ.");
                }
                warnings.Add("Cube đôi: kiểm tra hai khay kề nhau trong Game View; dây phải rõ và không xuyên khay thứ ba. Validator chưa kiểm tra hình học này.");
            }
        }
        catch (Exception exception) { errors.Add("Dữ liệu không hợp lệ: " + exception.Message); }
        return errors;
    }
    private static void CheckVector(JObject obj, string key, List<string> errors)
    {
        var value = obj[key] as JArray;
        if (value == null || value.Count != 3 || value.Any(v => v.Type != JTokenType.Float && v.Type != JTokenType.Integer))
            errors.Add(key + " phải chứa ba số.");
    }
    private static void CheckFinite(JToken token, List<string> errors, string path)
    {
        if (token.Type == JTokenType.Float)
        {
            double value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 1000000d)
                errors.Add(path + ": số không hữu hạn hoặc quá lớn.");
        }
        foreach (JToken child in token.Children()) CheckFinite(child, errors, path + "." + child.Path);
    }

    public static void WriteFile(string path, string json)
    {
        JObject root = Parse(json); List<string> errors = Validate(root, out _);
        if (errors.Count != 0) throw new FormatException(string.Join("\n", errors));
        string target = Path.GetFullPath(path), temporary = target + ".leveleditor.tmp";
        string parent = Path.GetDirectoryName(target);
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
        try
        {
            File.WriteAllText(temporary, root.ToString(Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static Color ColorFor(int type)
    {
        Color[] colors = { new Color(.9f,.2f,.2f), new Color(.18f,.45f,1f), new Color(1f,.85f,.15f),
            new Color(.25f,.8f,.3f), new Color(.6f,.25f,.9f), new Color(1f,.5f,.1f),
            new Color(1f,.4f,.7f), new Color(.05f,.4f,.25f), new Color(.5f,.3f,.15f),
            new Color(.1f,.8f,.8f), Color.white, Color.gray, new Color(.5f,.8f,1f), new Color(.15f,.15f,.15f) };
        int ordinary = type >= 201 ? type - 200 : type >= 101 ? type - 100 : type;
        return ordinary > 0 && ordinary <= colors.Length ? colors[ordinary - 1] : Color.gray;
    }
}
#endif
