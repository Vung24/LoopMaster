#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Owns disposable preview meshes; no objects are added to the edited scene.</summary>
public sealed class LoopLevelRoadPreview : IDisposable
{
    public readonly List<Mesh> Meshes = new List<Mesh>();
    public readonly List<Vector3[]> Lines = new List<Vector3[]>();
    private readonly List<GamePath> _paths = new List<GamePath>();
    private PreviewRenderUtility _renderer;
    private Material _road, _wall;
    private string _pathJson;

    public void Update(string pathJson, Puzzle puzzle)
    {
        if (_pathJson == pathJson) return;
        ClearMeshes(); _pathJson = null;
        foreach (GamePath path in puzzle.Paths)
        {
            Mesh mesh = BuildMesh(path, out Vector3[] line);
            Meshes.Add(mesh); Lines.Add(line); _paths.Add(path);
        }
        _pathJson = pathJson;
    }

    // Keep sampling, tangent fallback, topology and UVs identical to GameRoad.
    // RecoveredRoadGeometry supplies the actual fourteen-vertex road cross section.
    public static Mesh BuildMesh(GamePath path, out Vector3[] line)
    {
        line = Array.Empty<Vector3>();
        if (path?.ControlPoints == null || path.ControlPoints.Count < 2 || path.ControlPoints.Count > 128) return null;
        foreach (ControlPoint point in path.ControlPoints)
            if (!Finite(point.Position) || !Finite(point.InTangent) || !Finite(point.OutTangent)) return null;
        int samples = Mathf.Max(2, path.ControlPoints.Count * 30), sections = samples + 1;
        int stride = RecoveredRoadGeometry.SectionVertexCount;
        var vertices = new Vector3[sections * stride];
        var uvs = new Vector2[vertices.Length];
        line = new Vector3[sections + (path.IsCircle > 0 ? 1 : 0)];
        for (int i = 0; i < sections; i++)
        {
            float progress = i / (float)samples;
            int segments = path.ControlPoints.Count - 1;
            float scaled = Mathf.Clamp01(progress) * segments;
            int segment = Mathf.Min(Mathf.FloorToInt(scaled), segments - 1);
            float t = segment == segments - 1 && progress >= 1f ? 1f : scaled - segment;
            ControlPoint a = path.ControlPoints[segment], b = path.ControlPoints[segment + 1];
            float inverse = 1f - t;
            Vector3 position = inverse * inverse * inverse * a.Position +
                3f * inverse * inverse * t * a.OutTangent +
                3f * inverse * t * t * b.InTangent + t * t * t * b.Position;
            if (!Finite(position)) { line = Array.Empty<Vector3>(); return null; }
            Vector3 tangent = 3f * inverse * inverse * (a.OutTangent - a.Position) +
                6f * inverse * t * (b.InTangent - a.OutTangent) +
                3f * t * t * (b.Position - b.InTangent);
            tangent = tangent.sqrMagnitude > .0001f ? tangent.normalized : (b.Position - a.Position).normalized;
            Vector3 right = Vector3.Cross(tangent, Vector3.up).normalized;
            if (right.sqrMagnitude < .001f) right = Vector3.left;
            line[i] = position;
            int first = i * stride;
            for (int v = 0; v < stride; v++) vertices[first + v] = RecoveredRoadGeometry.GetVertex(v, position, right);
            uvs[first + 1] = new Vector2(1f, .5f);
            for (int side = 0; side < 2; side++)
            {
                int start = first + (side == 0 ? 3 : 9);
                uvs[start] = new Vector2(.35f, .5f); uvs[start + 1] = new Vector2(.45f, .5f);
                uvs[start + 2] = new Vector2(.55f, .5f); uvs[start + 3] = new Vector2(.65f, .5f);
                uvs[start + 4] = new Vector2(1f, .5f);
            }
        }
        uvs[1] = new Vector2(1f, 0f);
        if (path.IsCircle > 0) line[line.Length - 1] = line[0];
        int links = path.IsCircle > 0 ? sections : sections - 1;
        var road = new int[links * 6]; var walls = new int[links * 60];
        for (int i = 0; i < links; i++)
        {
            int from = i * stride, to = (i + 1) % sections * stride;
            RecoveredRoadGeometry.WriteRoadLink(road, i * 6, from, to);
            RecoveredRoadGeometry.WriteWallLink(walls, i * 60, from, to);
        }
        var mesh = new Mesh { name = "Level Editor road preview", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = vertices; mesh.uv = uvs; mesh.subMeshCount = 2;
        mesh.SetTriangles(road, 0); mesh.SetTriangles(walls, 1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private void EnsureMaterials()
    {
        if (_road != null) return;
        Shader shader = Shader.Find("Hidden/LoopMaster/RoadPreview");
        if (shader == null) throw new InvalidOperationException("RoadPreview shader is missing; allow Unity to finish importing editor assets.");
        _road = new Material(shader) { name = "Preview road surface", hideFlags = HideFlags.HideAndDontSave, color = new Color(.29f, .38f, .50f) };
        _wall = new Material(shader) { name = "Preview road walls", hideFlags = HideFlags.HideAndDontSave, color = new Color(.95f, .72f, .30f) };
    }

    public void DrawSceneMeshes()
    {
        EnsureMaterials();
        foreach (Mesh mesh in Meshes)
        {
            if (mesh == null) continue;
            _road.SetPass(0); Graphics.DrawMeshNow(mesh, Matrix4x4.identity, 0);
            _wall.SetPass(0); Graphics.DrawMeshNow(mesh, Matrix4x4.identity, 1);
        }
    }

    public Bounds BoundsFor(int selected, bool all)
    {
        Bounds bounds = default; bool found = false;
        for (int i = 0; i < Meshes.Count; i++)
        {
            if ((!all && i != selected) || Meshes[i] == null) continue;
            if (!found) { bounds = Meshes[i].bounds; found = true; }
            else bounds.Encapsulate(Meshes[i].bounds);
        }
        return found ? bounds : new Bounds(Vector3.zero, Vector3.one * 8);
    }

    public void DrawInspector(Rect rect, int selected, bool all, int mode, Vector2 orbit, float zoom, int point)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            GUI.Label(rect, "Road preview requires a graphics device.", EditorStyles.centeredGreyMiniLabel); return;
        }
        EnsureMaterials();
        if (_renderer == null) _renderer = new PreviewRenderUtility();
        Camera camera = _renderer.camera;
        Bounds bounds = BoundsFor(selected, all);
        Quaternion rotation = Quaternion.Euler(orbit.y, orbit.x, 0);
        float radius = Mathf.Max(.5f, bounds.extents.magnitude);
        camera.orthographic = true;
        camera.orthographicSize = radius * 1.15f * zoom * Mathf.Max(1f, rect.height / rect.width);
        camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * radius * 4f, rotation);
        camera.nearClipPlane = .01f; camera.farClipPlane = radius * 10f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.11f, .13f, .17f);
        _renderer.BeginPreview(rect, GUIStyle.none);
        for (int i = 0; i < Meshes.Count; i++)
        {
            if (mode == 2 || (!all && i != selected) || Meshes[i] == null) continue;
            _renderer.DrawMesh(Meshes[i], Matrix4x4.identity, _road, 0);
            _renderer.DrawMesh(Meshes[i], Matrix4x4.identity, _wall, 1);
        }
        _renderer.Render();
        Texture texture = _renderer.EndPreview();
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        // Project handles over the image so points and tangent positions remain legible.
        GUI.BeginGroup(rect);
        Handles.BeginGUI();
        for (int i = 0; i < Lines.Count; i++)
        {
            if (!all && i != selected) continue;
            Vector3[] line = Lines[i];
            if (line.Length == 0) continue;
            if (mode != 1 && line.Length > 1)
            {
                var projected = new Vector3[line.Length];
                for (int j = 0; j < line.Length; j++) projected[j] = Project(camera, line[j], rect.size);
                Handles.color = i == selected ? Color.cyan : new Color(.4f, .65f, .7f);
                Handles.DrawAAPolyLine(2f, projected);
            }
            if (i != selected || _paths[i]?.ControlPoints == null) continue;
            var points = _paths[i].ControlPoints;
            for (int j = 0; j < points.Count; j++)
            {
                Vector3 p = Project(camera, points[j].Position, rect.size);
                Handles.color = j == point ? Color.yellow : Color.cyan;
                Handles.DrawSolidDisc(p, Vector3.forward, j == point ? 5f : 3f);
                GUI.Label(new Rect(p.x + 6, p.y - 8, 45, 20), j.ToString(), EditorStyles.whiteMiniLabel);
                if (j != point) continue;
                foreach (Vector3 tangent in new[] { points[j].InTangent, points[j].OutTangent })
                {
                    Vector3 projectedTangent = Project(camera, tangent, rect.size);
                    Handles.color = Color.green; Handles.DrawAAPolyLine(1.5f, p, projectedTangent);
                    Handles.DrawSolidDisc(projectedTangent, Vector3.forward, 3f);
                }
            }
        }
        Handles.EndGUI(); GUI.EndGroup();
    }

    private static Vector3 Project(Camera camera, Vector3 point, Vector2 size)
    {
        Vector3 p = camera.WorldToViewportPoint(point);
        return new Vector3(p.x * size.x, (1f - p.y) * size.y, 0);
    }
    private void ClearMeshes()
    {
        foreach (Mesh mesh in Meshes) if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
        Meshes.Clear(); Lines.Clear(); _paths.Clear();
    }
    private static bool Finite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    public void Dispose()
    {
        ClearMeshes(); _pathJson = null;
        _renderer?.Cleanup(); _renderer = null;
        if (_road != null) UnityEngine.Object.DestroyImmediate(_road);
        if (_wall != null) UnityEngine.Object.DestroyImmediate(_wall);
        _road = _wall = null;
    }
}
#endif
