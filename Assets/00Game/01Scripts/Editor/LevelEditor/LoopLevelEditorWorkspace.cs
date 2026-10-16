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
    [SerializeField] private bool _canvasReplaceRoad = true;
    [SerializeField] private List<int> _canvasSelection = new List<int>();
    [SerializeField] private Vector2 _canvasCenter, _inspectorScroll, _tableScroll;
    [SerializeField] private float _canvasScale = 25;
    [SerializeField] private int _canvasTool, _swapColor = 2, _variantSeed = 12345, _solverBudget = 10000;
    [SerializeField] private bool _showAutoFill;
    [SerializeField] private int _autoFillColors = 4, _autoFillSecret = 20, _autoFillColorLocks, _autoFillNumLocks, _autoFillGroups, _autoFillEnterOnly, _autoFillSeed = 12345;
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
    private Vector3 _hoverSegmentPos;
    private bool _hasHoverSegment;
    private int _hoverSegmentPath = -1, _hoverSegmentIndex = -1;
    private float _hoverSegmentT;
    [SerializeField] private bool _showPointList = true;
    [SerializeField] private bool _showWorkspaceLayout = true;
    [SerializeField] private int _canvasMap = -1;
    private JObject _balanceRoot;
    private readonly SortedDictionary<int, int> _balanceColors = new SortedDictionary<int, int>();
    private int _initialMoves, _balanceTotal, _normalTargets, _longTargets, _normalDemand, _longDemand;
    private bool _initialComplete;
    private string _balanceError;
    private int _tableDragBox = -1, _tableDragCube = -1, _tableDragControl;
    private readonly HashSet<string> _canvasStroke = new HashSet<string>();
    private IEnumerator _draftSearch;
    private DebugLevelSolver _draftSolver;
    private GameData _draftSimulation;

    private void ClearCanvasSelection() { _canvasSelection.Clear(); _box = -1; _canvasMap = -1; }
    private void SetCanvasSelection(IEnumerable<int> indices)
    {
        _canvasMap = -1;
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
                GUILayout.Label("Alt+Click: hút màu · 1..9: đổi màu · Q/W/E/R: đổi tool · cuộn: zoom", EditorStyles.miniLabel);
            }
            DrawWorkspacePalette(layout);
            DrawLiveBalanceHUD(root);
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
                DrawRoadDirectionChevrons(project, sampled);

                var selfHits = A.FindSelfIntersections(sampled);
                for (int h = 0; h < selfHits.Count; h++)
                {
                    Vector3 hitCanvas = project(selfHits[h]);
                    Handles.color = new Color(1f, 0.35f, 0.1f, 0.85f);
                    Handles.DrawSolidDisc(hitCanvas, Vector3.forward, 8);
                    Handles.color = Color.yellow;
                    Handles.DrawWireDisc(hitCanvas, Vector3.forward, 8);
                    GUI.Label(new Rect(hitCanvas.x - 7, hitCanvas.y - 11, 16, 16), "⚠️", EditorStyles.boldLabel);
                    GUI.Label(new Rect(hitCanvas.x + 9, hitCanvas.y - 8, 90, 16), "Giao cắt phẳng!", EditorStyles.whiteMiniLabel);
                }

                if (_canvasTool != 6) continue;
                JArray points = A.Array((JObject)paths[p], "ControlPoints");
                bool repeated = points.Count > 1 && (A.Vector((JObject)points[0], "Position") - A.Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
                int anchorCount = points.Count - (repeated ? 1 : 0);
                for (int i = 0; i < anchorCount; i++)
                {
                    Vector3 worldPos = A.Vector((JObject)points[i], "Position");
                    Vector3 pos = project(worldPos);
                    bool isSelected = _path == p && _point == i;
                    bool isOverpass = worldPos.y > 0.6f;
                    if (isSelected)
                    {
                        Handles.color = new Color(1f, 0.9f, 0.2f, 0.35f);
                        Handles.DrawSolidDisc(pos, Vector3.forward, 11);
                        Handles.color = Color.yellow;
                        Handles.DrawWireDisc(pos, Vector3.forward, 11);
                    }
                    Handles.color = isSelected ? Color.yellow : (isOverpass ? new Color(0.9f, 0.4f, 1f) : (i == 0 ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.3f, 0.8f, 1f)));
                    Handles.DrawSolidDisc(pos, Vector3.forward, isOverpass ? 6.5f : 5.5f);
                    Handles.color = Color.black;
                    Handles.DrawWireDisc(pos, Vector3.forward, isOverpass ? 6.5f : 5.5f);
                    string ptLabel = (i == 0 ? "1 (Đầu)" : (i + 1).ToString()) + (isOverpass ? " [🌉" + worldPos.y.ToString("F1") + "m]" : "");
                    GUI.Label(new Rect(pos.x + 6, pos.y - 12, isOverpass ? 85 : 45, 18), ptLabel, EditorStyles.whiteMiniLabel);
                    if (!isSelected || !_showTangents) continue;
                    foreach (string field in new[] { "InTangent", "OutTangent" })
                    {
                        Vector3 tangent = project(A.Vector((JObject)points[i], field));
                        Handles.color = field == "InTangent" ? Color.magenta : Color.green;
                        Handles.DrawLine(pos, tangent); Handles.DrawSolidDisc(tangent, Vector3.forward, 4);
                    }
                }
                if (_hasHoverSegment && _hoverSegmentPath == p && _canvasGesture == 0 && !_drawingRoad)
                {
                    Vector3 ghost = project(_hoverSegmentPos);
                    Handles.color = new Color(1f, 0.9f, 0.2f, 0.85f);
                    Handles.DrawWireDisc(ghost, Vector3.forward, 8);
                    GUI.Label(new Rect(ghost.x - 5, ghost.y - 10, 20, 20), "+", EditorStyles.whiteBoldLabel);
                    GUI.Label(new Rect(ghost.x + 9, ghost.y - 8, 70, 16), "Chèn điểm", EditorStyles.whiteMiniLabel);
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
            DrawCanvasCapacities(rect, layout);
            if (_drawingRoad)
            {
                if (_sketchPoints.Count > 0)
                {
                    for (int i = 0; i < _sketchPoints.Count; i++)
                    {
                        Vector3 sp = project(_sketchPoints[i]);
                        Handles.color = i == 0 ? new Color(0.2f, 0.9f, 0.3f) : Color.yellow;
                        Handles.DrawSolidDisc(sp, Vector3.forward, 6);
                        Handles.color = Color.black;
                        Handles.DrawWireDisc(sp, Vector3.forward, 6);
                        GUI.Label(new Rect(sp.x + 7, sp.y - 10, 45, 18), i == 0 ? "1 (Đầu)" : (i + 1).ToString(), EditorStyles.whiteMiniLabel);
                    }
                    if (_sketchCursorValid)
                    {
                        Vector3 cp = project(_sketchCursor);
                        Vector3 last = project(_sketchPoints[_sketchPoints.Count - 1]);
                        Handles.color = new Color(0.3f, 1f, 0.5f, 0.8f);
                        Handles.DrawDottedLine(last, cp, 4);
                        Handles.color = Color.white;
                        Handles.DrawWireDisc(cp, Vector3.forward, 6);
                        GUI.Label(new Rect(cp.x + 8, cp.y - 10, 30, 18), (_sketchPoints.Count + 1).ToString(), EditorStyles.whiteMiniLabel);

                        bool nearStart = _sketchClosed && CanFinishSketch &&
                            (Vector2.Distance((Vector2)cp, (Vector2)project(_sketchPoints[0])) < 22f ||
                             (_sketchPoints[0] - _sketchCursor).sqrMagnitude < 0.36f);
                        if (nearStart)
                        {
                            Vector3 startP = project(_sketchPoints[0]);
                            Handles.color = Color.green;
                            Handles.DrawWireDisc(startP, Vector3.forward, 12);
                            Handles.DrawWireDisc(startP, Vector3.forward, 14);
                            GUI.Label(new Rect(startP.x - 30, startP.y - 26, 120, 18), "★ Bấm để khép kín", EditorStyles.whiteMiniLabel);
                        }
                        else if (_sketchClosed && _sketchPoints.Count >= 2)
                        {
                            Handles.color = new Color(0.3f, 0.7f, 1f, 0.5f);
                            Handles.DrawDottedLine(cp, project(_sketchPoints[0]), 4);
                        }
                    }
                    var previewList = new List<Vector3>(_sketchPoints);
                    if (_sketchCursorValid && (previewList[previewList.Count - 1] - _sketchCursor).sqrMagnitude > 0.001f)
                        previewList.Add(_sketchCursor);
                    if (previewList.Count >= 2)
                    {
                        try
                        {
                            Vector3[] sampledPreview = LoopLevelBoardLayout.SampleRoad(A.RoadFromPositions(previewList, false, _sketchSmooth ? _roadSmoothing : 0));
                            if (sampledPreview.Length > 1)
                            {
                                Handles.color = new Color(0.2f, 0.9f, 0.4f, 0.85f);
                                Handles.DrawAAPolyLine(2.5f, sampledPreview.Select(project).ToArray());
                            }
                        }
                        catch {}
                    }
                }
                Rect bar = new Rect(8, 8, local.width - 16, 26);
                EditorGUI.DrawRect(bar, new Color(0.12f, 0.16f, 0.22f, 0.92f));
                Handles.color = new Color(0.2f, 0.8f, 0.4f);
                Handles.DrawAAPolyLine(1.5f, new Vector3(bar.xMin, bar.yMax), new Vector3(bar.xMax, bar.yMax));
                GUI.Label(new Rect(bar.x + 8, bar.y + 4, Mathf.Max(100, bar.width - 230), 20),
                    "VẼ ĐƯỜNG (" + _sketchPoints.Count + " điểm): Click: đặt · Chuột phải: bỏ · Enter: xong · Esc: hủy", EditorStyles.whiteMiniLabel);
                float btnX = bar.xMax - 210;
                if (GUI.Button(new Rect(btnX, bar.y + 3, 65, 20), CanFinishSketch ? "✓ Xong" : "Thiếu", EditorStyles.miniButton))
                {
                    if (CanFinishSketch) { FinishRoadSketch(); GUIUtility.ExitGUI(); }
                }
                if (GUI.Button(new Rect(btnX + 70, bar.y + 3, 65, 20), "↶ Bỏ điểm", EditorStyles.miniButton))
                {
                    RemoveSketchPoint();
                }
                if (GUI.Button(new Rect(btnX + 140, bar.y + 3, 60, 20), "✕ Hủy", EditorStyles.miniButton))
                {
                    CancelRoadSketch(); GUIUtility.ExitGUI();
                }
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
        JArray paths = A.Array(A.Layout(root), "Paths"); float nearest = 196f;
        bool found = false;
        for (int p = 0; p < paths.Count; p++)
        {
            JArray points = A.Array((JObject)paths[p], "ControlPoints");
            bool repeated = points.Count > 1 && (A.Vector((JObject)points[0], "Position") - A.Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
            int count = points.Count - (repeated ? 1 : 0);
            for (int i = 0; i < count; i++)
                foreach (string field in _showTangents && _path == p && _point == i ? new[] { "Position", "InTangent", "OutTangent" } : new[] { "Position" })
                {
                    float d = (CanvasProject(rect, A.Vector((JObject)points[i], field)) - mouse).sqrMagnitude;
                    if (d < nearest) { nearest = d; _canvasPointPath = p; _canvasPointIndex = i; _canvasPointField = field; found = true; }
                }
        }
        if (found) { _path = _canvasPointPath; _point = _canvasPointIndex; }
        return found;
    }
    private bool FindCanvasRoadSegment(Rect rect, JObject root, Vector2 mouse, out int bestPath, out int bestSegment, out float bestT, out Vector3 bestPos)
    {
        bestPath = -1; bestSegment = -1; bestT = 0; bestPos = Vector3.zero; float nearest = 196f;
        JArray paths = A.Array(A.Layout(root), "Paths");
        for (int p = 0; p < paths.Count; p++)
        {
            JArray points = A.Array((JObject)paths[p], "ControlPoints");
            if (points.Count >= 128) continue;
            for (int s = 0; s < points.Count - 1; s++)
            {
                JObject a = (JObject)points[s], b = (JObject)points[s + 1];
                Vector3 p0 = A.Vector(a, "Position"), p1 = A.Vector(a, "OutTangent");
                Vector3 p2 = A.Vector(b, "InTangent"), p3 = A.Vector(b, "Position");
                for (int n = 1; n < 40; n++)
                {
                    float t = n / 40f, u = 1 - t;
                    Vector3 point = u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
                    float d = (CanvasProject(rect, point) - mouse).sqrMagnitude;
                    if (d < nearest) { nearest = d; bestPath = p; bestSegment = s; bestT = t; bestPos = point; }
                }
            }
        }
        return bestPath >= 0;
    }
    private void SplitCanvasRoad(Rect rect, JObject root, Vector2 mouse)
    {
        if (!FindCanvasRoadSegment(rect, root, mouse, out int bestPath, out int bestSegment, out float bestT, out _)) return;
        WorkspaceCommand("Chèn điểm giữ hình đường", next =>
        {
            _path = bestPath;
            JArray pts = A.Array((JObject)A.Array(A.Layout(next), "Paths")[bestPath], "ControlPoints");
            _point = A.SplitSegment(pts, bestSegment, bestT);
            if (_autoSmoothPoints) A.SmoothRoad((JObject)A.Array(A.Layout(next), "Paths")[bestPath], _roadSmoothing);
        });
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
            if(gesture==5 && _autoSmoothPoints && _canvasPointPath>=0)
            {
                JArray paths = A.Array(A.Layout(next), "Paths");
                if (_canvasPointPath < paths.Count)
                    A.SmoothRoad((JObject)paths[_canvasPointPath], _roadSmoothing);
            }
            string action = "Di chuyển cụm hộp";
            if (gesture == 4) action = "Tô khối trên bàn";
            else if (gesture == 5) action = "Kéo điểm đường";
            else if (gesture == 6) action = "Di chuyển Capacity";
            Undo.IncrementCurrentGroup(); Commit(next, action); Undo.IncrementCurrentGroup();
        }
        catch(Exception ex){_status=ex.Message;}
    }
    private void HandleCanvasInput(Rect rect, JObject root)
    {
        Event e=Event.current; bool inside=rect.Contains(e.mousePosition);
        if (_canvasProposal != null) return;
        if (inside)
        {
            if (_drawingRoad)
            {
                _sketchCursorValid = true;
                _sketchCursor = Snap(CanvasWorld(rect, e.mousePosition, _roadCenter.y));
                if (e.type == EventType.MouseMove) Repaint();
            }
            else if (_canvasTool == 6 && _canvasGesture == 0)
            {
                bool prevHover = _hasHoverSegment;
                Vector3 prevPos = _hoverSegmentPos;
                _hasHoverSegment = FindCanvasRoadSegment(rect, root, e.mousePosition, out _hoverSegmentPath, out _hoverSegmentIndex, out _hoverSegmentT, out _hoverSegmentPos);
                if (e.type == EventType.MouseMove && (_hasHoverSegment != prevHover || (_hasHoverSegment && (_hoverSegmentPos - prevPos).sqrMagnitude > 0.01f)))
                    Repaint();
            }
        }
        else if (_hasHoverSegment)
        {
            _hasHoverSegment = false;
            Repaint();
        }
        if(e.type==EventType.ScrollWheel&&inside)
        {
            if (_canvasGesture != 0) CancelCanvasGesture();
            Vector3 anchor=CanvasWorld(rect,e.mousePosition); _canvasScale=Mathf.Clamp(_canvasScale*Mathf.Pow(1.1f,-e.delta.y),2,180);
            Vector3 after=CanvasWorld(rect,e.mousePosition); _canvasCenter+=new Vector2(anchor.x-after.x,anchor.z-after.z);e.Use();Repaint();return;
        }
        if(e.type==EventType.KeyDown&&GUIUtility.keyboardControl==_canvasFocus&&!EditorGUIUtility.editingTextField)
        {
            if(e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha9)
            {
                _brushColor=e.keyCode-KeyCode.Alpha0; _status="Đã chọn màu cọ: "+_brushColor;
                e.Use();Repaint();return;
            }
            if(e.keyCode==KeyCode.Q){_canvasTool=0;_status="Công cụ: Chọn / Di chuyển";e.Use();Repaint();return;}
            if(e.keyCode==KeyCode.W){_canvasTool=1;_status="Công cụ: Đặt hộp";e.Use();Repaint();return;}
            if(e.keyCode==KeyCode.E){_canvasTool=2;_status="Công cụ: Tô khối";e.Use();Repaint();return;}
            if(e.keyCode==KeyCode.R){_canvasTool=3;_status="Công cụ: Ẩn / Hiện";e.Use();Repaint();return;}
            if(e.keyCode==KeyCode.T){_canvasTool=6;_status="Công cụ: Sửa đường";e.Use();Repaint();return;}
            if(e.keyCode==KeyCode.Escape)
            {
                CancelCanvasGesture();
                if(_drawingRoad)CancelRoadSketch();
                else if(_canvasTool==6 && _point>=0){_point=-1;Repaint();}
                else _canvasTool=0;
                e.Use();GUIUtility.ExitGUI();return;
            }
            if(_drawingRoad)
            {
                if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter){FinishRoadSketch();e.Use();GUIUtility.ExitGUI();return;}
                else if(e.keyCode==KeyCode.Backspace){RemoveSketchPoint();e.Use();return;}
            }
            if(_canvasTool==6 && _point>=0)
            {
                if(e.keyCode==KeyCode.Delete||e.keyCode==KeyCode.Backspace)
                {
                    JArray paths=A.Array(A.Layout(root),"Paths");
                    if(_path>=0&&_path<paths.Count)
                    {
                        WorkspaceCommand("Xóa điểm đường",next=>
                        {
                            JObject p=(JObject)A.Array(A.Layout(next),"Paths")[_path];
                            A.RemoveRoadPoint(p,_point);
                            _point=Math.Min(_point,A.Array(p,"ControlPoints").Count-1);
                            if(_autoSmoothPoints)A.SmoothRoad(p,_roadSmoothing);
                        });
                    }
                    e.Use();return;
                }
            }
            if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && LoopLevelCommands.IsCapacity(A.Layout(root), _canvasMap))
            { WorkspaceCommand("Xóa Capacity", DeleteSelectedCapacity); e.Use(); return; }
            if(e.keyCode==KeyCode.Delete||e.keyCode==KeyCode.Backspace){WorkspaceCommand("Xóa hộp đã chọn",next=>{LoopLevelCommands.DeleteBoxes(next,_canvasSelection);ClearCanvasSelection();});e.Use();return;}
            if((e.control||e.command)&&e.keyCode==KeyCode.A){SetCanvasSelection(Enumerable.Range(0,A.Array(A.Layout(root),"Boxes").Count));e.Use();GUIUtility.ExitGUI();return;}
            if((e.control||e.command)&&e.keyCode==KeyCode.D){DuplicateWorkspace(false);e.Use();return;}
        }
        if(e.type==EventType.MouseDown&&inside)
        {
            _canvasFocus=_canvasControl;GUIUtility.keyboardControl=_canvasControl;
            if(e.button==2||(e.alt&&e.button==0))
            {
                if(e.alt&&e.button==0)
                {
                    int pickBox=HitCanvasBox(rect,root,e.mousePosition,out int pickCube);
                    if(pickBox>=0&&pickCube>=0)
                    {
                        JArray pickCubes=A.Array((JObject)A.Array(A.Layout(root),"Boxes")[pickBox],"Cubes");
                        if(pickCube<pickCubes.Count)
                        {
                            _brushColor=A.Int((JObject)pickCubes[pickCube],"Type");
                            _status="Hút màu cọ: "+_brushColor;
                            e.Use();Repaint();return;
                        }
                    }
                }
                BeginCanvasGesture(2,root,rect,e.mousePosition);e.Use();return;
            }
            if(_drawingRoad)
            {
                if(e.button==0)
                {
                    bool close=_sketchClosed&&CanFinishSketch&&_sketchPoints.Count>0&&
                        (Vector2.Distance(e.mousePosition,CanvasProject(rect,_sketchPoints[0]))<22f||
                         (_sketchPoints[0]-_sketchCursor).sqrMagnitude<0.36f);
                    if(close)FinishRoadSketch();
                    else AddSketchPoint(Snap(CanvasWorld(rect,e.mousePosition,_roadCenter.y)));
                    e.Use();GUIUtility.ExitGUI();return;
                }
                if(e.button==1)
                {
                    if(_sketchPoints.Count>0)RemoveSketchPoint();
                    else CancelRoadSketch();
                    e.Use();Repaint();return;
                }
                return;
            }
            if(_canvasTool==6)
            {
                if(e.button==1)
                {
                    if(HitCanvasPoint(rect,root,e.mousePosition))
                    {
                        ShowRoadPointContextMenu(root,_canvasPointPath,_canvasPointIndex);
                        e.Use();return;
                    }
                }
                else if(e.button==0)
                {
                    if(HitCanvasPoint(rect,root,e.mousePosition))
                    {
                        BeginCanvasGesture(5,root,rect,e.mousePosition);
                        e.Use();Repaint();GUIUtility.ExitGUI();return;
                    }
                    if(FindCanvasRoadSegment(rect,root,e.mousePosition,out int bPath,out int bSeg,out float bT,out _))
                    {
                        WorkspaceCommand("Chèn điểm trên đường",next=>
                        {
                            _path=bPath;
                            JArray pts=A.Array((JObject)A.Array(A.Layout(next),"Paths")[bPath],"ControlPoints");
                            _point=A.SplitSegment(pts,bSeg,bT);
                            if(_autoSmoothPoints)A.SmoothRoad((JObject)A.Array(A.Layout(next),"Paths")[bPath],_roadSmoothing);
                        });
                        e.Use();Repaint();GUIUtility.ExitGUI();return;
                    }
                    if(_point>=0){_point=-1;Repaint();}
                }
                return;
            }
            if(e.button!=0) return;
            if(_canvasTool==1)
            {
                Vector3 position=Snap(CanvasWorld(rect,e.mousePosition,_placementHeight));
                WorkspaceCommand("Đặt hộp",next=> {JObject box=A.NewBox(position);box["BoxType"]=_placementType;box["Rotation"]=A.Vector(new Vector3(0,_placementYaw,0));JArray boxes=A.Array(A.Layout(next),"Boxes");boxes.Add(box);SetCanvasSelection(new[]{boxes.Count-1});});e.Use();return;
            }
            if(_canvasTool>=2&&_canvasTool<=5){BeginCanvasGesture(4,root,rect,e.mousePosition);PaintCanvas(rect,e.mousePosition);e.Use();Repaint();GUIUtility.ExitGUI();return;}
            int mapHit = HitCanvasCapacity(rect, A.Layout(root), e.mousePosition);
            if (mapHit >= 0)
            {
                ClearCanvasSelection(); _canvasMap = mapHit;
                BeginCanvasGesture(6, root, rect, e.mousePosition);
                e.Use(); Repaint(); GUIUtility.ExitGUI(); return;
            }
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
            else if (_canvasGesture == 6)
            {
                _canvasDraft = A.Parse(_canvasBase);
                JObject layout = A.Layout(_canvasDraft);
                if (LoopLevelCommands.IsCapacity(layout, _canvasMap))
                {
                    JObject item = (JObject)A.Array(layout, "MapData")[_canvasMap];
                    Vector3 delta = CanvasWorld(rect, e.mousePosition) - _canvasWorldDown;
                    if (_snap > 0) delta = Snap(delta);
                    item["Position"] = A.Vector(A.Vector(item, "Position") + delta);
                }
            }
            else if(_canvasGesture==4&&inside)PaintCanvas(rect,e.mousePosition);
            else if(_canvasGesture==5)
            {
                _canvasDraft=A.Parse(_canvasBase);JObject path=(JObject)A.Array(A.Layout(_canvasDraft),"Paths")[_canvasPointPath];JArray points=A.Array(path,"ControlPoints");
                JObject point=(JObject)points[_canvasPointIndex];Vector3 initial=A.Vector(point,_canvasPointField);
                Vector3 moved=Snap(initial+CanvasWorld(rect,e.mousePosition)-_canvasWorldDown);
                if(_canvasPointField=="Position")
                {
                    A.MovePoint(points,_canvasPointIndex,moved);
                    if(_autoSmoothPoints)A.SmoothRoad(path,_roadSmoothing);
                }
                else A.SetTangent(points,_canvasPointIndex,_canvasPointField,moved);
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
        JObject layout=A.Layout(root);JArray boxes=A.Array(layout,"Boxes");JArray paths=A.Array(layout,"Paths");
        if(_canvasTool==6)
        {
            DrawWorkspaceRoadInspector(root, layout, paths, boxes);
            return;
        }
        if (LoopLevelCommands.IsCapacity(layout, _canvasMap))
        {
            DrawCapacityInspector(root);
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
        DrawWorkspaceLayout(root);
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
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("⚡ Tự động bo đường bao (Áp dụng ngay)"))
                WorkspaceCommand("Bo đường quanh hộp", next => LoopLevelBoardLayout.FitRoadOnly(A.Layout(next), _generatorSettings ?? new LoopLevelGeneratorSettings()));
        }
        _showAutoFill = EditorGUILayout.Foldout(_showAutoFill, "⚡ Bán tự động: Điền màu & câu đố cho các hộp hiện có", true);
        if (_showAutoFill)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _autoFillColors = EditorGUILayout.IntSlider("Số màu", _autoFillColors <= 0 ? 4 : _autoFillColors, 2, 8);
                _autoFillSecret = EditorGUILayout.IntSlider("Khối ẩn (%)", _autoFillSecret, 0, 60);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _autoFillColorLocks = EditorGUILayout.IntSlider("Khóa màu", _autoFillColorLocks, 0, 3);
                _autoFillNumLocks = EditorGUILayout.IntSlider("Khóa số", _autoFillNumLocks, 0, 3);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _autoFillGroups = EditorGUILayout.IntSlider("Nhóm chìa", _autoFillGroups, 0, 3);
                _autoFillEnterOnly = EditorGUILayout.IntSlider("Chỉ nhận vào", _autoFillEnterOnly, 0, 3);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _autoFillSeed = EditorGUILayout.IntField("Seed", _autoFillSeed == 0 ? 12345 : _autoFillSeed, GUILayout.Width(150));
                if (GUILayout.Button("Seed mới", GUILayout.Width(80))) _autoFillSeed = Guid.NewGuid().GetHashCode() & int.MaxValue;
                if (GUILayout.Button("⚡ Điền màu & khóa cho layout này", GUILayout.Height(22)))
                {
                    ApplyAutoFillLayout(root);
                }
            }
        }
    }

    private void ApplyAutoFillLayout(JObject root)
    {
        WorkspaceCommand("Tự động điền màu & câu đố cho layout", next =>
        {
            if (LoopLevelGenerator.AutoFillCubesAndMechanics(next, _autoFillColors, _autoFillSecret, _autoFillColorLocks, _autoFillNumLocks, _autoFillGroups, _autoFillEnterOnly, 0, _autoFillSeed, out string err))
            {
                _status = "Đã điền màu & câu đố cho layout hiện tại. Hãy chơi thử để kiểm tra.";
            }
            else
            {
                _status = "Điền màu không thành công: " + err;
            }
        });
    }

    private Rect CanvasCapacityRect(Rect rect, JObject item)
    {
        Vector2 center = CanvasProject(rect, A.Vector(item, "Position"));
        Vector2 size = new Vector2(Mathf.Max(40, 2 * _canvasScale), Mathf.Max(20, _canvasScale));
        return new Rect(center - size * .5f, size);
    }

    private int HitCanvasCapacity(Rect rect, JObject layout, Vector2 mouse)
    {
        JArray map = A.Array(layout, "MapData");
        for (int i = map.Count - 1; i >= 0; i--)
            if (LoopLevelCommands.IsCapacity(layout, i) && CanvasCapacityRect(rect, (JObject)map[i]).Contains(mouse)) return i;
        return -1;
    }

    private void DrawCanvasCapacities(Rect rect, JObject layout)
    {
        JArray map = A.Array(layout, "MapData");
        for (int i = 0; i < map.Count; i++)
        {
            if (!LoopLevelCommands.IsCapacity(layout, i)) continue;
            Rect marker = CanvasCapacityRect(rect, (JObject)map[i]);
            marker.position -= rect.position;
            Color color = new Color(.15f, .4f, .3f);
            if (_canvasMap == i) color = new Color(.5f, .45f, .15f);
            EditorGUI.DrawRect(marker, color);
            GUI.Label(marker, "Capacity 0/" + A.Int(layout, "PathLength"), EditorStyles.whiteMiniLabel);
        }
    }

    private void DrawCapacityInspector(JObject root)
    {
        Header("Bảng Capacity");
        EditorGUILayout.HelpBox("Kéo bảng bằng công cụ Chọn. Đây là ký hiệu vị trí; kiểm tra hình ảnh prefab trong Game View.", MessageType.None);
        JObject editable = (JObject)root.DeepClone();
        JObject item = (JObject)A.Array(A.Layout(editable), "MapData")[_canvasMap];
        EditorGUI.BeginChangeCheck();
        VectorField(item, "Position"); VectorField(item, "Rotation"); VectorField(item, "Scale");
        if (EditorGUI.EndChangeCheck() && _canvasGesture == 0)
        {
            Commit(editable, "Sửa Capacity");
            GUIUtility.ExitGUI();
        }
        if (GUILayout.Button("Xóa bảng Capacity")) WorkspaceCommand("Xóa Capacity", DeleteSelectedCapacity);
    }

    private void DeleteSelectedCapacity(JObject root)
    {
        JObject layout = A.Layout(root);
        if (LoopLevelCommands.IsCapacity(layout, _canvasMap)) A.Array(layout, "MapData").RemoveAt(_canvasMap);
        ClearCanvasSelection();
    }

    private void AddWorkspaceCapacity(JObject root)
    {
        ClearCanvasSelection();
        _canvasMap = LoopLevelCommands.AddCapacity(root);
        _canvasTool = 0; _canvasFramed = false;
    }

    private void PreviewFitWorkspaceRoad(JObject root)
    {
        LoopLevelBoardLayout.FitRoadOnly(A.Layout(root), _generatorSettings);
    }

    private void DrawWorkspaceLayout(JObject root)
    {
        _showWorkspaceLayout = EditorGUILayout.Foldout(_showWorkspaceLayout, "Bố cục, đường & Capacity", true);
        if (!_showWorkspaceLayout) return;
        if (_generatorSettings == null) _generatorSettings = new LoopLevelGeneratorSettings();
        var settings = _generatorSettings;
        settings.BoxGap = EditorGUILayout.Slider("Khoảng cách giữa mép hộp", settings.BoxGap, .35f, 2);
        settings.RoadClearance = EditorGUILayout.Slider("Khoảng hở tới thành đường", settings.RoadClearance, .05f, 3);
        settings.RoadShape = EditorGUILayout.Popup("Hình đường bao", settings.RoadShape, new[] { "Chữ nhật bo góc", "Bầu dục", "Tròn" });
        settings.BoardLayout = EditorGUILayout.Popup("Cách xếp hộp", settings.BoardLayout, new[] { "Hai dãy hướng ra đường", "Các hàng cân đối" });
        if (settings.BoardLayout == 1)
        {
            settings.BoardColumns = EditorGUILayout.IntSlider("Số cột (0 = tự động)", settings.BoardColumns, 0, 24);
            settings.BoardAspect = EditorGUILayout.Slider("Tỉ lệ rộng / sâu", settings.BoardAspect, .5f, 1.5f);
        }
        EditorGUILayout.HelpBox("Thiết lập áp dụng khi tạo preview mới bằng các nút xếp hộp / đường bao. Bản nháp giữ nguyên tới khi bấm Áp dụng.", MessageType.None);
        int capacity = A.Int(A.Layout(root), "PathLength");
        int nextCapacity = EditorGUILayout.IntField("Sức chứa đường (nhóm)", capacity);
        if (nextCapacity != capacity && nextCapacity >= 1)
            WorkspaceCommand("Đổi sức chứa đường", next => A.Layout(next)["PathLength"] = nextCapacity);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Thêm / chọn Capacity")) WorkspaceCommand("Thêm / chọn Capacity", AddWorkspaceCapacity);
            if (GUILayout.Button("Xem trước đường bao")) PreviewWorkspaceCommand("Thay đường bao quanh hộp", PreviewFitWorkspaceRoad);
        }
    }

    private void UpdateBalanceSummary(JObject root)
    {
        if (ReferenceEquals(_balanceRoot, root)) return;
        _balanceRoot = root;
        _balanceColors.Clear();
        _balanceTotal = _normalTargets = _longTargets = _normalDemand = _longDemand = 0;
        _initialMoves = 0; _initialComplete = false; _balanceError = null;
        foreach (JObject box in A.Array(A.Layout(root), "Boxes"))
        {
            int type = A.Int(box, "BoxType");
            if (type == 0) _normalTargets++;
            else if (type == 2) _longTargets++;
            foreach (JObject cube in A.Array(box, "Cubes"))
            {
                int color = A.Int(cube, "Type");
                _balanceColors.TryGetValue(color, out int count);
                _balanceColors[color] = count + 1; _balanceTotal++;
            }
        }
        foreach (KeyValuePair<int, int> color in _balanceColors)
        {
            int capacity = LoopLevelCommands.ColorTargetCapacity(color.Key);
            if (capacity == 4) _normalDemand += Mathf.CeilToInt(color.Value / 4f);
            else if (capacity == 6) _longDemand += Mathf.CeilToInt(color.Value / 6f);
        }
        try
        {
            GameData data = LoopLevelGenerator.DecodeDetached(root);
            _initialMoves = LoopLevelCommands.CountInitialMoves(data);
            _initialComplete = data.IsGameOver();
        }
        catch (Exception exception) { _balanceError = "Chưa đánh giá được nước thả: " + exception.Message; }
    }

    private void DrawLiveBalanceHUD(JObject root)
    {
        UpdateBalanceSummary(root);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            string initial = "Nước thả ban đầu: " + _initialMoves + " hộp";
            if (_initialComplete) initial = "Bản nháp đã ở trạng thái hoàn thành theo mô phỏng.";
            else if (_initialMoves == 0) initial += " — cần kiểm tra; chưa kết luận vô nghiệm.";
            if (_balanceError != null) initial = _balanceError;
            EditorGUILayout.LabelField(initial, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Màu / số khối (điều kiện cần theo mô phỏng):", EditorStyles.miniLabel);
            int columns = Mathf.Max(3, Mathf.FloorToInt((position.width - 40) / 70));
            int column = 0;
            EditorGUILayout.BeginHorizontal();
            try
            {
                foreach (KeyValuePair<int, int> color in _balanceColors)
                {
                    if (column == columns)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        column = 0;
                    }
                    int capacity = LoopLevelCommands.ColorTargetCapacity(color.Key);
                    string suffix = " ?";
                    if (capacity > 0) suffix = color.Value % capacity == 0 ? " ✓" : " !";
                    string tooltip = "Màu đặc biệt: cần kiểm tra trong Game View.";
                    if (capacity > 0) tooltip = "Mỗi hộp đích chứa " + capacity + " khối. Cân bằng số lượng không chứng minh có lời giải. Single không tính là hộp đích.";
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = A.ColorFor(color.Key);
                    if (GUILayout.Button(new GUIContent(color.Key + ":" + color.Value + suffix, tooltip), EditorStyles.miniButton, GUILayout.Width(65)))
                        SelectBoxesWithColor(root, color.Key);
                    GUI.backgroundColor = previous;
                    column++;
                }
                GUILayout.FlexibleSpace();
            }
            finally { EditorGUILayout.EndHorizontal(); }
            EditorGUILayout.LabelField("Tổng: " + _balanceTotal + " khối · Đường: " + A.Int(A.Layout(root), "PathLength") + " nhóm", EditorStyles.miniLabel);
            if (_normalDemand > _normalTargets || _longDemand > _longTargets)
                EditorGUILayout.HelpBox("Thiếu hộp đích theo tổng màu: thường cần " + _normalDemand + "/" + _normalTargets + ", dài cần " + _longDemand + "/" + _longTargets + ". Chưa xét chuyển đổi màu đặc biệt.", MessageType.Warning);
        }
    }

    private void SelectBoxesWithColor(JObject root, int color)
    {
        var matching = new List<int>();
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        for (int i = 0; i < boxes.Count; i++)
            foreach (JObject cube in A.Array((JObject)boxes[i], "Cubes"))
                if (A.Int(cube, "Type") == color) { matching.Add(i); break; }
        SetCanvasSelection(matching);
    }
    private void DrawWorkspaceValidation(JObject root)
    {
        JObject layout=A.Layout(root);JArray boxes=A.Array(layout,"Boxes");
        GUILayout.Label(boxes.Count + " hộp · Kiểm tra cấu trúc và lời giải", EditorStyles.wordWrappedMiniLabel);
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

    private void DrawWorkspaceRoadInspector(JObject root, JObject layout, JArray paths, JArray boxes)
    {
        Header("Đường & Điểm điều khiển");
        if (_drawingRoad)
        {
            EditorGUILayout.HelpBox("ĐANG VẼ ĐƯỜNG MỚI\n• Click chuột trái: đặt điểm trên bàn.\n• Chuột phải / Backspace: bỏ điểm vừa đặt.\n• Bấm điểm đầu hoặc Enter: hoàn tất khép kín.\n• Esc: hủy.", MessageType.Info);
            EditorGUILayout.LabelField("Số điểm đã đặt", _sketchPoints.Count.ToString(), EditorStyles.boldLabel);
            _sketchClosed = EditorGUILayout.ToggleLeft("Khép kín vòng (Closed Loop)", _sketchClosed);
            _sketchSmooth = EditorGUILayout.ToggleLeft("Bo cong mềm (Smooth)", _sketchSmooth);
            if (_sketchSmooth) _roadSmoothing = EditorGUILayout.Slider("Độ mượt", _roadSmoothing, 0.05f, 1f);
            _canvasReplaceRoad = EditorGUILayout.ToggleLeft("Thay thế đường đang chọn khi hoàn tất", _canvasReplaceRoad);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!CanFinishSketch))
                    if (GUILayout.Button("✓ Hoàn tất đường", GUILayout.Height(26))) { FinishRoadSketch(); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("↶ Bỏ điểm", GUILayout.Height(26), GUILayout.Width(75))) RemoveSketchPoint();
                if (GUILayout.Button("✕ Hủy", GUILayout.Height(26), GUILayout.Width(55))) { CancelRoadSketch(); GUIUtility.ExitGUI(); }
            }
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Bút vẽ đường mới", GUILayout.Height(26)))
            {
                CancelRoadSketch();
                _drawingRoad = true;
                _status = "Bấm trên bàn để đặt các điểm cho đường mới.";
                GUIUtility.ExitGUI();
            }
            if (boxes.Count > 0)
            {
                if (GUILayout.Button("⟲ Bao quanh hộp", GUILayout.Height(26)))
                {
                    WorkspaceCommand("Tạo đường bao quanh các hộp", next =>
                    {
                        LoopLevelBoardLayout.FitRoadOnly(A.Layout(next), _generatorSettings ?? new LoopLevelGeneratorSettings());
                        _path = 0; _point = 0;
                    });
                }
            }
        }

        if (paths.Count == 0)
        {
            EditorGUILayout.HelpBox("Chưa có đường nào trên bàn. Bấm '+ Bút vẽ đường mới' hoặc chọn mẫu bên dưới để tạo.", MessageType.Warning);
            DrawQuickRoadTemplates(paths);
            return;
        }

        _path = Mathf.Clamp(_path, 0, paths.Count - 1);
        using (new EditorGUILayout.HorizontalScope())
        {
            _path = EditorGUILayout.Popup("Chọn đường", _path, Enumerable.Range(0, paths.Count).Select(i => "Đường " + i + (A.Int((JObject)paths[i], "IsCircle") > 0 ? " (Vòng kín)" : " (Mở)")).ToArray());
            if (GUILayout.Button("+ Thêm", GUILayout.Width(55)))
            {
                WorkspaceCommand("Thêm đường mới", next =>
                {
                    JArray p = A.Array(A.Layout(next), "Paths");
                    p.Add(A.RectanglePath());
                    _path = p.Count - 1; _point = 0;
                });
            }
        }

        JObject path = (JObject)paths[_path];
        JArray points = A.Array(path, "ControlPoints");
        bool isClosed = A.Int(path, "IsCircle") > 0;

        Vector3[] sampledRoad = LoopLevelBoardLayout.SampleRoad(path);
        List<Vector3> flatHits = A.FindSelfIntersections(sampledRoad);
        if (flatHits.Count > 0)
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("⚠️ Phát hiện " + flatHits.Count + " điểm giao cắt phẳng!\nHai nhánh đường cắt nhau trên cùng mặt phẳng (Y=0.2m) làm mesh và khối đâm xuyên nhau.", MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("🌉 Nâng thành Cầu vượt 3D", "Tự động nâng 1 nhánh tại giao điểm lên Y=1.8m để băng chuyền dưới chui qua lọt")))
                    {
                        WorkspaceCommand("Tạo cầu vượt 3D", next =>
                        {
                            JObject target = (JObject)A.Array(A.Layout(next), "Paths")[_path];
                            A.Make3DOverpass(target, 1.8f);
                        });
                    }
                    if (GUILayout.Button(new GUIContent("↔ Tách eo không cắt", "Tách 2 nhánh tại điểm giao nhau sang 2 bên để thành đường thắt eo không cắt nhau")))
                    {
                        WorkspaceCommand("Tách eo đường không cắt", next =>
                        {
                            JObject target = (JObject)A.Array(A.Layout(next), "Paths")[_path];
                            A.UntangleRoad(target, 1.8f);
                        });
                    }
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Làm mượt", "Tính lại tiếp tuyến cong mềm")))
                WorkspaceCommand("Làm mượt đường", next => A.SmoothRoad((JObject)A.Array(A.Layout(next), "Paths")[_path], _roadSmoothing));
            if (GUILayout.Button(new GUIContent("Làm thẳng", "Đưa các góc về nhọn / đa giác")))
                WorkspaceCommand("Làm thẳng đường", next => A.SmoothRoad((JObject)A.Array(A.Layout(next), "Paths")[_path], 0f));
            if (GUILayout.Button(new GUIContent(isClosed ? "Mở vòng" : "Khép kín", "Chuyển giữa đường mở và vòng kín")))
                WorkspaceCommand("Đổi kiểu vòng", next => A.ToggleRoadClosed((JObject)A.Array(A.Layout(next), "Paths")[_path], _roadSmoothing));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Đảo chiều", "Đảo ngược chiều chạy của băng chuyền")))
                WorkspaceCommand("Đảo chiều đường", next => A.ReverseRoad((JObject)A.Array(A.Layout(next), "Paths")[_path], _autoSmoothPoints ? _roadSmoothing : -1f));
            if (GUILayout.Button(new GUIContent("Căn giữa bàn", "Di chuyển đường về trung tâm bàn chơi")))
                WorkspaceCommand("Căn giữa đường", next => A.CenterRoad((JObject)A.Array(A.Layout(next), "Paths")[_path], Vector3.zero));
            if (GUILayout.Button(new GUIContent("Xóa đường", "Xóa hoàn toàn đường này")))
            {
                WorkspaceCommand("Xóa đường", next =>
                {
                    A.Array(A.Layout(next), "Paths").RemoveAt(_path);
                    _point = -1;
                    _path = Math.Max(0, _path - 1);
                });
                GUIUtility.ExitGUI();
            }
        }

        EditorGUILayout.Space(2);
        _autoSmoothPoints = EditorGUILayout.ToggleLeft("Tự làm mượt khi di chuyển điểm", _autoSmoothPoints);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Độ mượt", GUILayout.Width(65));
            _roadSmoothing = EditorGUILayout.Slider(_roadSmoothing, 0.05f, 1f);
        }
        _showTangents = EditorGUILayout.ToggleLeft("Hiện tiếp tuyến nâng cao (In/Out Tangent)", _showTangents);

        EditorGUILayout.Space(2);
        DrawQuickRoadTemplates(paths);

        EditorGUILayout.Space(2);
        DrawControlPointList(path, points, isClosed);
    }

    private void DrawControlPointList(JObject path, JArray points, bool isClosed)
    {
        bool repeatedEnd = points.Count > 1 && (A.Vector((JObject)points[0], "Position") - A.Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        int anchorCount = points.Count - (repeatedEnd ? 1 : 0);

        _showPointList = EditorGUILayout.Foldout(_showPointList, "Danh sách điểm điều khiển (" + anchorCount + " điểm)", true);
        if (_showPointList)
        {
            for (int i = 0; i < anchorCount; i++)
            {
                JObject pt = (JObject)points[i];
                Vector3 pos = A.Vector(pt, "Position");
                bool isSelected = _point == i;
                bool isOverpass = pos.y > 0.6f;

                using (new EditorGUILayout.HorizontalScope(isSelected ? EditorStyles.selectionRect : EditorStyles.helpBox))
                {
                    string label = (i == 0 ? "① (Đầu): " : (i + 1) + ": ") + "(" + pos.x.ToString("F1") + ", " + pos.z.ToString("F1") + ")" + (isOverpass ? " [🌉 Y:" + pos.y.ToString("F1") + "]" : "");
                    if (GUILayout.Button(label, EditorStyles.label))
                    {
                        _point = i;
                        Repaint();
                    }
                    if (GUILayout.Button("+ Chèn", EditorStyles.miniButton, GUILayout.Width(52)))
                    {
                        WorkspaceCommand("Chèn điểm sau điểm " + (i + 1), next =>
                        {
                            JObject targetPath = (JObject)A.Array(A.Layout(next), "Paths")[_path];
                            _point = A.InsertPointAfter(targetPath, i, null, _autoSmoothPoints ? _roadSmoothing : -1f);
                        });
                    }
                    using (new EditorGUI.DisabledScope(anchorCount <= (isClosed ? 3 : 2)))
                    {
                        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(24)))
                        {
                            WorkspaceCommand("Xóa điểm " + (i + 1), next =>
                            {
                                JObject targetPath = (JObject)A.Array(A.Layout(next), "Paths")[_path];
                                A.RemoveRoadPoint(targetPath, i);
                                _point = Math.Min(i, A.Array(targetPath, "ControlPoints").Count - 1);
                                if (_autoSmoothPoints) A.SmoothRoad(targetPath, _roadSmoothing);
                            });
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Thêm điểm cuối"))
                {
                    WorkspaceCommand("Thêm điểm cuối đường", next =>
                    {
                        JObject targetPath = (JObject)A.Array(A.Layout(next), "Paths")[_path];
                        _point = A.InsertPointAfter(targetPath, anchorCount - 1, null, _autoSmoothPoints ? _roadSmoothing : -1f);
                    });
                }
            }
        }

        if (_point >= 0 && _point < points.Count)
        {
            EditorGUILayout.Space(2);
            JObject selectedPt = (JObject)points[_point];
            Header("Điểm " + (_point + 1) + " đang chọn");
            Vector3 posBefore = A.Vector(selectedPt, "Position");
            Vector2 xz = EditorGUILayout.Vector2Field("Tọa độ (X, Z)", new Vector2(posBefore.x, posBefore.z));
            float newY = EditorGUILayout.FloatField("Độ cao Y (Cầu/Sàn)", posBefore.y);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Gán độ cao:", GUILayout.Width(75));
                if (GUILayout.Button("Sàn (0.2m)", EditorStyles.miniButton)) newY = 0.2f;
                if (GUILayout.Button("Dốc (1.0m)", EditorStyles.miniButton)) newY = 1.0f;
                if (GUILayout.Button("Cầu (1.8m)", EditorStyles.miniButton)) newY = 1.8f;
            }
            if (xz.x != posBefore.x || xz.y != posBefore.z || Mathf.Abs(newY - posBefore.y) > 0.001f)
            {
                Vector3 newPos = new Vector3(xz.x, newY, xz.y);
                WorkspaceCommand("Sửa tọa độ điểm " + (_point + 1), next =>
                {
                    JObject targetPath = (JObject)A.Array(A.Layout(next), "Paths")[_path];
                    JArray pts = A.Array(targetPath, "ControlPoints");
                    A.MovePoint(pts, _point, newPos);
                    if (_autoSmoothPoints) A.SmoothRoad(targetPath, _roadSmoothing);
                });
            }
        }
    }

    private void DrawQuickRoadTemplates(JArray paths)
    {
        _showRoadPresets = EditorGUILayout.Foldout(_showRoadPresets, "Mẫu đường nhanh theo điểm", true);
        if (!_showRoadPresets) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Chữ nhật (4 góc)"))
                {
                    WorkspaceCommand("Tạo đường chữ nhật", next =>
                    {
                        JArray p = A.Array(A.Layout(next), "Paths");
                        p.Add(A.RectanglePath());
                        _path = p.Count - 1; _point = 0;
                    });
                }
                if (GUILayout.Button("Oval / Tròn (6 điểm)"))
                {
                    WorkspaceCommand("Tạo đường oval", next =>
                    {
                        JArray p = A.Array(A.Layout(next), "Paths");
                        p.Add(A.CirclePath(4f));
                        _path = p.Count - 1; _point = 0;
                    });
                }
                if (GUILayout.Button("Chữ U (5 điểm)"))
                {
                    WorkspaceCommand("Tạo đường chữ U", next =>
                    {
                        JArray p = A.Array(A.Layout(next), "Paths");
                        p.Add(A.UShapeRoad(Vector3.zero, 8f, 8f, _roadSmoothing));
                        _path = p.Count - 1; _point = 0;
                    });
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Số 8 (Thắt eo không cắt)", "2 nhánh uốn lượn song song qua tâm không cắt nhau")))
                {
                    WorkspaceCommand("Tạo đường số 8 thắt eo", next =>
                    {
                        JArray p = A.Array(A.Layout(next), "Paths");
                        p.Add(A.HourglassRoad(Vector3.zero, 10f, 6f, 1.8f, _roadSmoothing));
                        _path = p.Count - 1; _point = 0;
                    });
                }
                if (GUILayout.Button(new GUIContent("Số 8 (Cầu vượt 3D)", "1 nhánh sàn Y=0.2m, 1 nhánh cầu vượt Y=2.0m")))
                {
                    WorkspaceCommand("Tạo đường số 8 cầu vượt 3D", next =>
                    {
                        JArray p = A.Array(A.Layout(next), "Paths");
                        p.Add(A.Figure8OverpassRoad(Vector3.zero, 10f, 6f, 1.8f, _roadSmoothing));
                        _path = p.Count - 1; _point = 0;
                    });
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Số 8 (Cắt phẳng)", "Đường số 8 cắt nhau tại tâm phẳng Y=0.2m")))
                {
                    WorkspaceCommand("Tạo đường số 8 cắt phẳng", next =>
                    {
                        JArray p = A.Array(A.Layout(next), "Paths");
                        p.Add(A.Figure8Road(Vector3.zero, 10f, 6f, _roadSmoothing));
                        _path = p.Count - 1; _point = 0;
                    });
                }
            }
        }
    }

    private void ShowRoadPointContextMenu(JObject root, int pathIndex, int pointIndex)
    {
        GenericMenu menu = new GenericMenu();
        JArray paths = A.Array(A.Layout(root), "Paths");
        if (pathIndex < 0 || pathIndex >= paths.Count) return;
        JObject path = (JObject)paths[pathIndex];
        JArray points = A.Array(path, "ControlPoints");
        bool isClosed = A.Int(path, "IsCircle") > 0;
        bool repeated = points.Count > 1 && (A.Vector((JObject)points[0], "Position") - A.Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        int anchors = points.Count - (repeated ? 1 : 0);

        if (anchors > (isClosed ? 3 : 2))
        {
            menu.AddItem(new GUIContent("Xóa điểm " + (pointIndex + 1) + " (Delete)"), false, () =>
            {
                WorkspaceCommand("Xóa điểm đường", next =>
                {
                    JObject p = (JObject)A.Array(A.Layout(next), "Paths")[pathIndex];
                    A.RemoveRoadPoint(p, pointIndex);
                    _point = Math.Min(pointIndex, A.Array(p, "ControlPoints").Count - 1);
                    if (_autoSmoothPoints) A.SmoothRoad(p, _roadSmoothing);
                });
            });
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Xóa điểm " + (pointIndex + 1) + " (Đã đạt số điểm tối thiểu)"));
        }

        menu.AddItem(new GUIContent("Chèn điểm sau điểm " + (pointIndex + 1)), false, () =>
        {
            WorkspaceCommand("Chèn điểm đường", next =>
            {
                JObject p = (JObject)A.Array(A.Layout(next), "Paths")[pathIndex];
                _point = A.InsertPointAfter(p, pointIndex, null, _autoSmoothPoints ? _roadSmoothing : -1f);
            });
        });

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Nâng điểm này thành Cầu vượt (Y = 1.8m)"), false, () =>
        {
            WorkspaceCommand("Nâng điểm cầu vượt", next =>
            {
                JObject p = (JObject)A.Array(A.Layout(next), "Paths")[pathIndex];
                JArray pts = A.Array(p, "ControlPoints");
                Vector3 cur = A.Vector((JObject)pts[pointIndex], "Position");
                cur.y = 1.8f;
                A.MovePoint(pts, pointIndex, cur);
                if (_autoSmoothPoints) A.SmoothRoad(p, _roadSmoothing);
            });
        });
        menu.AddItem(new GUIContent("Hạ điểm này xuống mặt Sàn (Y = 0.2m)"), false, () =>
        {
            WorkspaceCommand("Hạ điểm xuống sàn", next =>
            {
                JObject p = (JObject)A.Array(A.Layout(next), "Paths")[pathIndex];
                JArray pts = A.Array(p, "ControlPoints");
                Vector3 cur = A.Vector((JObject)pts[pointIndex], "Position");
                cur.y = 0.2f;
                A.MovePoint(pts, pointIndex, cur);
                if (_autoSmoothPoints) A.SmoothRoad(p, _roadSmoothing);
            });
        });

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Làm mượt đường này"), false, () =>
        {
            WorkspaceCommand("Làm mượt đường", next =>
            {
                A.SmoothRoad((JObject)A.Array(A.Layout(next), "Paths")[pathIndex], _roadSmoothing);
            });
        });

        menu.AddItem(new GUIContent("Làm thẳng đường (góc nhọn)"), false, () =>
        {
            WorkspaceCommand("Làm thẳng đường", next =>
            {
                A.SmoothRoad((JObject)A.Array(A.Layout(next), "Paths")[pathIndex], 0f);
            });
        });

        menu.AddItem(new GUIContent("Đảo chiều đường"), false, () =>
        {
            WorkspaceCommand("Đảo chiều đường", next =>
            {
                A.ReverseRoad((JObject)A.Array(A.Layout(next), "Paths")[pathIndex], _autoSmoothPoints ? _roadSmoothing : -1f);
            });
        });

        menu.AddItem(new GUIContent(isClosed ? "Mở vòng (Open)" : "Khép kín vòng (Close)"), false, () =>
        {
            WorkspaceCommand("Đổi kiểu vòng đường", next =>
            {
                A.ToggleRoadClosed((JObject)A.Array(A.Layout(next), "Paths")[pathIndex], _roadSmoothing);
            });
        });

        menu.ShowAsContext();
    }

    private void DrawRoadDirectionChevrons(Func<Vector3, Vector3> project, Vector3[] sampled)
    {
        if (sampled == null || sampled.Length < 10) return;
        Handles.color = new Color(0.4f, 0.9f, 1f, 0.55f);
        int step = Mathf.Max(8, sampled.Length / 12);
        for (int i = step / 2; i < sampled.Length - 1; i += step)
        {
            Vector3 p1 = project(sampled[i]);
            Vector3 p2 = project(sampled[i + 1]);
            Vector2 dir = new Vector2(p2.x - p1.x, p2.y - p1.y);
            if (dir.sqrMagnitude < 0.001f) continue;
            dir.Normalize();
            Vector2 normal = new Vector2(-dir.y, dir.x);
            Vector3 tip = p1 + (Vector3)(dir * 4.5f);
            Vector3 wing1 = p1 - (Vector3)(dir * 3.5f - normal * 3.5f);
            Vector3 wing2 = p1 - (Vector3)(dir * 3.5f + normal * 3.5f);
            Handles.DrawAAPolyLine(1.8f, wing1, tip, wing2);
        }
    }
}
#endif
