#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using A = LoopLevelAuthoring;

public static class LoopLevelBoardLayout
{
    public static Bounds LocalBounds(int type)
    {
        if (type == 1) return new Bounds(new Vector3(0, .9f, 0), new Vector3(2.2f, 1.8f, 2.4f));
        return type == 2
            ? new Bounds(new Vector3(0, .7f, -.15f), new Vector3(2, 1.4f, 6.9f))
            : new Bounds(new Vector3(0, .7f, .6f), new Vector3(2, 1.4f, 5.4f));
    }
    public static Vector3[] Corners(JObject box)
    {
        Bounds local = LocalBounds(A.Int(box, "BoxType"));
        Quaternion rotation = A.Int(box, "BoxType") == 1 ? Quaternion.identity : Quaternion.Euler(A.Vector(box, "Rotation"));
        Vector3 position = A.Vector(box, "Position");
        var result = new Vector3[8];
        for (int i = 0; i < 8; i++)
            result[i] = position + rotation * new Vector3((i & 1) == 0 ? local.min.x : local.max.x,
                (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z);
        return result;
    }
    public static Bounds BoxBounds(JObject box)
    {
        Vector3[] corners = Corners(box); var bounds = new Bounds(corners[0], Vector3.zero);
        foreach (Vector3 corner in corners) bounds.Encapsulate(corner);
        return bounds;
    }
    public static Bounds BoardBounds(JObject layout, bool includeRoad = false)
    {
        Bounds bounds = default; bool any = false;
        foreach (JObject box in A.Array(layout, "Boxes"))
        {
            Bounds item = BoxBounds(box);
            if (!any) { bounds = item; any = true; } else bounds.Encapsulate(item);
        }
        if (includeRoad)
            foreach (JObject road in A.Array(layout, "Paths"))
                foreach (Vector3 point in SampleRoad(road))
                {
                    var item = new Bounds(point, new Vector3(1, .5f, 1));
                    if (!any) { bounds = item; any = true; } else bounds.Encapsulate(item);
                }
        return any ? bounds : new Bounds(Vector3.zero, Vector3.one * 8);
    }
    public static void Arrange(JObject layout, LoopLevelGeneratorSettings settings, int seed)
    {
        JArray boxes = A.Array(layout, "Boxes");
        if (boxes.Count == 0) throw new FormatException("Thêm hộp trước khi sắp xếp bàn chơi.");
        var order = Enumerable.Range(0, boxes.Count).ToList(); var random = new System.Random(seed);
        for (int i = order.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int old = order[i]; order[i] = order[j]; order[j] = old; }
        if (settings.BoardLayout == 0)
        {
            ArrangeBanks(boxes, order, settings.BoxGap);
            FitAndBind(layout, settings);
            return;
        }
        List<List<int>> best = null; float bestCost = float.PositiveInfinity;
        int first = settings.BoardColumns == 0 ? 1 : Math.Min(settings.BoardColumns, boxes.Count);
        int last = settings.BoardColumns == 0 ? boxes.Count : first;
        for (int columns = first; columns <= last; columns++)
        {
            int rows = Mathf.CeilToInt(boxes.Count / (float)columns), offset = 0;
            var candidate = new List<List<int>>(); float width = 0, depth = settings.BoxGap * (rows - 1);
            for (int row = 0; row < rows; row++)
            {
                int count = boxes.Count / rows + (row < boxes.Count % rows ? 1 : 0);
                var indices = order.GetRange(offset, count); offset += count; candidate.Add(indices);
                width = Mathf.Max(width, indices.Sum(i => LocalBounds(A.Int((JObject)boxes[i], "BoxType")).size.x) + settings.BoxGap * (count - 1));
                depth += indices.Max(i => LocalBounds(A.Int((JObject)boxes[i], "BoxType")).size.z);
            }
            float ratio = (width + 2 * (settings.RoadClearance + .5f)) / (depth + 2 * (settings.RoadClearance + .5f));
            float cost = width * depth * (1 + 2 * Mathf.Abs(Mathf.Log(ratio / settings.BoardAspect)));
            if (cost < bestCost) { best = candidate; bestCost = cost; }
        }
        float totalDepth = best.Sum(row => row.Max(i => LocalBounds(A.Int((JObject)boxes[i], "BoxType")).size.z)) + settings.BoxGap * (best.Count - 1);
        float bottom = -totalDepth * .5f;
        foreach (var row in best)
        {
            float depth = row.Max(i => LocalBounds(A.Int((JObject)boxes[i], "BoxType")).size.z);
            float width = row.Sum(i => LocalBounds(A.Int((JObject)boxes[i], "BoxType")).size.x) + settings.BoxGap * (row.Count - 1);
            float left = -width * .5f, z = bottom + depth * .5f;
            foreach (int index in row)
            {
                JObject box = (JObject)boxes[index]; int type = A.Int(box, "BoxType"); Bounds local = LocalBounds(type);
                Vector3 rotation = new Vector3(0, type != 1 && z < 0 ? 180 : 0, 0);
                Vector3 center = new Vector3(left + local.size.x * .5f, 0, z);
                Vector3 pivotOffset = Quaternion.Euler(rotation) * new Vector3(local.center.x, 0, local.center.z);
                box["Rotation"] = A.Vector(rotation); box["Position"] = A.Vector(center - pivotOffset);
                left += local.size.x + settings.BoxGap;
            }
            bottom += depth + settings.BoxGap;
        }
        FitAndBind(layout, settings);
    }
    private static void ArrangeBanks(JArray boxes, List<int> order, float gap)
    {
        int split = (boxes.Count + 1) / 2;
        for (int side = 0; side < 2; side++)
        {
            var bank = side == 0 ? order.Take(split).ToList() : order.Skip(split).ToList();
            if (bank.Count == 0) continue;
            float sign = side == 0 ? -1 : 1;
            Func<int, float> tangent = i => A.Int((JObject)boxes[i], "BoxType") == 1 ? LocalBounds(1).size.z : LocalBounds(A.Int((JObject)boxes[i], "BoxType")).size.x;
            float radialDepth = bank.Max(i => A.Int((JObject)boxes[i], "BoxType") == 1 ? LocalBounds(1).size.x : LocalBounds(A.Int((JObject)boxes[i], "BoxType")).size.z);
            float total = bank.Sum(tangent) + gap * (bank.Count - 1), bottom = -total * .5f;
            foreach (int index in bank)
            {
                JObject box = (JObject)boxes[index]; int type = A.Int(box, "BoxType"); Bounds local = LocalBounds(type);
                Vector3 rotation = new Vector3(0, type == 1 ? 0 : sign * 90, 0);
                float radial = type == 1 ? local.size.x : local.size.z, length = tangent(index);
                Vector3 center = new Vector3(sign * (gap * .5f + radialDepth - radial * .5f), 0, bottom + length * .5f);
                Vector3 pivot = Quaternion.Euler(rotation) * new Vector3(local.center.x, 0, local.center.z);
                box["Rotation"] = A.Vector(rotation); box["Position"] = A.Vector(center - pivot);
                bottom += length + gap;
            }
        }
    }
    public static void FitRoadOnly(JObject layout, LoopLevelGeneratorSettings settings)
    {
        JArray boxes = A.Array(layout, "Boxes");
        if (boxes.Count == 0) throw new FormatException("Thêm hộp trước khi chỉnh đường bao.");
        Bounds content = BoardBounds(layout);
        layout["Paths"] = new JArray(FitRoad(boxes, content, settings.RoadShape, settings.RoadClearance));
        Bounds whole = BoardBounds(layout, true); layout["Size"] = Mathf.Max(whole.size.x, whole.size.z) + 1;
    }
    private static void FitAndBind(JObject layout, LoopLevelGeneratorSettings settings)
    {
        JArray boxes = A.Array(layout, "Boxes"); Bounds content = BoardBounds(layout);
        Vector3 shift = new Vector3(content.center.x, 0, content.center.z);
        foreach (JObject box in boxes) box["Position"] = A.Vector(A.Vector(box, "Position") - shift);
        content = BoardBounds(layout);
        JObject fitted = FitRoad(boxes, content, settings.RoadShape, settings.RoadClearance);
        layout["Paths"] = new JArray(fitted);
        Bounds whole = BoardBounds(layout, true); layout["Size"] = Mathf.Max(whole.size.x, whole.size.z) + 1;
        List<Vector3> slots = A.Slots(new JObject { ["layout"] = layout.DeepClone() });
        var used = new HashSet<int>();
        foreach (JObject box in boxes)
        {
            Quaternion rotation = A.Int(box, "BoxType") == 1 ? Quaternion.identity : Quaternion.Euler(A.Vector(box, "Rotation"));
            Vector3 exit = A.Vector(box, "Position") + rotation * new Vector3(0, 0, A.Int(box, "BoxType") == 1 ? -.2f : 2.2f);
            int nearest = Enumerable.Range(0, slots.Count).Where(i => !used.Contains(i))
                .OrderBy(i => (slots[i] - exit).sqrMagnitude).First();
            used.Add(nearest); box["PathIndexV2"] = nearest + 1;
        }
    }
    private static JObject FitRoad(JArray boxes, Bounds content, int shape, float clearance)
    {
        Vector3 center = new Vector3(content.center.x, .2f, content.center.z);
        float width = content.size.x + 2 * (clearance + .5f), depth = content.size.z + 2 * (clearance + .5f);
        if (shape == 2) width = depth = Mathf.Max(width, depth);
        for (int attempt = 0; attempt < 100; attempt++)
        {
            JObject road = shape == 0 ? A.RoundedRectangleRoad(center, width, depth, 1.2f) : A.OvalRoad(center, width, depth);
            Vector3[] samples = SampleRoad(road);
            if (boxes.Cast<JObject>().All(box => Corners(box).All(point => ContainsWithClearance(samples, point, clearance + .5f)))) return road;
            width *= 1.025f; depth *= 1.025f;
        }
        throw new FormatException("Không thể tạo đường bao quanh các hộp.");
    }
    public static Vector3[] SampleRoad(JObject road)
    {
        var puzzle = ExportedPuzzleJsonDecoder.Decode(new JObject { ["layout"] = new JObject { ["Paths"] = new JArray(road.DeepClone()) } }.ToString());
        if (puzzle.Paths.Count == 0 || puzzle.Paths[0].ControlPoints.Count < 2) return Array.Empty<Vector3>();
        var points = puzzle.Paths[0].ControlPoints; int count = points.Count * 30;
        return Enumerable.Range(0, count + 1).Select(i => GameUtils.GetBezierPoint(points, i / (float)count)).ToArray();
    }
    public static bool ContainsWithClearance(Vector3[] polygon, Vector3 point, float clearance)
    {
        bool inside = false; Vector2 p = new Vector2(point.x, point.z);
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = new Vector2(polygon[j].x, polygon[j].z), b = new Vector2(polygon[i].x, polygon[i].z);
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            Vector2 segment = b - a;
            float t = segment.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, segment) / segment.sqrMagnitude);
            if ((p - (a + segment * t)).sqrMagnitude < clearance * clearance - .00001f) return false;
        }
        return inside;
    }
    public static void FitCamera(Camera camera, JObject layout)
    {
        if (camera == null || !camera.orthographic) return;
        Bounds bounds = BoardBounds(layout, true); Quaternion rotation = camera.transform.rotation;
        Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
        Vector3 extents = bounds.extents;
        float horizontal = Mathf.Abs(right.x) * extents.x + Mathf.Abs(right.y) * extents.y + Mathf.Abs(right.z) * extents.z;
        float vertical = Mathf.Abs(up.x) * extents.x + Mathf.Abs(up.y) * extents.y + Mathf.Abs(up.z) * extents.z;
        camera.orthographicSize = Mathf.Max(vertical / .72f, horizontal / Mathf.Max(.1f, camera.aspect) / .9f) + .3f;
        float distance = Mathf.Max(25, bounds.size.magnitude * 2);
        camera.transform.position = bounds.center - camera.transform.forward * distance;
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, distance * 2);
    }
}
#endif
