#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Detached JSON authoring. Never constructs GameData or changes the live board.</summary>
public static class LoopLevelAuthoring
{
    public const string LevelFolder = "Assets/00Game/05LevelData/LevelData";
    public const int MaximumSlots = 50000;

    public static JObject Parse(string json)
    {
        var root = JObject.Parse(json);
        if (!(root["layout"] is JObject))
            throw new FormatException("Expected a level object with a 'layout' object.");
        return root;
    }

    public static JObject Layout(JObject root) => (JObject)root["layout"];
    public static JArray Array(JObject obj, string key)
    {
        if (obj[key] is JArray array) return array;
        // Reading a legacy null/absent list must not change its JSON representation.
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
                ["MapData"] = new JArray(), ["Groups"] = new JArray(),
                ["Covers"] = new JArray(), ["SurpriseDatas"] = new JArray() } };
        return root;
    }

    public static JObject NewBox(Vector3 position) => new JObject {
        ["BoxType"] = 0, ["PathIndex"] = 0, ["PathIndexV2"] = 0,
        ["Position"] = Vector(position), ["Rotation"] = Vector(Vector3.zero),
        ["Cubes"] = new JArray(), ["LinkedCubes"] = new JArray(), ["LockColor"] = 0,
        ["IsEnterOnly"] = 0, ["NumLock"] = 0, ["LimitedColor"] = 0,
        ["IsAdjacentLocked"] = 0, ["MultiColorLock"] = JValue.CreateNull() };

    public static JObject NewCube(int color) => new JObject { ["Type"] = color,
        ["IsSecret"] = 0, ["GroupKeyId"] = 0, ["NumLock"] = 0, ["IsFixed"] = 0 };
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
        // Store an exact seam, rather than relying on floating-point sin(2*pi).
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
        if (points.Count < (closed ? 3 : 2)) throw new FormatException(closed ? "A loop needs at least three distinct points." : "A road needs at least two distinct points.");
        if (points.Count > (closed ? 127 : 128)) throw new FormatException("A road may have at most 128 control points, including its closing point.");
        var path = new JObject { ["IsCircle"] = closed ? 1 : 0, ["ControlPoints"] = points };
        SmoothRoad(path, smoothing);
        return path;
    }

    /// <summary>Generate aligned, bounded handles from anchor positions; preserve custom JSON fields.</summary>
    public static void SmoothRoad(JObject path, float smoothing)
    {
        JArray points = Array(path, "ControlPoints");
        if (points.Count < 2) return;
        bool repeated = (Vector((JObject)points[0], "Position") - Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        bool closed = Int(path, "IsCircle") > 0 || repeated;
        if (closed && !repeated)
        {
            if (points.Count >= 128) throw new FormatException("Remove a point before closing this road (128 point limit).");
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
    private static void CheckRoadSize(float width, float depth)
    {
        if (float.IsNaN(width) || float.IsInfinity(width) || float.IsNaN(depth) || float.IsInfinity(depth) || width <= 0 || depth <= 0)
            throw new FormatException("Road width and depth must be positive, finite numbers.");
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

    // De Casteljau subdivision preserves the curve exactly when inserting a point.
    public static int SplitSegment(JArray points, int index)
    {
        if (index < 0 || index >= points.Count - 1) throw new ArgumentOutOfRangeException(nameof(index));
        var a = (JObject)points[index]; var b = (JObject)points[index + 1];
        Vector3 p0 = Vector(a, "Position"), p1 = Vector(a, "OutTangent"),
            p2 = Vector(b, "InTangent"), p3 = Vector(b, "Position");
        Vector3 q0 = (p0 + p1) * 0.5f, q1 = (p1 + p2) * 0.5f, q2 = (p2 + p3) * 0.5f;
        Vector3 r0 = (q0 + q1) * 0.5f, r1 = (q1 + q2) * 0.5f;
        a["OutTangent"] = Vector(q0); b["InTangent"] = Vector(q2);
        points.Insert(index + 1, Point((r0 + r1) * 0.5f, r0, r1));
        return index + 1;
    }

    public static void RemoveBox(JObject root, int index)
    {
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
                throw new FormatException("Path produces more than 50,000 conveyor slots. Increase section distance.");
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
        return best + 1; // Native JSON binding is one-based; zero means find nearest at runtime.
    }

    public static List<string> Validate(JObject root, out List<string> warnings)
    {
        var errors = new List<string>(); warnings = new List<string>();
        try
        {
            if (Int(root, "level_number") < 1) errors.Add("level_number must be positive.");
            if (Int(root, "puzzle_id") < 1) errors.Add("puzzle_id must be positive.");
            JObject layout = Layout(root);
            if (layout == null) { errors.Add("Missing layout object."); return errors; }
            CheckFinite(root, errors, "level");
            foreach (string key in new[] { "Boxes", "Paths" })
                if (!(layout[key] is JArray)) errors.Add("layout." + key + " must be an array.");
            foreach (string key in new[] { "MapData", "Groups", "Covers", "SurpriseDatas" })
                if (layout[key] != null && layout[key].Type != JTokenType.Null && !(layout[key] is JArray))
                    errors.Add("layout." + key + " must be an array or null.");
            if (errors.Count > 0) return errors;
            if (Int(layout, "Version", Puzzle.LegacyVersion) < 0 || Int(layout, "Version", Puzzle.LegacyVersion) > 12) errors.Add("Version must be 0–12 (0 uses the legacy default).");
            if (Int(layout, "HardLevel") < 0 || Int(layout, "HardLevel") > 2) errors.Add("HardLevel must be 0–2.");
            if (Float(layout, "Size", 10f) <= 0f) errors.Add("Size must be positive.");
            if (Int(layout, "PathLength") < 1) errors.Add("PathLength (capacity groups) must be positive.");
            if (Float(layout, "PathSectionDistance", 0.2f) < 0f) errors.Add("PathSectionDistance cannot be negative.");
            if (Float(layout, "PathSectionDistance", 0.2f) == 0f) warnings.Add("PathSectionDistance 0 uses the runtime default 0.2.");
            JArray paths = Array(layout, "Paths"), boxes = Array(layout, "Boxes");
            if (paths.Count == 0) errors.Add("Add at least one path.");
            foreach (JObject path in paths)
            {
                var points = path["ControlPoints"] as JArray;
                if (points == null || points.Count < 2) { errors.Add("Each path needs at least two points."); continue; }
                if (points.Count > 128) errors.Add("A path may have at most 128 points in this editor.");
                foreach (JObject point in points)
                    foreach (string key in new[] { "Position", "InTangent", "OutTangent" }) CheckVector(point, key, errors);
            }
            foreach (JObject box in boxes)
            {
                int index = boxes.IndexOf(box), type = Int(box, "BoxType");
                if (type < 0 || type > 2) errors.Add("Box " + index + ": invalid BoxType.");
                CheckVector(box, "Position", errors); CheckVector(box, "Rotation", errors);
                var cubes = box["Cubes"] as JArray;
                if (cubes == null) { errors.Add("Box " + index + ": Cubes must be an array."); continue; }
                // Single boxes can contain several queued cubes; only one pops per click.
                int capacity = type == 2 ? 6 : 4;
                if (type != 1 && cubes.Count > capacity) errors.Add("Box " + index + ": exceeds capacity " + capacity + ".");
                if (type == 1 && cubes.Count > 64) errors.Add("Box " + index + ": Single queue exceeds 64 entries.");
                foreach (JObject cube in cubes)
                {
                    if (Int(cube, "Type") <= 0) errors.Add("Box " + index + ": cube Type must be positive.");
                    if (Int(cube, "NumLock") < 0 || Int(cube, "GroupKeyId") < 0) errors.Add("Cube lock/key cannot be negative.");
                }
                foreach (string key in new[] { "PathIndexV2", "NumLock", "LockColor", "LimitedColor" })
                    if (Int(box, key) < 0) errors.Add("Box " + index + ": " + key + " cannot be negative.");
                if (box["LinkedCubes"] is JArray links)
                    foreach (JArray link in links)
                        foreach (JToken value in link)
                            if (value.Value<int>() < 0 || value.Value<int>() >= cubes.Count) errors.Add("Box " + index + ": invalid LinkedCubes index.");
            }
            if (boxes.Count == 0) errors.Add("Add at least one box.");
            foreach (JObject item in Array(layout, "MapData"))
            {
                if (string.IsNullOrWhiteSpace(item.Value<string>("Name"))) errors.Add("Map item needs a prefab Name.");
                foreach (string key in new[] { "Position", "Rotation", "Scale" }) CheckVector(item, key, errors);
            }
            var groupIds = new HashSet<int>();
            foreach (JObject group in Array(layout, "Groups"))
                if (Int(group, "Id") <= 0 || !groupIds.Add(Int(group, "Id"))) errors.Add("Group IDs must be positive and unique.");
            foreach (string key in new[] { "Groups", "Covers" })
                foreach (JObject entry in Array(layout, key))
                {
                    var indices = entry["BoxIndexes"] as JArray;
                    if (indices == null || indices.Count == 0) { errors.Add(key + ": empty BoxIndexes."); continue; }
                    var seen = new HashSet<int>();
                    foreach (JToken token in indices)
                    {
                        int i = token.Value<int>();
                        if (i < 0 || i >= boxes.Count || !seen.Add(i)) errors.Add(key + ": invalid or repeated box index.");
                    }
                }
            if (errors.Count == 0)
            {
                var slots = Slots(root);
                if (slots.Count == 0) errors.Add("Path has no conveyor slots.");
                foreach (JObject box in boxes)
                    if (Int(box, "PathIndexV2") > slots.Count) errors.Add("Box " + boxes.IndexOf(box) + ": PathIndexV2 is outside conveyor slots.");
            }
            if (boxes.All(box => ((JObject)box)["Cubes"] is JArray cubes && cubes.Count == 0)) warnings.Add("All boxes are empty.");
            warnings.Add("Structural validation does not prove that the puzzle is solvable. Test the saved level in Play Mode.");
            ExportedPuzzleJsonDecoder.Decode(root.ToString(Formatting.None));
        }
        catch (Exception exception) { errors.Add("Invalid data: " + exception.Message); }
        return errors;
    }
    private static void CheckVector(JObject obj, string key, List<string> errors)
    {
        var value = obj[key] as JArray;
        if (value == null || value.Count != 3 || value.Any(v => v.Type != JTokenType.Float && v.Type != JTokenType.Integer))
            errors.Add(key + " must contain three numbers.");
    }
    private static void CheckFinite(JToken token, List<string> errors, string path)
    {
        if (token.Type == JTokenType.Float)
        {
            double value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 1000000d)
                errors.Add(path + ": non-finite or excessively large number.");
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
