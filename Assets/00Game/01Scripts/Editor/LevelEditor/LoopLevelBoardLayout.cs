#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using A = LoopLevelAuthoring;

public static class LoopLevelBoardLayout
{
    private const float RoadSamplingPadding = .005f;
    public static Bounds LocalBounds(int type)
    {
        if (type == 1)
        {
            return new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(1.7f, 1.5f, 2.0f));
        }

        if (type == 2)
        {
            return new Bounds(new Vector3(0f, 0.6f, -0.6f), new Vector3(1.7f, 1.2f, 5.8f));
        }

        return new Bounds(new Vector3(0f, 0.6f, 0.15f), new Vector3(1.7f, 1.2f, 4.3f));
    }

    public static float GetTangentSpan(int boxType)
    {
        if (boxType == 1)
        {
            return LocalBounds(1).size.z;
        }

        return LocalBounds(boxType).size.x;
    }

    public static float GetRadialSpan(int boxType)
    {
        if (boxType == 1)
        {
            return LocalBounds(1).size.x;
        }

        return LocalBounds(boxType).size.z;
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
        {
            foreach (JObject road in A.Array(layout, "Paths"))
                foreach (Vector3 point in SampleRoad(road))
                {
                    var item = new Bounds(point, new Vector3(1, .5f, 1));
                    if (!any) { bounds = item; any = true; } else bounds.Encapsulate(item);
                }
            foreach (JObject mapItem in A.Array(layout, "MapData"))
            {
                var item = new Bounds(A.Vector(mapItem, "Position"), new Vector3(2, 1, 2));
                if (!any) { bounds = item; any = true; } else bounds.Encapsulate(item);
            }
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
        var leftBank = new List<int>();
        var rightBank = new List<int>();
        for (int i = 0; i < order.Count; i++)
        {
            if (i < split)
            {
                leftBank.Add(order[i]);
            }
            else
            {
                rightBank.Add(order[i]);
            }
        }

        ArrangeVerticalBank(boxes, leftBank, gap, gap, -1f);
        ArrangeVerticalBank(boxes, rightBank, gap, gap, 1f);
    }

    private static void ArrangeVerticalBank(JArray boxes, List<int> bank, float gap, float centerGap, float sign)
    {
        if (bank.Count == 0)
        {
            return;
        }

        float maxRadial = 0f;
        float totalLength = 0f;
        for (int i = 0; i < bank.Count; i++)
        {
            int type = A.Int((JObject)boxes[bank[i]], "BoxType");
            float radial = GetRadialSpan(type);
            float tangent = GetTangentSpan(type);
            if (radial > maxRadial)
            {
                maxRadial = radial;
            }
            totalLength += tangent;
            if (i > 0)
            {
                totalLength += gap;
            }
        }

        float bottom = -totalLength * 0.5f;
        for (int i = 0; i < bank.Count; i++)
        {
            JObject box = (JObject)boxes[bank[i]];
            int type = A.Int(box, "BoxType");
            Bounds local = LocalBounds(type);
            float radial = GetRadialSpan(type);
            float length = GetTangentSpan(type);

            Vector3 rotation = new Vector3(0, type == 1 ? 0 : sign * 90f, 0);
            Vector3 center = new Vector3(sign * (centerGap * 0.5f + maxRadial - radial * 0.5f), 0f, bottom + length * 0.5f);
            Vector3 pivot = Quaternion.Euler(rotation) * new Vector3(local.center.x, 0f, local.center.z);

            box["Rotation"] = A.Vector(rotation);
            box["Position"] = A.Vector(center - pivot);
            bottom += length + gap;
        }
    }

    public static void ArrangeTwoColumnsFacingOutward(JArray boxes, IList<int> selection, float gap = 0.4f, float centerGap = 0.4f)
    {
        int[] indices = GetEffectiveIndices(boxes, selection);
        if (indices.Length == 0)
        {
            return;
        }

        int split = (indices.Length + 1) / 2;
        var leftBank = new List<int>();
        var rightBank = new List<int>();
        for (int i = 0; i < indices.Length; i++)
        {
            if (i < split)
            {
                leftBank.Add(indices[i]);
            }
            else
            {
                rightBank.Add(indices[i]);
            }
        }

        ArrangeVerticalBank(boxes, leftBank, gap, centerGap, -1f);
        ArrangeVerticalBank(boxes, rightBank, gap, centerGap, 1f);
    }

    public static void FitRoadOnly(JObject layout, LoopLevelGeneratorSettings settings)
    {
        JArray boxes = A.Array(layout, "Boxes");
        if (boxes.Count == 0)
        {
            throw new FormatException("Thêm hộp trước khi chỉnh đường bao.");
        }

        Bounds content = BoardBounds(layout);
        if (settings.RoadShape >= 3)
        {
            FitOpenRoad(layout, boxes, content, settings.RoadShape, settings.RoadClearance);
        }
        else
        {
            layout["Paths"] = new JArray(FitRoad(boxes, content, settings.RoadShape, settings.RoadClearance));
            A.SyncEnterExitGates(layout);
        }

        Bounds whole = BoardBounds(layout, true);
        layout["Size"] = Mathf.Max(whole.size.x, whole.size.z) + 1f;
    }

    public static void FitOpenRoad(JObject layout, JArray boxes, Bounds content, int shape, float clearance)
    {
        Vector3 center = new Vector3(content.center.x, 0.2f, content.center.z);
        float requiredClearance = clearance + 0.5f;
        float padding = requiredClearance + 0.05f;
        float width = content.size.x + 2f * padding;
        float depth = content.size.z + 2f * padding;

        if (shape == 3)
        {
            A.FitStraightRoadBesideBoxes(layout, padding, true);
        }
        else if (shape == 4)
        {
            layout["Paths"] = new JArray(A.SCurveRoad(center, width, depth));
            A.SyncEnterExitGates(layout);
        }
        else if (shape == 5)
        {
            layout["Paths"] = new JArray(A.OpenWindingRoad(center, width, depth));
            A.SyncEnterExitGates(layout);
        }
    }

    private static void FitAndBind(JObject layout, LoopLevelGeneratorSettings settings)
    {
        JArray boxes = A.Array(layout, "Boxes");
        Bounds content = BoardBounds(layout);
        Vector3 shift = new Vector3(content.center.x, 0f, content.center.z);
        for (int i = 0; i < boxes.Count; i++)
        {
            JObject box = (JObject)boxes[i];
            box["Position"] = A.Vector(A.Vector(box, "Position") - shift);
        }

        content = BoardBounds(layout);

        if (settings.RoadShape >= 3)
        {
            FitOpenRoad(layout, boxes, content, settings.RoadShape, settings.RoadClearance);
        }
        else
        {
            JObject fitted = FitRoad(boxes, content, settings.RoadShape, settings.RoadClearance);
            layout["Paths"] = new JArray(fitted);
            A.SyncEnterExitGates(layout);
        }

        Bounds whole = BoardBounds(layout, true);
        layout["Size"] = Mathf.Max(whole.size.x, whole.size.z) + 1f;
        AutoBindBoxesToRoad(layout);
    }

    private static bool AreAllBoxesContained(JArray boxes, Vector3[] samples, float clearance)
    {
        for (int b = 0; b < boxes.Count; b++)
        {
            Vector3[] corners = Corners((JObject)boxes[b]);
            for (int c = 0; c < corners.Length; c++)
            {
                if (!ContainsWithClearance(samples, corners[c], clearance))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static JObject FitRoad(JArray boxes, Bounds content, int shape, float clearance)
    {
        Vector3 center = new Vector3(content.center.x, 0.2f, content.center.z);
        float requiredClearance = clearance + 0.5f;
        float padding = requiredClearance + 0.05f;

        float width = content.size.x + 2f * padding;
        float depth = content.size.z + 2f * padding;

        if (shape == 1)
        {
            float halfW = content.size.x * 0.5f;
            float halfD = content.size.z * 0.5f;
            float diag = Mathf.Sqrt(halfW * halfW + halfD * halfD);
            float targetDiag = diag + requiredClearance + 0.05f;
            float scale = targetDiag / Mathf.Max(0.001f, diag);
            width = 2f * halfW * scale;
            depth = 2f * halfD * scale;
        }
        else if (shape == 2)
        {
            float maxDim = Mathf.Max(width, depth);
            width = maxDim;
            depth = maxDim;
        }

        float radius = Mathf.Clamp(requiredClearance * 0.85f, 0.4f, Mathf.Min(width, depth) * 0.25f);
        float collisionClearance = requiredClearance - 0.001f;

        for (int attempt = 0; attempt < 25; attempt++)
        {
            JObject road = shape == 0
                ? A.RoundedRectangleRoad(center, width, depth, radius)
                : A.OvalRoad(center, width, depth);

            Vector3[] samples = SampleRoad(road);
            if (AreAllBoxesContained(boxes, samples, collisionClearance))
            {
                return road;
            }

            width += 0.15f;
            depth += 0.15f;
            radius = Mathf.Clamp(radius + 0.05f, 0.4f, Mathf.Min(width, depth) * 0.25f);
        }

        return shape == 0
            ? A.RoundedRectangleRoad(center, width, depth, radius)
            : A.OvalRoad(center, width, depth);
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
        bool inside = false;
        Vector2 p = new Vector2(point.x, point.z);
        float clearanceSq = clearance * clearance - 0.00001f;

        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = new Vector2(polygon[j].x, polygon[j].z);
            Vector2 b = new Vector2(polygon[i].x, polygon[i].z);

            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }

            Vector2 segment = b - a;
            float t = segment.sqrMagnitude < 0.000001f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, segment) / segment.sqrMagnitude);
            if ((p - (a + segment * t)).sqrMagnitude < clearanceSq)
            {
                return false;
            }
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

    public static int[] GetEffectiveIndices(JArray boxes, IList<int> selection)
    {
        if (boxes == null || boxes.Count == 0) return Array.Empty<int>();
        if (selection != null && selection.Count > 0)
        {
            var valid = new List<int>();
            for (int i = 0; i < selection.Count; i++)
            {
                int index = selection[i];
                if (index >= 0 && index < boxes.Count && !valid.Contains(index))
                    valid.Add(index);
            }
            if (valid.Count > 0) return valid.ToArray();
        }
        var all = new int[boxes.Count];
        for (int i = 0; i < boxes.Count; i++) all[i] = i;
        return all;
    }

    public static void ArrangeSingleRow(JArray boxes, IList<int> selection, float gap = 0.4f)
    {
        int[] indices = GetEffectiveIndices(boxes, selection);
        if (indices.Length == 0) return;

        float totalWidth = 0f;
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            Bounds local = LocalBounds(A.Int(box, "BoxType"));
            totalWidth += local.size.x;
            if (i > 0) totalWidth += gap;
        }

        float currentZ = 0f;
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            currentZ += A.Vector(box, "Position").z;
        }
        currentZ /= indices.Length;

        float left = -totalWidth * 0.5f;
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            int type = A.Int(box, "BoxType");
            Bounds local = LocalBounds(type);
            Vector3 rotation = new Vector3(0, 0, 0);
            Vector3 center = new Vector3(left + local.size.x * 0.5f, 0f, currentZ);
            Vector3 pivot = Quaternion.Euler(rotation) * new Vector3(local.center.x, 0, local.center.z);
            box["Rotation"] = A.Vector(rotation);
            box["Position"] = A.Vector(center - pivot);
            left += local.size.x + gap;
        }
    }

    public static void ArrangeSingleColumn(JArray boxes, IList<int> selection, float gap = 0.4f, float rotationY = 270f)
    {
        int[] indices = GetEffectiveIndices(boxes, selection);
        if (indices.Length == 0) return;

        float totalDepth = 0f;
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            int type = A.Int(box, "BoxType");
            Bounds local = LocalBounds(type);
            float spanZ = type == 1 ? local.size.z : local.size.x;
            totalDepth += spanZ;
            if (i > 0) totalDepth += gap;
        }

        float currentX = 0f;
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            currentX += A.Vector(box, "Position").x;
        }
        currentX /= indices.Length;

        float top = totalDepth * 0.5f;
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            int type = A.Int(box, "BoxType");
            Bounds local = LocalBounds(type);
            float spanZ = type == 1 ? local.size.z : local.size.x;
            Vector3 rotation = new Vector3(0, type == 1 ? 0 : rotationY, 0);
            Vector3 center = new Vector3(currentX, 0f, top - spanZ * 0.5f);
            Vector3 pivot = Quaternion.Euler(rotation) * new Vector3(local.center.x, 0, local.center.z);
            box["Rotation"] = A.Vector(rotation);
            box["Position"] = A.Vector(center - pivot);
            top -= spanZ + gap;
        }
    }

    public static void ArrangeTwoBanks(JArray boxes, IList<int> selection, float horizontalGap = 0.4f, float bankDistance = 4.5f)
    {
        int[] indices = GetEffectiveIndices(boxes, selection);
        if (indices.Length == 0) return;

        int split = (indices.Length + 1) / 2;
        var topBank = new List<int>();
        var bottomBank = new List<int>();
        for (int i = 0; i < indices.Length; i++)
        {
            if (i < split) topBank.Add(indices[i]);
            else bottomBank.Add(indices[i]);
        }

        ArrangeBankRow(boxes, topBank, horizontalGap, bankDistance * 0.5f, 180f);
        ArrangeBankRow(boxes, bottomBank, horizontalGap, -bankDistance * 0.5f, 0f);
    }

    private static void ArrangeBankRow(JArray boxes, List<int> bank, float gap, float z, float rotationY)
    {
        if (bank.Count == 0) return;
        float totalWidth = 0f;
        for (int i = 0; i < bank.Count; i++)
        {
            JObject box = (JObject)boxes[bank[i]];
            Bounds local = LocalBounds(A.Int(box, "BoxType"));
            totalWidth += local.size.x;
            if (i > 0) totalWidth += gap;
        }

        float left = -totalWidth * 0.5f;
        for (int i = 0; i < bank.Count; i++)
        {
            JObject box = (JObject)boxes[bank[i]];
            int type = A.Int(box, "BoxType");
            Bounds local = LocalBounds(type);
            Vector3 rotation = new Vector3(0, type == 1 ? 0 : rotationY, 0);
            Vector3 center = new Vector3(left + local.size.x * 0.5f, 0f, z);
            Vector3 pivot = Quaternion.Euler(rotation) * new Vector3(local.center.x, 0, local.center.z);
            box["Rotation"] = A.Vector(rotation);
            box["Position"] = A.Vector(center - pivot);
            left += local.size.x + gap;
        }
    }

    public static void ArrangeGrid(JArray boxes, IList<int> selection, int columns = 2, float gapX = 0.4f, float gapZ = 0.8f)
    {
        int[] indices = GetEffectiveIndices(boxes, selection);
        if (indices.Length == 0) return;
        columns = Mathf.Max(1, columns);
        int rows = Mathf.CeilToInt(indices.Length / (float)columns);

        float maxRowWidth = 0f;
        float totalDepth = 0f;
        for (int r = 0; r < rows; r++)
        {
            float rowWidth = 0f;
            float maxBoxDepth = 0f;
            for (int c = 0; c < columns; c++)
            {
                int itemIdx = r * columns + c;
                if (itemIdx >= indices.Length) break;
                JObject box = (JObject)boxes[indices[itemIdx]];
                Bounds local = LocalBounds(A.Int(box, "BoxType"));
                rowWidth += local.size.x;
                if (c > 0) rowWidth += gapX;
                if (local.size.z > maxBoxDepth) maxBoxDepth = local.size.z;
            }
            if (rowWidth > maxRowWidth) maxRowWidth = rowWidth;
            totalDepth += maxBoxDepth;
            if (r > 0) totalDepth += gapZ;
        }

        float topZ = totalDepth * 0.5f;
        int current = 0;
        for (int r = 0; r < rows; r++)
        {
            int inThisRow = Mathf.Min(columns, indices.Length - current);
            float thisRowWidth = 0f;
            float maxBoxDepth = 0f;
            for (int c = 0; c < inThisRow; c++)
            {
                JObject box = (JObject)boxes[indices[current + c]];
                Bounds local = LocalBounds(A.Int(box, "BoxType"));
                thisRowWidth += local.size.x;
                if (c > 0) thisRowWidth += gapX;
                if (local.size.z > maxBoxDepth) maxBoxDepth = local.size.z;
            }

            float leftX = -thisRowWidth * 0.5f;
            float centerZ = topZ - maxBoxDepth * 0.5f;
            for (int c = 0; c < inThisRow; c++)
            {
                JObject box = (JObject)boxes[indices[current++]];
                int type = A.Int(box, "BoxType");
                Bounds local = LocalBounds(type);
                Vector3 rotation = new Vector3(0, type != 1 && centerZ < 0 ? 180 : 0, 0);
                Vector3 center = new Vector3(leftX + local.size.x * 0.5f, 0f, centerZ);
                Vector3 pivot = Quaternion.Euler(rotation) * new Vector3(local.center.x, 0, local.center.z);
                box["Rotation"] = A.Vector(rotation);
                box["Position"] = A.Vector(center - pivot);
                leftX += local.size.x + gapX;
            }
            topZ -= maxBoxDepth + gapZ;
        }
    }

    public static void AlignBoxes(JArray boxes, IList<int> selection, bool alignZ)
    {
        int[] indices = GetEffectiveIndices(boxes, selection);
        if (indices.Length < 2) return;

        float sum = 0f;
        for (int i = 0; i < indices.Length; i++)
        {
            Vector3 pos = A.Vector((JObject)boxes[indices[i]], "Position");
            sum += alignZ ? pos.z : pos.x;
        }
        float avg = sum / indices.Length;

        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            Vector3 pos = A.Vector(box, "Position");
            if (alignZ) pos.z = avg;
            else pos.x = avg;
            box["Position"] = A.Vector(pos);
        }
    }

    public static void RotateBoxes(JArray boxes, IList<int> selection, float angleY)
    {
        int[] indices = GetEffectiveIndices(boxes, selection);
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            if (A.Int(box, "BoxType") == 1) continue;
            box["Rotation"] = A.Vector(new Vector3(0, angleY, 0));
        }
    }

    public static void RotateBoxesTowardsCenter(JArray boxes, IList<int> selection)
    {
        int[] indices = GetEffectiveIndices(boxes, selection);
        for (int i = 0; i < indices.Length; i++)
        {
            JObject box = (JObject)boxes[indices[i]];
            if (A.Int(box, "BoxType") == 1) continue;
            Vector3 pos = A.Vector(box, "Position");
            Vector3 dir = -pos; dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) continue;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            box["Rotation"] = A.Vector(new Vector3(0, yaw, 0));
        }
    }

    public static void AutoBindBoxesToRoad(JObject layout)
    {
        if (layout == null) return;
        JArray boxes = A.Array(layout, "Boxes");
        if (boxes.Count == 0) return;
        List<Vector3> slots = A.Slots(new JObject { ["layout"] = layout.DeepClone() });
        if (slots.Count == 0) return;

        HashSet<int> used = new HashSet<int>();
        for (int b = 0; b < boxes.Count; b++)
        {
            JObject box = (JObject)boxes[b];
            int type = A.Int(box, "BoxType");
            Quaternion rotation = type == 1 ? Quaternion.identity : Quaternion.Euler(A.Vector(box, "Rotation"));
            Vector3 exit = A.Vector(box, "Position") + rotation * new Vector3(0, 0, type == 1 ? -0.2f : 2.2f);

            int nearest = -1;
            float bestDistSq = float.PositiveInfinity;
            for (int s = 0; s < slots.Count; s++)
            {
                if (used.Contains(s)) continue;
                float d = (slots[s] - exit).sqrMagnitude;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    nearest = s;
                }
            }

            if (nearest < 0)
            {
                for (int s = 0; s < slots.Count; s++)
                {
                    float d = (slots[s] - exit).sqrMagnitude;
                    if (d < bestDistSq)
                    {
                        bestDistSq = d;
                        nearest = s;
                    }
                }
            }

            if (nearest >= 0)
            {
                used.Add(nearest);
                box["PathIndex"] = nearest;
                box["PathIndexV2"] = nearest + 1;
            }
        }
    }
}
#endif
