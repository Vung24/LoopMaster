#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using A = LoopLevelAuthoring;

// Reuse authored spatial patterns, never their puzzle contents, locks or identities.
public static class AlternativeCampaign70Geometry
{
    private static readonly Dictionary<int, List<JObject>> References = new Dictionary<int, List<JObject>>();
    private static string _referenceRoot;

    public static void Apply(JObject layout, AlternativeCampaign70Builder.Profile profile, int seed, string projectRoot)
    {
        profile.GeometryReference = 0;
        if (profile.Level <= 4) return;
        if (profile.Longs > 0)
        {
            // Long footprints need dedicated space; enclosing circles waste most of it.
            var settings = new LoopLevelGeneratorSettings { RoadShape = profile.Road, RoadClearance = .08f };
            if (settings.RoadShape == 1 || settings.RoadShape == 2)
            {
                JArray map = A.Array(layout, "MapData");
                for (int i = map.Count - 1; i >= 0; i--)
                    if (map[i].Value<string>("Name") == "Capacity") map.RemoveAt(i);
            }
            if (settings.RoadShape < 3) settings.RoadShape = 0;
            LoopLevelBoardLayout.FitRoadOnly(layout, settings);
            LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
            EnsureCapacity(layout);
            return;
        }

        LoadReferences(projectRoot);
        if (!References.TryGetValue(profile.Boxes, out List<JObject> candidates) || candidates.Count == 0)
        {
            // The original catalog has no thirteen-box normal board. Keep the generated
            // box layout and fit a compact road; validation still checks every footprint.
            var settings = new LoopLevelGeneratorSettings { RoadShape = profile.Road, RoadClearance = .08f };
            if (settings.RoadShape < 3) settings.RoadShape = 0;
            LoopLevelBoardLayout.FitRoadOnly(layout, settings);
            LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
            EnsureCapacity(layout);
            return;
        }
        var compatible = new List<JObject>();
        foreach (JObject candidate in candidates)
        {
            JArray paths = A.Array(A.Layout(candidate), "Paths");
            if (paths.Count != 1) continue;
            JObject path = (JObject)paths[0];
            if (IsClosed(path) != (profile.Road < 3)) continue;
            if (profile.Road == 3 && A.Array(path, "ControlPoints").Count != 2) continue;
            compatible.Add(candidate);
        }
        // The source set only has eight-box straight roads; retain the generated straight
        // layout for other counts instead of changing the lesson's road family.
        if (compatible.Count == 0 && profile.Road == 3) return;
        if (compatible.Count == 0) throw new FormatException("No compatible open/closed authored geometry.");
        int index = (int)((uint)seed % (uint)compatible.Count);
        JObject reference = compatible[index];
        JObject source = A.Layout(reference);
        JArray boxes = A.Array(layout, "Boxes"), originals = A.Array(source, "Boxes");
        for (int i = 0; i < boxes.Count; i++)
        {
            JObject box = (JObject)boxes[i], original = (JObject)originals[i];
            box["Rotation"] = original["Rotation"].DeepClone();
            if (A.Int(box, "BoxType") == 1) box["Rotation"] = A.Vector(Vector3.zero);
            box["PathIndex"] = original["PathIndex"].DeepClone();
            box["PathIndexV2"] = A.Int(original, "PathIndex") + 1;
        }
        float spacing = 1.08f;
        for (int attempt = 0; ; attempt++)
        {
            for (int i = 0; i < boxes.Count; i++)
                boxes[i]["Position"] = A.Vector(Expand(A.Vector((JObject)originals[i], "Position"), spacing));
            if (!Overlapping(boxes)) break;
            if (attempt >= 4) throw new FormatException("Authored geometry cannot accommodate the box footprints.");
            spacing += .04f;
        }
        layout["Paths"] = source["Paths"].DeepClone();
        foreach (JObject path in A.Array(layout, "Paths"))
        foreach (JObject point in A.Array(path, "ControlPoints"))
        foreach (string field in new[] { "Position", "InTangent", "OutTangent" })
            point[field] = A.Vector(Expand(A.Vector(point, field), spacing));
        layout["MapData"] = new JArray();
        foreach (JObject item in A.Array(source, "MapData"))
            if (item.Value<string>("Name") == "Capacity")
            {
                JObject copy = (JObject)item.DeepClone();
                copy["Position"] = A.Vector(Expand(A.Vector(copy, "Position"), spacing));
                A.Array(layout, "MapData").Add(copy);
            }
        A.SyncEnterExitGates(layout);
        EnsureCapacity(layout);
        layout["Size"] = source["Size"].DeepClone();
        profile.GeometryReference = A.Int(reference, "level_number");
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
    }

    private static void EnsureCapacity(JObject layout)
    {
        foreach (JObject item in A.Array(layout, "MapData"))
            if (item.Value<string>("Name") == "Capacity") return;
        Bounds bounds = LoopLevelBoardLayout.BoardBounds(layout, true);
        A.Array(layout, "MapData").Add(new JObject { ["Name"] = "Capacity",
            ["Position"] = A.Vector(new Vector3(bounds.center.x, .2f, bounds.max.z + 1.2f)),
            ["Rotation"] = A.Vector(Vector3.zero), ["Scale"] = A.Vector(Vector3.one) });
    }

    private static Vector3 Expand(Vector3 position, float spacing)
    {
        // Original rows sometimes use 1.6 spacing; authoring footprints require at least 1.7.
        return new Vector3(position.x * spacing, position.y, position.z * spacing);
    }

    private static bool Overlapping(JArray boxes)
    {
        for (int a = 0; a < boxes.Count; a++)
        for (int b = a + 1; b < boxes.Count; b++)
        {
            Bounds first = LoopLevelBoardLayout.BoxBounds((JObject)boxes[a]);
            Bounds second = LoopLevelBoardLayout.BoxBounds((JObject)boxes[b]);
            float x = Mathf.Min(first.max.x, second.max.x) - Mathf.Max(first.min.x, second.min.x);
            float z = Mathf.Min(first.max.z, second.max.z) - Mathf.Max(first.min.z, second.min.z);
            if (x >= .01f && z >= .01f) return true;
        }
        return false;
    }

    private static bool IsClosed(JObject path)
    {
        if (A.Int(path, "IsCircle") > 0) return true;
        JArray points = A.Array(path, "ControlPoints");
        return points.Count > 1 && (A.Vector((JObject)points[0], "Position") -
            A.Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .0001f;
    }

    private static void LoadReferences(string projectRoot)
    {
        string catalog = Path.GetFullPath(Path.Combine(projectRoot,
            "docs/superpowers/verification/alternative-campaign-70/geometry-references.json"));
        if (_referenceRoot == catalog) return;
        if (!File.Exists(catalog)) throw new FileNotFoundException("The original authored geometry catalog is required to rebuild the campaign.", catalog);
        References.Clear();
        foreach (JObject root in JArray.Parse(File.ReadAllText(catalog)))
        {
            JArray boxes = A.Array(A.Layout(root), "Boxes");
            bool normal = true;
            foreach (JObject box in boxes)
                if (A.Int(box, "BoxType") != 0) { normal = false; break; }
            if (!normal || boxes.Count == 0) continue;
            if (!References.TryGetValue(boxes.Count, out List<JObject> candidates))
            {
                candidates = new List<JObject>();
                References.Add(boxes.Count, candidates);
            }
            candidates.Add(root);
        }
        _referenceRoot = catalog;
    }
}
#endif
