#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using A = LoopLevelAuthoring;

public sealed partial class LoopLevelEditorWindow
{
    [SerializeField] private bool _workspace = true, _canvasPlayerView, _canvasFramed;
    [SerializeField] private List<int> _canvasSelection = new List<int>();
    [SerializeField] private Vector2 _canvasCenter, _inspectorScroll, _tableScroll;
    [SerializeField] private float _canvasScale = 25;
    [SerializeField] private int _canvasTool, _swapColor = 2, _variantSeed = 12345, _solverBudget = 10000;
    [SerializeField] private string _draftSolverJson, _draftSolverStatus;
    private JObject _canvasDraft;
    private JObject _canvasProposal;
    private string _proposalBase, _proposalAction;
    private string _canvasBase;
    private Vector2 _canvasDown, _canvasPanStart;
    private Vector3 _canvasWorldDown;
    private Rect _canvasMarquee;
    private int _canvasControl, _canvasGesture, _canvasFocus, _canvasPointPath, _canvasPointIndex;
    private string _canvasPointField;
    private Rect _canvasRect;
    private Vector2 _canvasWindowOffset;
    private int _tableDragBox = -1, _tableDragCube = -1, _tableDragControl;
    private readonly HashSet<string> _canvasStroke = new HashSet<string>();
    private IEnumerator _draftSearch;
    private DebugLevelSolver _draftSolver;
    private GameData _draftSimulation;

    private void ClearCanvasSelection() { _canvasSelection.Clear(); _box = -1; }
    private void SetCanvasSelection(IEnumerable<int> indices)
    {
        _canvasSelection = indices.Distinct().OrderBy(i => i).ToList();
        _box = _canvasSelection.Count == 0 ? -1 : _canvasSelection[_canvasSelection.Count - 1]; Repaint();
    }
    private void CancelCanvasGesture()
    {
        _canvasDraft = null; _canvasBase = null; _canvasGesture = 0; _canvasStroke.Clear();
        if (_canvasControl != 0 && GUIUtility.hotControl == _canvasControl) GUIUtility.hotControl = 0;
        if (_tableDragControl != 0 && GUIUtility.hotControl == _tableDragControl) GUIUtility.hotControl = 0;
        _tableDragBox = _tableDragCube = -1;
        Repaint();
    }
    private void OnLostFocus() { CancelCanvasGesture(); }
    private void ClearWorkspaceProposal() { _canvasProposal = null; _proposalBase = _proposalAction = null; }
    private void PreviewWorkspaceCommand(string name, Action<JObject> edit, bool moved = false)
    {
        CancelCanvasGesture();
        try
        {
            JObject original = Root(), next = (JObject)original.DeepClone(); edit(next);
            LoopLevelCommands.RemapBindings(original, next, moved ? _canvasSelection : null);
            _canvasProposal = next; _proposalBase = _document.Json; _proposalAction = name;
        }
        catch (Exception ex) { _status = ex.Message; }
        if (Event.current != null) GUIUtility.ExitGUI();
    }
    private void ApplyWorkspaceProposal()
    {
        if (_canvasProposal == null) return;
        if (_proposalBase != _document.Json) { ClearWorkspaceProposal(); _status = "Bản nháp đã đổi; tạo lại bản xem trước."; return; }
        JObject candidate = _canvasProposal; string action = _proposalAction; ClearWorkspaceProposal();
        WorkspaceCommand(action, next => { next.RemoveAll(); foreach (var property in candidate.Properties()) next.Add(property.Name, property.Value.DeepClone()); });
    }

    private void WorkspaceCommand(string name, Action<JObject> edit, bool moved = false)
    {
        if (_canvasGesture != 0 || _drawingRoad) { _status = "Hoàn tất hoặc hủy thao tác đang thực hiện trước."; return; }
        try
        {
            JObject before = Root(), next = (JObject)before.DeepClone(); edit(next);
            if (moved) LoopLevelCommands.RemapBindings(before, next, _canvasSelection);
            Undo.IncrementCurrentGroup(); Commit(next, name); Undo.IncrementCurrentGroup();
            _canvasSelection = LoopLevelCommands.Selection(next, _canvasSelection).ToList();
            _status = name + ". Hoàn tác để khôi phục.";
        }
        catch (Exception ex) { _status = ex.Message; }
        if (Event.current != null) GUIUtility.ExitGUI();
    }

    private void DrawWorkspace()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        try
        {
            JObject root = Root(); JObject layout = A.Layout(root);
            if (_canvasProposal != null && _proposalBase != _document.Json) ClearWorkspaceProposal();
            if (_canvasProposal != null)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Xem trước: " + _proposalAction + " — dữ liệu chưa đổi.");
                    if (GUILayout.Button("Áp dụng", GUILayout.Width(80))) ApplyWorkspaceProposal();
                    if (GUILayout.Button("Hủy", GUILayout.Width(55))) { ClearWorkspaceProposal(); GUIUtility.ExitGUI(); }
                }
            }
            _canvasSelection = LoopLevelCommands.Selection(root, _canvasSelection).ToList();
            if (_box >= 0 && _box < A.Array(layout, "Boxes").Count && !_canvasSelection.Contains(_box)) SetCanvasSelection(new[] { _box });
            EditorGUILayout.Space(3);
            int tool = GUILayout.Toolbar(_canvasTool, new[] { "Chọn / kéo", "Đặt hộp", "Tô màu", "Ẩn / hiện", "Chìa khóa", "Xóa khối", "Đường" });
            if (tool != _canvasTool) { CancelCanvasGesture(); _canvasTool = tool; GUIUtility.ExitGUI(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Căn khung", GUILayout.Width(85))) _canvasFramed = false;
                _canvasPlayerView = GUILayout.Toggle(_canvasPlayerView, "Che màu ẩn như người chơi", GUILayout.Width(195));
                GUILayout.Label("Shift: chọn nhiều · kéo trống: quét chọn · chuột giữa/Alt: pan · cuộn: zoom", EditorStyles.miniLabel);
            }
            DrawWorkspacePalette(layout);
            float height = Mathf.Clamp(position.height - 530, 230, 600);
            bool wide = position.width >= 850;
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect canvas = GUILayoutUtility.GetRect(100, height, GUILayout.ExpandWidth(true));
                DrawCanvas(canvas, _canvasProposal ?? _canvasDraft ?? root);
                if (wide)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
                    {
                        using (var inspector = new EditorGUILayout.ScrollViewScope(_inspectorScroll, GUILayout.Height(height)))
                        { _inspectorScroll = inspector.scrollPosition; using (new EditorGUI.DisabledScope(_canvasProposal != null)) DrawWorkspaceInspector(root); }
                    }
                }
            }
            if (!wide)
            {
                using (var inspector = new EditorGUILayout.ScrollViewScope(_inspectorScroll, GUILayout.Height(160)))
                { _inspectorScroll = inspector.scrollPosition; using (new EditorGUI.DisabledScope(_canvasProposal != null)) DrawWorkspaceInspector(root); }
            }
            using (new EditorGUI.DisabledScope(_canvasProposal != null)) DrawWorkspaceOperations(root);
            EditorGUILayout.LabelField("Nội dung toàn bàn — trái: đáy · phải: thả trước · công cụ Chọn: kéo để đổi thứ tự", EditorStyles.boldLabel);
            using (var table = new EditorGUILayout.ScrollViewScope(_tableScroll, GUILayout.Height(135)))
            {
            _tableScroll = table.scrollPosition;
            int previousBox = _box;
            JObject tableSource = _canvasDraft ?? root;
            JObject tableRoot = (JObject)tableSource.DeepClone();
            _cubeBrushMode = _canvasTool == 3 ? 1 : _canvasTool == 4 ? 2 : 0;
            using (new EditorGUI.DisabledScope(_canvasProposal != null)) DrawBoardTable(A.Layout(tableRoot));
            if (_box != previousBox) SetCanvasSelection(new[] { _box });
            if (!JToken.DeepEquals(tableRoot, tableSource) && _canvasGesture == 0) { Commit(tableRoot, "Sửa khối trong bảng"); GUIUtility.ExitGUI(); }
            }
            DrawWorkspaceValidation(Root());
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
            if (!string.IsNullOrEmpty(LoopLevelPlaytest.Status)) EditorGUILayout.LabelField(LoopLevelPlaytest.Status, EditorStyles.wordWrappedMiniLabel);
            if (GUI.changed) { Repaint(); SceneView.RepaintAll(); }
        }
        catch (ExitGUIException) { throw; }
        catch (Exception ex) { CancelCanvasGesture(); EditorGUILayout.HelpBox(ex.Message, MessageType.Error); }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private void DrawWorkspacePalette(JObject layout)
    {
        int columns = position.width < 700 ? 7 : 14;
        for (int row = 0; row < 14 / columns; row++)
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int c = row * columns + 1; c <= (row + 1) * columns; c++)
            {
                Color old = GUI.backgroundColor; GUI.backgroundColor = A.ColorFor(c);
                if (GUILayout.Toggle(_brushColor == c, c.ToString(), "Button", GUILayout.Width(27), GUILayout.Height(25))) _brushColor = c;
                GUI.backgroundColor = old;
            }
            GUILayout.FlexibleSpace();
            if (row == 14 / columns - 1) _snap = Mathf.Max(0, EditorGUILayout.FloatField("Lưới", _snap, GUILayout.Width(120)));
        }
        if (_canvasTool == 4)
        {
            int[] ids = new[] { 0 }.Concat(A.Array(layout, "Groups").OfType<JObject>().Select(g => A.Int(g, "Id"))).Distinct().ToArray();
            int selected = Math.Max(0, Array.IndexOf(ids, _keyBrush));
            _keyBrush = ids[EditorGUILayout.Popup("Chìa khóa", selected, ids.Select(i => i == 0 ? "Bỏ chìa khóa" : "Nhóm " + i).ToArray())];
        }
        if (_canvasTool == 1)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _placementType = EditorGUILayout.Popup("Loại hộp", _placementType, new[] { "Thường", "Đơn", "Dài" });
                _placementYaw = EditorGUILayout.FloatField("Góc Y", _placementYaw);
            }
        }
    }

    private Vector2 CanvasProject(Rect rect, Vector3 world) => rect.center + new Vector2(world.x - _canvasCenter.x, -(world.z - _canvasCenter.y)) * _canvasScale;
    private Vector3 CanvasWorld(Rect rect, Vector2 pixel, float height = 0) => new Vector3(_canvasCenter.x + (pixel.x - rect.center.x) / _canvasScale,
        height, _canvasCenter.y - (pixel.y - rect.center.y) / _canvasScale);
    private void FrameCanvas(Rect rect, JObject root)
    {
        Bounds bounds = LoopLevelBoardLayout.BoardBounds(A.Layout(root), true);
        _canvasCenter = new Vector2(bounds.center.x, bounds.center.z);
        _canvasScale = Mathf.Clamp(Mathf.Min((rect.width - 48) / Mathf.Max(1, bounds.size.x), (rect.height - 48) / Mathf.Max(1, bounds.size.z)), 2, 150);
        _canvasFramed = true;
    }
    private static Vector3 CanvasCubePosition(int type, int index) => type == 1 ? Vector3.zero : new Vector3(0, 0, (type == 2 ? -2.8f : -1.2f) + index * .8f);
    private static Vector3 CanvasBoxPoint(JObject box, Vector3 local) => A.Vector(box, "Position") +
        (A.Int(box, "BoxType") == 1 ? Quaternion.identity : Quaternion.Euler(A.Vector(box, "Rotation"))) * local;

    private void DrawCanvas(Rect rect, JObject root)
    {
        _canvasRect = rect;
        _canvasWindowOffset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
        if (rect.width < 10 || rect.height < 10) return;
        if (!_canvasFramed && Event.current.type == EventType.Repaint) FrameCanvas(rect, root);
        _canvasControl = GUIUtility.GetControlID("LoopMasterCanvas".GetHashCode(), FocusType.Keyboard, rect);
        HandleCanvasInput(rect, root);
        if (Event.current.type != EventType.Repaint) return;
        GUI.BeginClip(rect); Rect local = new Rect(Vector2.zero, rect.size);
        EditorGUI.DrawRect(local, new Color(.075f, .095f, .125f)); Handles.BeginGUI();
        try
        {
            Func<Vector3, Vector3> project = p => (Vector3)(CanvasProject(rect, p) - rect.position);
            if (_snap > 0 && _snap * _canvasScale >= 8)
            {
                Vector3 min = CanvasWorld(rect, new Vector2(rect.xMin, rect.yMax)), max = CanvasWorld(rect, new Vector2(rect.xMax, rect.yMin));
                Handles.color = new Color(.18f, .21f, .26f);
                for (float x = Mathf.Ceil(min.x / _snap) * _snap; x <= max.x; x += _snap)
                    Handles.DrawLine(project(new Vector3(x, 0, min.z)), project(new Vector3(x, 0, max.z)));
                for (float z = Mathf.Ceil(min.z / _snap) * _snap; z <= max.z; z += _snap)
                    Handles.DrawLine(project(new Vector3(min.x, 0, z)), project(new Vector3(max.x, 0, z)));
            }
            JObject layout = A.Layout(root); JArray paths = A.Array(layout, "Paths"), boxes = A.Array(layout, "Boxes");
            for (int p = 0; p < paths.Count; p++)
            {
                Vector3[] sampled = LoopLevelBoardLayout.SampleRoad((JObject)paths[p]);
                if (sampled.Length < 2) continue;
                Handles.color = new Color(.26f, .37f, .50f); Handles.DrawAAPolyLine(Mathf.Max(3, _canvasScale), sampled.Select(project).ToArray());
                Handles.color = Color.cyan; Handles.DrawAAPolyLine(1.5f, sampled.Select(project).ToArray());
                if (_canvasTool != 6) continue;
                JArray points = A.Array((JObject)paths[p], "ControlPoints");
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 pos = project(A.Vector((JObject)points[i], "Position"));
                    Handles.color = _path == p && _point == i ? Color.yellow : Color.cyan;
                    Handles.DrawSolidDisc(pos, Vector3.forward, 4);
                    if (_path != p || _point != i || !_showTangents) continue;
                    foreach (string field in new[] { "InTangent", "OutTangent" })
                    {
                        Vector3 tangent = project(A.Vector((JObject)points[i], field));
                        Handles.color = Color.green; Handles.DrawLine(pos, tangent); Handles.DrawSolidDisc(tangent, Vector3.forward, 4);
                    }
                }
            }
            foreach (JObject group in A.Array(layout, "Groups"))
            {
                int[] members = A.Array(group, "BoxIndexes").Select(t => t.Value<int>()).Where(i => i >= 0 && i < boxes.Count).ToArray();
                if (members.Length == 0 || !members.Any(_canvasSelection.Contains)) continue;
                Vector3 target = project(A.Vector((JObject)boxes[members[0]], "Position")); Handles.color = new Color(1, .65f, .2f);
                foreach (int i in members.Skip(1)) Handles.DrawDottedLine(target, project(A.Vector((JObject)boxes[i], "Position")), 4);
                foreach (JObject b in boxes)
                    foreach (JObject c in A.Array(b, "Cubes"))
                        if (A.Int(c, "GroupKeyId") == A.Int(group, "Id")) Handles.DrawDottedLine(target, project(A.Vector(b, "Position")), 4);
            }
            Puzzle puzzle = ExportedPuzzleJsonDecoder.Decode(root.ToString(Formatting.None));
            for (int b = 0; b < boxes.Count; b++)
            {
                JObject box = (JObject)boxes[b]; int type = A.Int(box, "BoxType");
                Vector3[] corners = LoopLevelBoardLayout.Corners(box);
                Vector3[] outline = new[] { corners[0], corners[1], corners[5], corners[4] }.Select(project).ToArray();
                Handles.color = _canvasSelection.Contains(b) ? new Color(.27f, .33f, .2f) : new Color(.16f, .19f, .24f);
                Handles.DrawAAConvexPolygon(outline);
                Handles.color = _canvasSelection.Contains(b) ? Color.yellow : new Color(.65f, .72f, .8f);
                Handles.DrawAAPolyLine(2, outline.Concat(new[] { outline[0] }).ToArray());
                JArray cubes = A.Array(box, "Cubes"); int visible = type == 1 ? Math.Min(1, cubes.Count) : cubes.Count;
                for (int c = 0; c < visible; c++)
                {
                    int index = type == 1 ? cubes.Count - 1 : c; JObject cube = (JObject)cubes[index];
                    Vector3 cp = CanvasCubePosition(type, c);
                    var polygon = new[] { new Vector3(-.35f,0,-.35f), new Vector3(.35f,0,-.35f), new Vector3(.35f,0,.35f), new Vector3(-.35f,0,.35f) }
                        .Select(v => project(CanvasBoxPoint(box, cp + v))).ToArray();
                    bool hidden = A.Int(cube, "IsSecret") > 0;
                    Handles.color = hidden && _canvasPlayerView ? Color.gray : A.ColorFor(A.Int(cube, "Type")); Handles.DrawAAConvexPolygon(polygon);
                    Vector3 center = project(CanvasBoxPoint(box, cp));
                    string text = (hidden ? "?" : "") + (A.Int(cube, "GroupKeyId") > 0 && !(hidden && _canvasPlayerView) ? "K" + A.Int(cube, "GroupKeyId") : "");
                    if (text.Length > 0) GUI.Label(new Rect(center.x - 12, center.y - 8, 40, 20), text, EditorStyles.whiteMiniLabel);
                }
                if (_canvasTool == 2 && cubes.Count < A.CubeCapacity(box))
                {
                    Vector3 plus = project(CanvasBoxPoint(box, type == 1 ? new Vector3(.75f,0,0) : CanvasCubePosition(type, cubes.Count)));
                    GUI.Label(new Rect(plus.x - 7, plus.y - 9, 20, 20), "+", EditorStyles.whiteBoldLabel);
                }
                Vector3 centerBox = project(A.Vector(box, "Position"));
                string badge = "H" + b + (type == 1 ? " · Q" + cubes.Count : "");
                if (A.Int(box, "LockColor") > 0) badge += " M" + A.Int(box, "LockColor");
                if (A.Int(box, "NumLock") > 0) badge += " N" + A.Int(box, "NumLock");
                if (A.Int(box, "IsEnterOnly") > 0) badge += " Vào";
                if (A.Int(box, "LimitedColor") > 0) badge += " C" + A.Int(box, "LimitedColor");
                GUI.Label(new Rect(centerBox.x + 6, centerBox.y - 10, 140, 22), badge, EditorStyles.whiteMiniLabel);
                Vector3 exit = CanvasBoxPoint(box, new Vector3(0,0,type == 1 ? -.2f : 2.2f));
                Handles.color = Color.cyan; Handles.DrawLine(project(CanvasBoxPoint(box, new Vector3(0,0,1.8f))), project(exit));
                if (_canvasSelection.Contains(b))
                {
                    if (A.Int(box, "IsAdjacentLocked") > 0)
                    {
                        Handles.color = Color.magenta;
                        foreach (int source in AdjacentLockRules.GetSourceIndexes(puzzle.Boxes, b)) Handles.DrawDottedLine(centerBox, project(A.Vector((JObject)boxes[source], "Position")), 3);
                    }
                    List<Vector3> slots = _canvasDraft == null && _canvasProposal == null ? _slots : A.Slots(root);
                    int binding = A.Int(box, "PathIndexV2"); if (binding <= 0) binding = A.NearestSlot(box, slots);
                    if (_showBindings && binding > 0 && binding <= slots.Count)
                    { Handles.color = Color.yellow; Handles.DrawDottedLine(project(exit), project(slots[binding - 1]), 3); }
                }
            }
            if (_drawingRoad && _sketchPoints.Count > 0)
            {
                Handles.color = Color.green;
                Vector3[] sketch = CanFinishSketch ? LoopLevelBoardLayout.SampleRoad(A.RoadFromPositions(_sketchPoints, _sketchClosed, _sketchSmooth ? _roadSmoothing : 0)) : _sketchPoints.ToArray();
                if (sketch.Length > 1) Handles.DrawAAPolyLine(3, sketch.Select(project).ToArray());
            }
            if (_canvasGesture == 3)
            {
                Rect selection = _canvasMarquee; selection.position -= rect.position;
                EditorGUI.DrawRect(selection, new Color(.3f,.65f,1,.15f));
                Handles.color = Color.cyan; Handles.DrawAAPolyLine(1, new Vector3(selection.xMin,selection.yMin), new Vector3(selection.xMax,selection.yMin), new Vector3(selection.xMax,selection.yMax), new Vector3(selection.xMin,selection.yMax), new Vector3(selection.xMin,selection.yMin));
            }
        }
        finally { Handles.EndGUI(); GUI.EndClip(); }
    }

    private int HitCanvasBox(Rect rect, JObject root, Vector2 mouse, out int cube)
    {
        Vector3 world = CanvasWorld(rect, mouse); JArray boxes = A.Array(A.Layout(root), "Boxes"); cube = -1;
        for (int b = boxes.Count - 1; b >= 0; b--)
        {
            JObject box = (JObject)boxes[b]; int type = A.Int(box, "BoxType");
            Quaternion rotation = type == 1 ? Quaternion.identity : Quaternion.Euler(A.Vector(box, "Rotation"));
            Vector3 local = Quaternion.Inverse(rotation) * (world - A.Vector(box, "Position"));
            JArray cubes = A.Array(box, "Cubes"); int count = type == 1 ? Math.Min(1, cubes.Count) : cubes.Count;
            for (int c = 0; c < count; c++)
            {
                Vector3 center = CanvasCubePosition(type, c);
                if (Mathf.Abs(local.x-center.x) <= .4f && Mathf.Abs(local.z-center.z) <= .4f) { cube = type == 1 ? cubes.Count-1 : c; return b; }
            }
            Vector3 append = type == 1 ? new Vector3(.75f,0,0) : CanvasCubePosition(type, cubes.Count);
            if (_canvasTool == 2 && cubes.Count < A.CubeCapacity(box) && Mathf.Abs(local.x-append.x) <= .35f && Mathf.Abs(local.z-append.z) <= .35f)
            { cube = cubes.Count; return b; }
            Bounds footprint = LoopLevelBoardLayout.LocalBounds(type);
            if (local.x >= footprint.min.x && local.x <= footprint.max.x && local.z >= footprint.min.z && local.z <= footprint.max.z) return b;
        }
        return -1;
    }
    private bool HitCanvasPoint(Rect rect, JObject root, Vector2 mouse)
    {
        JArray paths = A.Array(A.Layout(root), "Paths"); float nearest = 100;
        bool found = false;
        for (int p = 0; p < paths.Count; p++)
        {
            JArray points = A.Array((JObject)paths[p], "ControlPoints");
            for (int i = 0; i < points.Count; i++)
                foreach (string field in _showTangents && _path == p && _point == i ? new[] { "Position", "InTangent", "OutTangent" } : new[] { "Position" })
                {
                    float d = (CanvasProject(rect,A.Vector((JObject)points[i],field))-mouse).sqrMagnitude;
                    if (d < nearest) { nearest = d; _canvasPointPath = p; _canvasPointIndex = i; _canvasPointField = field; found = true; }
                }
        }
        if (found) { _path = _canvasPointPath; _point = _canvasPointIndex; }
        return found;
    }
    private void SplitCanvasRoad(Rect rect, JObject root, Vector2 mouse)
    {
        int bestPath=-1, bestSegment=-1; float bestT=0, nearest=144;
        JArray paths = A.Array(A.Layout(root), "Paths");
        for (int p=0;p<paths.Count;p++)
        {
            JArray points=A.Array((JObject)paths[p],"ControlPoints");
            if (points.Count>=128) continue;
            for(int s=0;s<points.Count-1;s++)
            {
                JObject a=(JObject)points[s], b=(JObject)points[s+1];
                for(int n=1;n<40;n++)
                {
                    float t=n/40f,u=1-t;
                    Vector3 point=u*u*u*A.Vector(a,"Position")+3*u*u*t*A.Vector(a,"OutTangent")+3*u*t*t*A.Vector(b,"InTangent")+t*t*t*A.Vector(b,"Position");
                    float d=(CanvasProject(rect,point)-mouse).sqrMagnitude;
                    if(d<nearest){ nearest=d;bestPath=p;bestSegment=s;bestT=t; }
                }
            }
        }
        if(bestPath<0) return;
        WorkspaceCommand("Chèn điểm giữ hình đường", next=> { _path=bestPath; _point=A.SplitSegment(A.Array((JObject)A.Array(A.Layout(next),"Paths")[bestPath],"ControlPoints"),bestSegment,bestT); });
    }

    private void BeginCanvasGesture(int kind, JObject root, Rect rect, Vector2 mouse)
    {
        _canvasGesture = kind; _canvasBase = _document.Json; _canvasDraft = (JObject)root.DeepClone();
        _canvasDown = mouse; _canvasWorldDown = CanvasWorld(rect,mouse); _canvasPanStart=_canvasCenter;
        _canvasStroke.Clear(); GUIUtility.hotControl=_canvasControl;
    }
    private void PaintCanvas(Rect rect, Vector2 mouse)
    {
        int b=HitCanvasBox(rect,_canvasDraft,mouse,out int c); if(b<0||c<0) return;
        string key=b+":"+c;
        // Erasure only removes one cube per box in a stroke, preventing shifted indexes being erased again.
        if(!_canvasStroke.Add(_canvasTool==5 ? b.ToString() : key)) return;
        JObject box=(JObject)A.Array(A.Layout(_canvasDraft),"Boxes")[b]; SetCanvasSelection(new[]{b});
        if(_canvasTool==5){ if(c<A.Array(box,"Cubes").Count) A.RemoveCube(box,c); }
        else A.ApplyCubeBrush(box,c,_brushColor,_canvasTool==3?1:_canvasTool==4?2:0,_keyBrush);
    }
    private void EndCanvasGesture()
    {
        JObject next=_canvasDraft; int gesture=_canvasGesture; string baseline=_canvasBase;
        CancelCanvasGesture();
        if(gesture==2||gesture==3||next==null) return;
        if(baseline!=_document.Json){ _status="Bản nháp đã thay đổi; thao tác kéo được hủy."; return; }
        try
        {
            if(gesture==1) LoopLevelCommands.RemapBindings(A.Parse(baseline),next,_canvasSelection);
            Undo.IncrementCurrentGroup(); Commit(next,gesture==4?"Tô khối trên bàn":gesture==5?"Kéo điểm đường":"Di chuyển cụm hộp"); Undo.IncrementCurrentGroup();
        }
        catch(Exception ex){_status=ex.Message;}
    }
    private void HandleCanvasInput(Rect rect, JObject root)
    {
        Event e=Event.current; bool inside=rect.Contains(e.mousePosition);
        if (_canvasProposal != null) return;
        if(e.type==EventType.ScrollWheel&&inside)
        {
            if (_canvasGesture != 0) CancelCanvasGesture();
            Vector3 anchor=CanvasWorld(rect,e.mousePosition); _canvasScale=Mathf.Clamp(_canvasScale*Mathf.Pow(1.1f,-e.delta.y),2,180);
            Vector3 after=CanvasWorld(rect,e.mousePosition); _canvasCenter+=new Vector2(anchor.x-after.x,anchor.z-after.z);e.Use();Repaint();return;
        }
        if(e.type==EventType.KeyDown&&GUIUtility.keyboardControl==_canvasFocus&&!EditorGUIUtility.editingTextField)
        {
            if(e.keyCode==KeyCode.Escape){CancelCanvasGesture();if(_drawingRoad)CancelRoadSketch();else _canvasTool=0;e.Use();GUIUtility.ExitGUI();return;}
            if(_drawingRoad){if(e.keyCode==KeyCode.Return){FinishRoadSketch();e.Use();GUIUtility.ExitGUI();}else if(e.keyCode==KeyCode.Backspace){RemoveSketchPoint();e.Use();}return;}
            if(e.keyCode==KeyCode.Delete||e.keyCode==KeyCode.Backspace){WorkspaceCommand("Xóa hộp đã chọn",next=>{LoopLevelCommands.DeleteBoxes(next,_canvasSelection);ClearCanvasSelection();});e.Use();return;}
            if((e.control||e.command)&&e.keyCode==KeyCode.A){SetCanvasSelection(Enumerable.Range(0,A.Array(A.Layout(root),"Boxes").Count));e.Use();GUIUtility.ExitGUI();return;}
            if((e.control||e.command)&&e.keyCode==KeyCode.D){DuplicateWorkspace(false);e.Use();return;}
        }
        if(e.type==EventType.MouseDown&&inside)
        {
            _canvasFocus=_canvasControl;GUIUtility.keyboardControl=_canvasControl;
            if(e.button==2||(e.alt&&e.button==0)){BeginCanvasGesture(2,root,rect,e.mousePosition);e.Use();return;}
            if(e.button!=0) return;
            if(_drawingRoad){AddSketchPoint(Snap(CanvasWorld(rect,e.mousePosition,_roadCenter.y)));e.Use();GUIUtility.ExitGUI();return;}
            if(_canvasTool==6)
            {
                if(e.clickCount==2) SplitCanvasRoad(rect,root,e.mousePosition);
                else if(HitCanvasPoint(rect,root,e.mousePosition)) BeginCanvasGesture(5,root,rect,e.mousePosition);
                e.Use();Repaint();GUIUtility.ExitGUI();return;
            }
            if(_canvasTool==1)
            {
                Vector3 position=Snap(CanvasWorld(rect,e.mousePosition,_placementHeight));
                WorkspaceCommand("Đặt hộp",next=> {JObject box=A.NewBox(position);box["BoxType"]=_placementType;box["Rotation"]=A.Vector(new Vector3(0,_placementYaw,0));JArray boxes=A.Array(A.Layout(next),"Boxes");boxes.Add(box);SetCanvasSelection(new[]{boxes.Count-1});});e.Use();return;
            }
            if(_canvasTool>=2&&_canvasTool<=5){BeginCanvasGesture(4,root,rect,e.mousePosition);PaintCanvas(rect,e.mousePosition);e.Use();Repaint();GUIUtility.ExitGUI();return;}
            int hit=HitCanvasBox(rect,root,e.mousePosition,out _);
            if(hit>=0)
            {
                if(e.shift){var selected=new List<int>(_canvasSelection);if(!selected.Remove(hit))selected.Add(hit);SetCanvasSelection(selected);}
                else if(!_canvasSelection.Contains(hit)) SetCanvasSelection(new[]{hit});
                if(_canvasSelection.Count>0)BeginCanvasGesture(1,root,rect,e.mousePosition);
            }
            else {if(!e.shift)ClearCanvasSelection();BeginCanvasGesture(3,root,rect,e.mousePosition);_canvasMarquee=new Rect(e.mousePosition,Vector2.zero);}
            e.Use();Repaint();GUIUtility.ExitGUI();return;
        }
        if(e.type==EventType.MouseDrag&&_canvasGesture!=0&&GUIUtility.hotControl==_canvasControl)
        {
            if(_canvasGesture==2){Vector2 d=(e.mousePosition-_canvasDown)/_canvasScale;_canvasCenter=_canvasPanStart+new Vector2(-d.x,d.y);}
            else if(_canvasGesture==3)_canvasMarquee=Rect.MinMaxRect(Mathf.Min(_canvasDown.x,e.mousePosition.x),Mathf.Min(_canvasDown.y,e.mousePosition.y),Mathf.Max(_canvasDown.x,e.mousePosition.x),Mathf.Max(_canvasDown.y,e.mousePosition.y));
            else if(_canvasGesture==1)
            {
                _canvasDraft=A.Parse(_canvasBase);Vector3 d=CanvasWorld(rect,e.mousePosition)-_canvasWorldDown;
                if(_snap>0)d=Snap(d);LoopLevelCommands.Move(_canvasDraft,_canvasSelection,d);
            }
            else if(_canvasGesture==4&&inside)PaintCanvas(rect,e.mousePosition);
            else if(_canvasGesture==5)
            {
                _canvasDraft=A.Parse(_canvasBase);JObject path=(JObject)A.Array(A.Layout(_canvasDraft),"Paths")[_canvasPointPath];JArray points=A.Array(path,"ControlPoints");
                JObject point=(JObject)points[_canvasPointIndex];Vector3 initial=A.Vector(point,_canvasPointField);
                Vector3 moved=Snap(initial+CanvasWorld(rect,e.mousePosition)-_canvasWorldDown);
                if(_canvasPointField=="Position") A.MovePoint(points,_canvasPointIndex,moved);else point[_canvasPointField]=A.Vector(moved);
            }
            e.Use();Repaint();GUIUtility.ExitGUI();return;
        }
        if(e.rawType==EventType.MouseUp&&_canvasGesture!=0)
        {
            if(_canvasGesture==3)
            {
                JArray boxes=A.Array(A.Layout(root),"Boxes");var selected=new List<int>(_canvasSelection);
                for(int i=0;i<boxes.Count;i++)if(_canvasMarquee.Contains(CanvasProject(rect,A.Vector((JObject)boxes[i],"Position"))))selected.Add(i);
                SetCanvasSelection(selected);
            }
            EndCanvasGesture();e.Use();Repaint();GUIUtility.ExitGUI();
        }
    }

    private void DrawWorkspaceInspector(JObject root)
    {
        JObject layout=A.Layout(root);JArray boxes=A.Array(layout,"Boxes");
        if(_canvasTool==6)
        {
            Header("Đường — kéo điểm; bấm đúp đoạn để chèn");
            _showTangents=EditorGUILayout.ToggleLeft("Hiện tiếp tuyến của điểm chọn",_showTangents);
            _sketchClosed=EditorGUILayout.ToggleLeft("Vẽ vòng kín",_sketchClosed);
            _sketchSmooth=EditorGUILayout.ToggleLeft("Làm mượt đường mới",_sketchSmooth);
            if(!_drawingRoad){if(GUILayout.Button("Vẽ đường mới trên bàn")){CancelRoadSketch();_drawingRoad=true;_tab=2;_status="Bấm đặt điểm trên bàn; Enter hoàn tất, Backspace bỏ điểm, Esc hủy.";}}
            else using(new EditorGUILayout.HorizontalScope()) {using(new EditorGUI.DisabledScope(!CanFinishSketch))if(GUILayout.Button("Hoàn tất")){FinishRoadSketch();GUIUtility.ExitGUI();}if(GUILayout.Button("Hủy")){CancelRoadSketch();GUIUtility.ExitGUI();}}
            if(A.Array(layout,"Paths").Count>0)
            {
                _path=Mathf.Clamp(_path,0,A.Array(layout,"Paths").Count-1);
                _path=EditorGUILayout.Popup("Đường",_path,Enumerable.Range(0,A.Array(layout,"Paths").Count).Select(i=>"Đường "+i).ToArray());
                if(GUILayout.Button("Làm mượt đường chọn"))WorkspaceCommand("Làm mượt đường",next=>A.SmoothRoad((JObject)A.Array(A.Layout(next),"Paths")[_path],_roadSmoothing));
                if(GUILayout.Button("Xóa điểm chọn")&&_point>=0)WorkspaceCommand("Xóa điểm đường",next=>A.RemoveRoadPoint((JObject)A.Array(A.Layout(next),"Paths")[_path],_point));
                if(GUILayout.Button("Xóa đường chọn"))WorkspaceCommand("Xóa đường",next=> {A.Array(A.Layout(next),"Paths").RemoveAt(_path);_point=-1;});
            }
            return;
        }
        Header(_canvasSelection.Count+" hộp được chọn");
        if(_box<0||_box>=boxes.Count){EditorGUILayout.HelpBox("Bấm hộp để sửa. Shift hoặc quét vùng để chọn nhiều. Chọn Đặt hộp để dựng bố cục.",MessageType.None);return;}
        JObject editable=(JObject)root.DeepClone();JObject box=(JObject)A.Array(A.Layout(editable),"Boxes")[_box];
        Header("Hộp "+_box+" — "+BoxTypeLabel(A.Int(box,"BoxType")));
        Vector3 priorPosition=A.Vector(box,"Position"),priorRotation=A.Vector(box,"Rotation");
        VectorField(box,"Position");VectorField(box,"Rotation");
        int type=EditorGUILayout.Popup("Loại hộp",A.Int(box,"BoxType"),new[]{"Thường","Đơn","Dài"});if(type!=A.Int(box,"BoxType"))box["BoxType"]=type;
        Integer(box,"LockColor","Khóa màu");Integer(box,"NumLock","Khóa số");Integer(box,"LimitedColor","Chỉ nhận màu");
        Flag(box,"IsEnterOnly","Chỉ nhận vào");Flag(box,"IsAdjacentLocked","Khóa liền kề");
        bool pinned=A.Int(box,"PathIndexV2")>0;
        bool nextPinned=EditorGUILayout.Toggle("Gắn cố định với đường",pinned);
        if(nextPinned!=pinned)box["PathIndexV2"]=nextPinned?A.NearestSlot(box,_slots):0;
        if(nextPinned)Integer(box,"PathIndexV2","Ô đường (từ 1)");
        EditorGUILayout.LabelField(nextPinned?"Sửa đường: giữ vị trí điểm gắn gần nhất.":"Tự tìm ô gần cửa thả khi chạy game.",EditorStyles.wordWrappedMiniLabel);
        if(!JToken.DeepEquals(editable,root)&&_canvasGesture==0)
        {
            if(priorPosition!=A.Vector(box,"Position")||priorRotation!=A.Vector(box,"Rotation"))LoopLevelCommands.RemapBindings(root,editable,new[]{_box});
            Commit(editable,"Sửa thuộc tính hộp");
            GUIUtility.ExitGUI();
        }
        Header("Nhóm / chìa khóa");
        if(GUILayout.Button("Tạo nhóm từ vùng chọn"))WorkspaceCommand("Tạo nhóm",next=> {_keyBrush=LoopLevelCommands.CreateGroup(next,_canvasSelection,_brushColor);_canvasTool=4;});
        foreach(JObject group in A.Array(layout,"Groups").OfType<JObject>().Where(g=>A.Array(g,"BoxIndexes").Any(t=>_canvasSelection.Contains(t.Value<int>()))))
        {
            int id=A.Int(group,"Id");GUILayout.Label("Nhóm "+id+" · màu khóa "+A.Int(group,"LockedByColor"));
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Gán chìa khóa")){_keyBrush=id;_canvasTool=4;}
                if(GUILayout.Button("Xóa nhóm"))WorkspaceCommand("Xóa nhóm và chìa khóa",next=>LoopLevelCommands.DeleteGroup(A.Layout(next),id));
            }
        }
        if(A.Int(box,"IsAdjacentLocked")>0)
        {
            var sources=AdjacentLockRules.GetSourceIndexes(ExportedPuzzleJsonDecoder.Decode(root.ToString()).Boxes,_box);
            GUILayout.Label("Nguồn mở khóa: "+(sources.Count==0?"chưa có":string.Join(", ",sources)),EditorStyles.wordWrappedMiniLabel);
        }
    }

    private void DuplicateWorkspace(bool mechanisms)
    {
        WorkspaceCommand(mechanisms?"Nhân bản cụm cơ chế":"Nhân bản hộp (bỏ chìa khóa nhóm)",next=>SetCanvasSelection(LoopLevelCommands.Duplicate(next,_canvasSelection,Vector3.right*2,mechanisms)));
    }
    private void DrawWorkspaceOperations(JObject root)
    {
        using(new EditorGUILayout.HorizontalScope())
        {
            using(new EditorGUI.DisabledScope(_canvasSelection.Count==0||_canvasGesture!=0||_drawingRoad))
            {
                if(GUILayout.Button("Nhân bản"))DuplicateWorkspace(false);
                if(GUILayout.Button("Nhân bản nhóm + chìa"))DuplicateWorkspace(true);
                if(GUILayout.Button("Xóa hộp")){WorkspaceCommand("Xóa hộp",next=>{LoopLevelCommands.DeleteBoxes(next,_canvasSelection);ClearCanvasSelection();});}
                if(GUILayout.Button("Xoay cụm 90°"))WorkspaceCommand("Xoay cụm",next=>LoopLevelCommands.Rotate(next,_canvasSelection,90),true);
                if(GUILayout.Button("Phản chiếu X"))PreviewWorkspaceCommand("Phản chiếu cụm",next=>LoopLevelCommands.Mirror(next,_canvasSelection),true);
            }
        }
        using(new EditorGUILayout.HorizontalScope())
        {
            using(new EditorGUI.DisabledScope(_canvasSelection.Count<2))
            {
                if(GUILayout.Button("Căn hàng ngang"))WorkspaceCommand("Căn hàng ngang",next=>LoopLevelCommands.Align(next,_canvasSelection,false,false),true);
                if(GUILayout.Button("Căn cột"))WorkspaceCommand("Căn cột",next=>LoopLevelCommands.Align(next,_canvasSelection,true,false),true);
                if(GUILayout.Button("Giãn đều X"))WorkspaceCommand("Giãn đều X",next=>LoopLevelCommands.Align(next,_canvasSelection,true,true),true);
                if(GUILayout.Button("Giãn đều Z"))WorkspaceCommand("Giãn đều Z",next=>LoopLevelCommands.Align(next,_canvasSelection,false,true),true);
            }
        }
        using(new EditorGUILayout.HorizontalScope())
        {
            _swapColor=EditorGUILayout.IntField("Đổi màu đang chọn ↔",_swapColor,GUILayout.Width(205));
            if(GUILayout.Button("Đổi toàn level (cả khóa)"))WorkspaceCommand("Đổi bảng màu",next=>LoopLevelCommands.SwapColors(next,_brushColor,_swapColor));
        }
        using(new EditorGUILayout.HorizontalScope())
        {
            _variantSeed=EditorGUILayout.IntField("Seed",_variantSeed,GUILayout.Width(150));
            using(new EditorGUI.DisabledScope(_canvasSelection.Count<2))
                if(GUILayout.Button("Trộn khối vùng chọn"))WorkspaceCommand("Trộn nội dung, giữ bố cục / tổng màu",next=>LoopLevelCommands.ShuffleContents(next,_canvasSelection,_variantSeed));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(_canvasSelection.Count < 2))
                if (GUILayout.Button("Xem trước xếp hộp (giữ đường)")) PreviewWorkspaceCommand("Xếp hộp, giữ đường", next => LoopLevelCommands.ArrangeBoxesOnly(next, _canvasSelection, _generatorSettings ?? new LoopLevelGeneratorSettings(), _variantSeed), true);
            if (GUILayout.Button("Xem trước đường bao (giữ hộp)")) PreviewWorkspaceCommand("Thay đường bao quanh hộp", next => LoopLevelBoardLayout.FitRoadOnly(A.Layout(next), _generatorSettings ?? new LoopLevelGeneratorSettings()));
        }
    }

    private void DrawWorkspaceValidation(JObject root)
    {
        JObject layout=A.Layout(root);JArray boxes=A.Array(layout,"Boxes");
        var histogram=boxes.OfType<JObject>().SelectMany(b=>A.Array(b,"Cubes").OfType<JObject>()).GroupBy(c=>A.Int(c,"Type")).OrderBy(g=>g.Key);
        GUILayout.Label(boxes.Count+" hộp · "+A.Int(layout,"PathLength")+" nhóm sức chứa đường · màu: "+string.Join(" | ",histogram.Select(g=>g.Key+":"+g.Count())),EditorStyles.wordWrappedMiniLabel);
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("Kiểm tra cấu trúc")){_errors=A.Validate(root,out _warnings);_hasValidation=true;}
            _solverBudget=EditorGUILayout.IntField("Trạng thái tối đa",_solverBudget,GUILayout.Width(230));
            if(_draftSearch==null){if(GUILayout.Button("Tìm lời giải bản nháp"))StartDraftSolver(root);}
            else if(GUILayout.Button("Hủy tìm")){StopDraftSolver();_draftSolverStatus="Đã hủy; chưa kiểm chứng.";}
        }
        if(_hasValidation)foreach(string error in _errors.Take(5))EditorGUILayout.HelpBox(error,MessageType.Error);
        foreach(string warning in LoopLevelCommands.DesignWarnings(_canvasProposal??_canvasDraft??root).Take(3))
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(warning,MessageType.Warning);
                var match=System.Text.RegularExpressions.Regex.Match(warning,@"^Hộp (\d+):");
                if(match.Success&&GUILayout.Button("Chọn",GUILayout.Width(48)))SetCanvasSelection(new[]{int.Parse(match.Groups[1].Value)});
            }
        }
        if(A.Array(layout,"MapData").Count>0)GUILayout.Label("MapData được giữ nguyên; kiểm tra trang trí và camera trong Game View.",EditorStyles.wordWrappedMiniLabel);
        if(!string.IsNullOrEmpty(_draftSolverStatus))GUILayout.Label(_draftSolverJson==_document.Json?_draftSolverStatus:"Bản nháp đã đổi — cần tìm lời giải lại.",EditorStyles.wordWrappedMiniLabel);
    }
    private void StartDraftSolver(JObject root)
    {
        StopDraftSolver();List<string> errors=A.Validate(root,out _);if(errors.Count>0){_status=string.Join("\n",errors.Take(3));return;}
        JObject layout=A.Layout(root);
        if(A.Array(layout,"Covers").Count>0||A.Array(layout,"Boxes").OfType<JObject>().Any(b=>A.Array(b,"LinkedCubes").Count>0||A.Array(b,"MultiColorLock").Count>0||A.Array(b,"Cubes").OfType<JObject>().Any(c=>A.Int(c,"IsFixed")>0)))
        {_draftSolverJson=_document.Json;_draftSolverStatus="Có cơ chế chưa được kiểm chứng bởi mô phỏng này. Hãy chơi thử.";return;}
        _draftSolverJson=_document.Json;_draftSolverStatus="Đang tìm…";
        _draftSimulation=LoopLevelGenerator.DecodeDetached(root);_draftSolver=new DebugLevelSolver(_draftSimulation,Mathf.Clamp(_solverBudget,100,50000));
        _draftSearch=_draftSolver.Search();EditorApplication.update+=TickDraftSolver;
    }
    private void StopDraftSolver()
    {
        if (_draftSearch != null && _draftSolverStatus != null && _draftSolverStatus.StartsWith("Đang tìm")) _draftSolverStatus = "Đã dừng tìm; chưa kiểm chứng.";
        EditorApplication.update-=TickDraftSolver;(_draftSearch as IDisposable)?.Dispose();_draftSearch=null;_draftSimulation=null;
    }
    private void TickDraftSolver()
    {
        if(_draftSearch==null)return;
        if(_document==null||_document.Json!=_draftSolverJson){StopDraftSolver();_draftSolverStatus="Bản nháp đã đổi; đã hủy tìm.";return;}
        try
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();bool running=true;
            do{running=LoopLevelGenerator.InSimulation(_draftSimulation,()=>_draftSearch.MoveNext());}while(running&&timer.Elapsed.TotalMilliseconds<8);
            _draftSolverStatus="Đang tìm: "+_draftSolver.Explored+" trạng thái.";
            if(!running)
            {
                List<DebugLevelSolver.Step> solution=_draftSolver.Solution;
                _draftSolverStatus=solution==null?"Chưa tìm thấy trong giới hạn; không kết luận vô nghiệm.":"Đã tìm thấy "+solution.Count+" lần bấm trong mô phỏng. Cần chơi thử để đánh giá.";
                if(solution!=null)
                {
                    GameData replay=_draftSimulation;
                    foreach(var step in solution){GameData current=replay;replay=LoopLevelGenerator.InSimulation(current,()=>DebugLevelSolver.SimulateClick(current,step.BoxIndex));if(replay==null)break;}
                    if(replay==null||!replay.IsGameOver())_draftSolverStatus="Lời giải không replay được; chưa kiểm chứng.";
                    else _draftSolverStatus+=" Hộp: "+string.Join(" → ",solution.Select(s=>s.BoxIndex));
                }
                StopDraftSolver();
            }
        }
        catch(Exception ex){StopDraftSolver();_draftSolverStatus="Không thể kiểm chứng: "+ex.Message;}
        Repaint();
    }
}
#endif
