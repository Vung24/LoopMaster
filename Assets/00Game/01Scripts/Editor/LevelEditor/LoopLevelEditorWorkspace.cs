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
    [UnityEngine.Serialization.FormerlySerializedAs("_canvasMoveLocked")]
    [SerializeField] private bool _canvasViewLocked;
    [SerializeField] private bool _canvasReplaceRoad = true;
    [SerializeField] private List<int> _canvasSelection = new List<int>();
    [SerializeField] private Vector2 _canvasCenter, _inspectorScroll, _tableScroll;
    [SerializeField] private float _canvasScale = 25;
    [SerializeField] private float _canvasHeightMultiplier = 1f;
    [SerializeField] private int _canvasTool, _variantSeed = 12345, _solverBudget = 10000;
    [SerializeField] private bool _showAdvancedTools, _showBoardTable;
    [SerializeField] private int _partnerBoxChoice;
    [SerializeField] private int _linkedCubeChoice, _partnerCubeChoice;
    [SerializeField] private string _draftSolverJson, _draftSolverStatus;
    private JObject _canvasDraft;
    private JObject _canvasProposal;
    private string _proposalBase, _proposalAction;
    private string _proposalReport;
    private bool _draftSolverUsesProposal;
    private JObject _draftSolverProposal;
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
    [SerializeField] private int _canvasMap = -1;
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
    private void ClearWorkspaceProposal()
    {
        if (_draftSolverUsesProposal) StopDraftSolver();
        _draftSolverProposal = null;
        _canvasProposal = null;
        _proposalBase = _proposalAction = _proposalReport = null;
        _hasValidation = false;
    }
    private void PreviewWorkspaceCommand(string name, Action<JObject> edit, bool moved = false)
    {
        CancelCanvasGesture();
        ClearWorkspaceProposal();
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
                if (!string.IsNullOrEmpty(_proposalReport)) EditorGUILayout.HelpBox(_proposalReport, MessageType.Info);
            }
            _canvasSelection = LoopLevelCommands.Selection(root, _canvasSelection).ToList();
            if (_box >= 0 && _box < A.Array(layout, "Boxes").Count && !_canvasSelection.Contains(_box)) SetCanvasSelection(new[] { _box });
            EditorGUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Căn khung", GUILayout.Width(85))) _canvasFramed = false;
                using (new EditorGUI.DisabledScope(_canvasViewLocked))
                {
                    if (GUILayout.Button("−", GUILayout.Width(28))) ZoomCanvas(_canvasRect, _canvasRect.center, 1f / 1.25f);
                    if (GUILayout.Button("+", GUILayout.Width(28))) ZoomCanvas(_canvasRect, _canvasRect.center, 1.25f);
                }
                _canvasPlayerView = GUILayout.Toggle(_canvasPlayerView, "Che màu ẩn", GUILayout.Width(105));
                bool viewLocked = GUILayout.Toggle(_canvasViewLocked,
                    new GUIContent("Khóa giữa preview", "Căn toàn map vào giữa rồi khóa pan/zoom. Vẫn chỉnh hộp, cổng, điểm đường và tô màu/Hidden."));
                if (viewLocked != _canvasViewLocked)
                {
                    CancelCanvasGesture();
                    _canvasViewLocked = viewLocked;
                    if (viewLocked) _canvasFramed = false;
                    Repaint();
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_canvasTool == 6)
                {
                    GUILayout.Label("💡 Bước 1: Chọn mẫu đường bên dưới hoặc dùng bút vẽ tự do · Kéo mấu neo để uốn lượn đường đi", EditorStyles.miniLabel);
                }
                else if (_canvasTool == 0)
                {
                    GUILayout.Label("💡 Bước 2: Kéo Hộp & Cổng từ Bảng Prefabs bên dưới thả vào bàn chơi hoặc Scene 3D", EditorStyles.miniLabel);
                }
                else if (_canvasTool >= 2 && _canvasTool <= 4)
                {
                    GUILayout.Label("Bấm/kéo trên cube để tô · Shift+Click hoặc bấm viền hộp để chọn · Alt+Click: hút màu", EditorStyles.wordWrappedMiniLabel);
                }
                else if (_canvasTool == 5)
                {
                    GUI.contentColor = new Color(1f, 0.35f, 0.35f);
                    GUILayout.Label("🗑️ Bấm vào Khối để xóa khối · Bấm Hộp để xóa hộp · Bấm Cổng để xóa cổng · Bấm Mấu đường để xóa mấu (Esc để thoát)", EditorStyles.boldLabel);
                    GUI.contentColor = Color.white;
                }
            }
            if (_canvasTool == 6) DrawRoadQuickBar(root);
            if (_canvasTool >= 2 && _canvasTool <= 4) DrawWorkspacePalette(layout);
            if (_canvasTool == 5) EditorGUILayout.HelpBox("🗑️ CHẾ ĐỘ XÓA ĐANG BẬT (Phím R):\n• Bấm vào Khối Cube: xóa khối đó.\n• Kéo chuột qua các khối: xóa nhiều khối liên tiếp.\n• Bấm vào Hộp trống hoặc viền hộp: xóa nguyên hộp.\n• Bấm vào Cổng Vào/Ra hoặc Capacity: xóa cổng đó.\n• Bấm vào Mấu neo đường: xóa điểm đường đó.\n• Nhấn phím Esc hoặc chọn công cụ khác để thoát chế độ xóa.", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Cỡ khung preview", GUILayout.Width(115));
                float heightMultiplier = GUILayout.HorizontalSlider(_canvasHeightMultiplier, .5f, 2f);
                GUILayout.Label(heightMultiplier.ToString("P0"), GUILayout.Width(45));
                if (GUILayout.Button("Mặc định", GUILayout.Width(75))) heightMultiplier = 1f;
                if (heightMultiplier != _canvasHeightMultiplier)
                {
                    CancelCanvasGesture();
                    _canvasHeightMultiplier = heightMultiplier;
                    _canvasFramed = false;
                    Repaint();
                }
            }
            float height = Mathf.Clamp(position.height - 400, 240, 620) * 1.5f * _canvasHeightMultiplier;
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
            string navigationHint = "Cuộn chuột: zoom tại con trỏ · Giữ chuột giữa và kéo: di chuyển bàn";
            if (_canvasViewLocked) navigationHint = "Đã khóa khung nhìn ở giữa · Bỏ khóa để pan/zoom";
            EditorGUILayout.LabelField(navigationHint + " · W: kéo hộp/cổng · Q: kéo điểm đường", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(_canvasProposal != null))
            {
                bool editingRoad = _canvasTool == 6;
                bool hasSelectedBoxes = !editingRoad && _canvasSelection.Count > 0 && !LoopLevelCommands.IsMapItem(layout, _canvasMap);
                if (_canvasTool == 2 && hasSelectedBoxes) DrawManualCubeTools();
                if (_canvasTool == 0 || _canvasTool == 1) DrawPrefabPalette(root);

                if (hasSelectedBoxes)
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        GUILayout.Label(_canvasSelection.Count + " hộp đã chọn", EditorStyles.miniLabel);
                        if (GUILayout.Button("📋 Nhân bản", GUILayout.Height(22))) DuplicateWorkspace(false);
                        if (GUILayout.Button("⚡ Thêm màu mới (A)", GUILayout.Height(22))) TryQuickAddUnusedColor();
                        if (GUILayout.Button("🗑️ Xóa hộp", GUILayout.Height(22)))
                            WorkspaceCommand("Xóa hộp", DeleteSelectedBoxes);
                        if (GUILayout.Button("⚡ Đổ màu thiếu (đủ bộ 4)", GUILayout.Height(22)))
                            WorkspaceCommand("Điền màu thiếu theo bộ 4", FillMissingCubeColors);
                        if (GUILayout.Button("🗑️ Xóa khối hộp đã chọn", GUILayout.Height(22)))
                            WorkspaceCommand("Xóa khối các hộp đã chọn", next => LoopLevelCommands.ClearSelectedBoxes(next, _canvasSelection));
                    }
                }
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Toàn bàn", EditorStyles.miniLabel);
                    if (GUILayout.Button("⚡ Tự động gắn ô đường (Auto-Bind)", GUILayout.Height(22)))
                        WorkspaceCommand("Tự động liên kết ô đường gần nhất", next => LoopLevelBoardLayout.AutoBindBoxesToRoad(A.Layout(next)));
                    if (GUILayout.Button("Xem trước cân bằng bộ 4/6", GUILayout.Height(22)))
                        PreviewWorkspaceCommand("Cân bằng màu theo bộ 4/6", PreviewColorBalance);
                }

                if (editingRoad || hasSelectedBoxes)
                {
                    string toolsTitle = "⚙️ Căn chỉnh & liên kết hộp đã chọn";
                    if (editingRoad) toolsTitle = "⚙️ Tiện ích đường & cổng";
                    _showAdvancedTools = EditorGUILayout.Foldout(_showAdvancedTools, toolsTitle, true);
                    if (_showAdvancedTools)
                    {
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            if (editingRoad) DrawWorkspaceRoadOperations();
                            else DrawWorkspaceOperations(root);
                        }
                    }
                }

                _showBoardTable = EditorGUILayout.Foldout(_showBoardTable, "📋 Bảng danh sách khối chi tiết toàn bàn", true);
                if (_showBoardTable)
                {
                    using (var table = new EditorGUILayout.ScrollViewScope(_tableScroll, GUILayout.Height(135)))
                    {
                        _tableScroll = table.scrollPosition;
                        int previousBox = _box;
                        JObject tableSource = _canvasDraft ?? root;
                        JObject tableRoot = (JObject)tableSource.DeepClone();
                        _cubeBrushMode = _canvasTool == 3 ? 1 : _canvasTool == 4 ? 2 : 0;
                        DrawBoardTable(A.Layout(tableRoot));
                        if (_box != previousBox) SetCanvasSelection(new[] { _box });
                        if (!JToken.DeepEquals(tableRoot, tableSource) && _canvasGesture == 0) { Commit(tableRoot, "Sửa khối trong bảng"); GUIUtility.ExitGUI(); }
                    }
                }
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
        if (_canvasTool == 3) _canvasTool = 2;
        int brush = 0;
        if (_canvasTool == 4) brush = 1;
        int nextBrush = GUILayout.Toolbar(brush, new[] { "Màu", "Chìa khóa nhóm" });
        if (nextBrush != brush)
        {
            CancelCanvasGesture();
            _canvasTool = 2;
            if (nextBrush == 1) _canvasTool = 4;
            Repaint();
            GUIUtility.ExitGUI();
        }
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

    private void DrawRoadQuickBar(JObject root)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("🛣️ Mẫu đường:", GUILayout.Width(82));
            if (GUILayout.Button(new GUIContent("⭕ Tròn", "Tạo đường vòng tròn khép kín"), GUILayout.Height(22)))
            {
                WorkspaceCommand("Tạo đường tròn", next =>
                {
                    JArray paths = A.Array(A.Layout(next), "Paths");
                    paths.Clear();
                    paths.Add(A.CirclePath(4f));
                    A.SyncEnterExitGates(A.Layout(next));
                    _path = 0; _point = 0;
                });
            }
            if (GUILayout.Button(new GUIContent("▭ Chữ nhật", "Tạo đường chữ nhật bo góc khép kín"), GUILayout.Height(22)))
            {
                WorkspaceCommand("Tạo đường chữ nhật", next =>
                {
                    JArray paths = A.Array(A.Layout(next), "Paths");
                    paths.Clear();
                    paths.Add(A.RoundedRectangleRoad(Vector3.zero, 8f, 6f, 1.2f));
                    A.SyncEnterExitGates(A.Layout(next));
                    _path = 0; _point = 0;
                });
            }
            if (GUILayout.Button(new GUIContent("↪ Chữ U (Cổng)", "Tạo đường chữ U mở có cổng Vào và Ra"), GUILayout.Height(22)))
            {
                WorkspaceCommand("Tạo đường chữ U", next =>
                {
                    JArray paths = A.Array(A.Layout(next), "Paths");
                    paths.Clear();
                    paths.Add(A.UShapeRoad(Vector3.zero, 8f, 6.5f, 0.6f));
                    A.SyncEnterExitGates(A.Layout(next));
                    _path = 0; _point = 0;
                });
            }
            if (GUILayout.Button(new GUIContent("〰️ Chữ S (Cổng)", "Tạo đường chữ S mở uốn lượn có cổng Vào và Ra"), GUILayout.Height(22)))
            {
                WorkspaceCommand("Tạo đường chữ S", next =>
                {
                    JArray paths = A.Array(A.Layout(next), "Paths");
                    paths.Clear();
                    paths.Add(A.SCurveRoad(Vector3.zero, 8f, 6.5f, 0.6f));
                    A.SyncEnterExitGates(A.Layout(next));
                    _path = 0; _point = 0;
                });
            }
            if (GUILayout.Button(new GUIContent("➖ Thẳng (Cổng)", "Tạo đường thẳng mở có cổng Vào và Ra"), GUILayout.Height(22)))
            {
                WorkspaceCommand("Tạo đường thẳng", next =>
                {
                    JArray paths = A.Array(A.Layout(next), "Paths");
                    paths.Clear();
                    paths.Add(A.StraightRoadCentered(Vector3.zero, 8f, false));
                    A.SyncEnterExitGates(A.Layout(next));
                    _path = 0; _point = 0;
                });
            }
            GUILayout.Space(6);
            Color prevBg = GUI.backgroundColor;
            if (_drawingRoad) GUI.backgroundColor = new Color(1f, 0.65f, 0.2f);
            if (GUILayout.Button(new GUIContent(_drawingRoad ? "✏️ Đang vẽ (Esc)" : "✏️ Bút vẽ tự do", "Bấm để vẽ đường tự do bằng cách click các điểm trên bàn"), GUILayout.Height(22)))
            {
                if (_drawingRoad) CancelRoadSketch();
                else { CancelRoadSketch(); _drawingRoad = true; _status = "Bấm chuột trên bàn chơi để đặt các mấu neo vẽ đường mới. Bấm điểm đầu hoặc Enter để hoàn tất."; }
            }
            GUI.backgroundColor = prevBg;
            if (GUILayout.Button(new GUIContent("🔄 Đảo chiều", "Đảo ngược thứ tự điểm của băng chuyền"), GUILayout.Height(22)))
            {
                WorkspaceCommand("Đảo chiều đường", next =>
                {
                    JArray paths = A.Array(A.Layout(next), "Paths");
                    if (paths.Count > 0)
                    {
                        A.ReverseRoad((JObject)paths[0], 0.6f);
                        A.SyncEnterExitGates(A.Layout(next));
                    }
                });
            }
            if (GUILayout.Button(new GUIContent("🚪 Đồng bộ cổng", "Khớp cổng Vào/Ra với đầu/cuối đường"), GUILayout.Height(22)))
            {
                WorkspaceCommand("Đồng bộ cổng Vào/Ra", next => A.SyncEnterExitGates(A.Layout(next)));
            }
            if (GUILayout.Button(new GUIContent("⚡ Làm mượt", "Làm cong mượt các góc nhọn của đường"), GUILayout.Height(22)))
            {
                WorkspaceCommand("Làm mượt đường", next =>
                {
                    JArray paths = A.Array(A.Layout(next), "Paths");
                    if (paths.Count > 0) A.SmoothRoad((JObject)paths[0], 0.6f);
                });
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
        float horizontalScale = (rect.width - 48) / Mathf.Max(1, bounds.size.x);
        float verticalScale = (rect.height - 48) / Mathf.Max(1, bounds.size.z);
        _canvasScale = Mathf.Clamp(Mathf.Min(horizontalScale, verticalScale), .5f, 360);
        _canvasFramed = true;
    }
    private void ZoomCanvas(Rect rect, Vector2 anchor, float multiplier)
    {
        if (_canvasViewLocked) return;
        if (rect.width < 10 || rect.height < 10) return;
        CancelCanvasGesture();
        Vector3 before = CanvasWorld(rect, anchor);
        _canvasScale = Mathf.Clamp(_canvasScale * multiplier, .5f, 360);
        Vector3 after = CanvasWorld(rect, anchor);
        _canvasCenter += new Vector2(before.x - after.x, before.z - after.z);
        _canvasFramed = true;
        Repaint();
    }
    private static Vector3 CanvasCubePosition(int type, int index) => type == 1 ? Vector3.zero : new Vector3(0, 0, (type == 2 ? -2.8f : -1.2f) + index * .8f);
    private static Vector3 CanvasBoxPoint(JObject box, Vector3 local) => A.Vector(box, "Position") +
        (A.Int(box, "BoxType") == 1 ? Quaternion.identity : Quaternion.Euler(A.Vector(box, "Rotation"))) * local;

    private void DrawCanvas(Rect rect, JObject root)
    {
        if (_canvasViewLocked && _canvasRect.size != rect.size) _canvasFramed = false;
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
                    bool isLinked = A.Int(cube, "LinkedPairId") > 0;
                    string text = (hidden ? "?" : "") + (A.Int(cube, "GroupKeyId") > 0 && !(hidden && _canvasPlayerView) ? "K" + A.Int(cube, "GroupKeyId") : "") + (isLinked ? "🔗" : "");
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
                if (LoopLinkedCubeAuthoring.HasLinkedCube(box)) badge += " 🔗";
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
            foreach (LinkedCubePairRef pairRef in LoopLinkedCubeAuthoring.GetLinkedPairs(layout))
            {
                if (pairRef.BoxA >= 0 && pairRef.BoxA < boxes.Count && pairRef.BoxB >= 0 && pairRef.BoxB < boxes.Count)
                {
                    JObject bA = (JObject)boxes[pairRef.BoxA];
                    JObject bB = (JObject)boxes[pairRef.BoxB];
                    Vector3 worldPosA = CanvasBoxPoint(bA, CanvasCubePosition(A.Int(bA, "BoxType"), pairRef.CubeA));
                    Vector3 worldPosB = CanvasBoxPoint(bB, CanvasCubePosition(A.Int(bB, "BoxType"), pairRef.CubeB));
                    Vector3 ptA = project(worldPosA);
                    Vector3 ptB = project(worldPosB);

                    Handles.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);
                    Handles.DrawAAPolyLine(6f, ptA, ptB);

                    Handles.color = new Color(1f, 0.6f, 0.2f, 0.95f);
                    Handles.DrawAAPolyLine(2.5f, ptA, ptB);

                    Handles.DrawSolidDisc(ptA, Vector3.forward, 5f);
                    Handles.DrawSolidDisc(ptB, Vector3.forward, 5f);
                    Handles.color = Color.black;
                    Handles.DrawWireDisc(ptA, Vector3.forward, 5f);
                    Handles.DrawWireDisc(ptB, Vector3.forward, 5f);

                    Vector3 mid = (ptA + ptB) * 0.5f;
                    Handles.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
                    Handles.DrawSolidDisc(mid, Vector3.forward, 10f);
                    Handles.color = new Color(1f, 0.6f, 0.2f, 0.95f);
                    Handles.DrawWireDisc(mid, Vector3.forward, 10f);
                    GUI.Label(new Rect(mid.x - 7, mid.y - 9, 20, 20), "🔗", EditorStyles.boldLabel);
                }
            }
            DrawCanvasMapItems(rect, layout);
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
            DrawCanvasDragPreview(rect, root);
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
    private void BeginCanvasGesture(int kind, JObject root, Rect rect, Vector2 mouse)
    {
        if (_canvasViewLocked && kind == 2) return;
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
        if (HandleCanvasDragAndDrop(rect, root, inside)) return;
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
            ZoomCanvas(rect, e.mousePosition, Mathf.Pow(1.1f, -e.delta.y));
            e.Use(); return;
        }
        if(e.type==EventType.KeyDown&&GUIUtility.keyboardControl==_canvasFocus&&!EditorGUIUtility.editingTextField)
        {
            if(e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha9)
            {
                _brushColor=e.keyCode-KeyCode.Alpha0; _status="Đã chọn màu cọ: "+_brushColor;
                e.Use();Repaint();return;
            }
            if(e.keyCode==KeyCode.Q){_canvasTool=6;_status="Công cụ: 1. Đường đi";e.Use();Repaint();return;}
            if(e.keyCode==KeyCode.W){_canvasTool=0;_status="Công cụ: 2. Hộp & Cổng";e.Use();Repaint();return;}
            if(e.keyCode==KeyCode.E){_canvasTool=2;_status="Công cụ: 3. Tô màu khối";e.Use();Repaint();return;}
            if(e.keyCode==KeyCode.R){_canvasTool=5;_status="Công cụ: 4. Xóa (Eraser)";e.Use();Repaint();return;}
            if(e.keyCode==KeyCode.T){_canvasTool=6;_status="Công cụ: 1. Đường đi";e.Use();Repaint();return;}
            if(!e.control&&!e.command&&!e.alt&&e.keyCode==KeyCode.A)
            {
                if(TryQuickAddUnusedColor()){e.Use();Repaint();return;}
            }
            if(e.keyCode==KeyCode.Escape)
            {
                CancelCanvasGesture();
                if(_drawingRoad)CancelRoadSketch();
                else if(_canvasTool==6 && _point>=0){_point=-1;Repaint();}
                else if(_canvasTool==5){_canvasTool=0;_status="Đã thoát chế độ Xóa.";Repaint();}
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
            if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && LoopLevelCommands.IsMapItem(A.Layout(root), _canvasMap))
            { WorkspaceCommand("Xóa MapData", DeleteSelectedMapItem); e.Use(); return; }
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
            if (_canvasTool == 5)
            {
                // Eraser mode: smart deletion of points, map items, cubes, and boxes
                if (HitCanvasPoint(rect, root, e.mousePosition))
                {
                    int rPath = _path, rPoint = _point;
                    WorkspaceCommand("Xóa điểm đường", next =>
                    {
                        JArray paths = A.Array(A.Layout(next), "Paths");
                        if (rPath >= 0 && rPath < paths.Count)
                        {
                            JObject p = (JObject)paths[rPath];
                            A.RemoveRoadPoint(p, rPoint);
                            _point = -1;
                            if (_autoSmoothPoints) A.SmoothRoad(p, _roadSmoothing);
                        }
                    });
                    e.Use(); Repaint(); GUIUtility.ExitGUI(); return;
                }
                int eraseMapHit = HitCanvasMapItem(rect, A.Layout(root), e.mousePosition);
                if (eraseMapHit >= 0)
                {
                    _canvasMap = eraseMapHit;
                    WorkspaceCommand("Xóa MapData", DeleteSelectedMapItem);
                    e.Use(); Repaint(); GUIUtility.ExitGUI(); return;
                }
                int hitBox = HitCanvasBox(rect, root, e.mousePosition, out int hitCube);
                if (hitBox >= 0)
                {
                    JObject bObj = (JObject)A.Array(A.Layout(root), "Boxes")[hitBox];
                    JArray cubes = A.Array(bObj, "Cubes");
                    if (hitCube >= 0 && hitCube < cubes.Count)
                    {
                        BeginCanvasGesture(4, root, rect, e.mousePosition);
                        PaintCanvas(rect, e.mousePosition);
                        e.Use(); Repaint(); GUIUtility.ExitGUI(); return;
                    }
                    else
                    {
                        WorkspaceCommand("Xóa hộp", next =>
                        {
                            LoopLevelCommands.DeleteBoxes(next, new[] { hitBox });
                            ClearCanvasSelection();
                        });
                        e.Use(); Repaint(); GUIUtility.ExitGUI(); return;
                    }
                }
                return;
            }
            if(_canvasTool==1)
            {
                Vector3 position=Snap(CanvasWorld(rect,e.mousePosition,_placementHeight));
                WorkspaceCommand("Đặt hộp",next=> {JObject box=A.NewBox(position);box["BoxType"]=_placementType;box["Rotation"]=A.Vector(new Vector3(0,_placementYaw,0));JArray boxes=A.Array(A.Layout(next),"Boxes");boxes.Add(box);SetCanvasSelection(new[]{boxes.Count-1});});e.Use();return;
            }
            if (_canvasTool >= 2 && _canvasTool <= 4)
            {
                int brushBox = HitCanvasBox(rect, root, e.mousePosition, out int brushCube);
                if (brushBox >= 0 && (e.shift || brushCube < 0))
                {
                    var selected = new List<int>(_canvasSelection);
                    if (!e.shift) selected.Clear();
                    if (!selected.Remove(brushBox)) selected.Add(brushBox);
                    SetCanvasSelection(selected);
                }
                else if (brushBox >= 0)
                {
                    BeginCanvasGesture(4, root, rect, e.mousePosition);
                    PaintCanvas(rect, e.mousePosition);
                }
                e.Use(); Repaint(); GUIUtility.ExitGUI(); return;
            }
            int mapHit = HitCanvasMapItem(rect, A.Layout(root), e.mousePosition);
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
                if (LoopLevelCommands.IsMapItem(layout, _canvasMap))
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
        if (LoopLevelCommands.IsMapItem(layout, _canvasMap))
        {
            DrawMapItemInspector(root);
            return;
        }
        Header(_canvasSelection.Count+" hộp được chọn");
        if(_box<0||_box>=boxes.Count){EditorGUILayout.HelpBox("Bấm hộp để sửa. Shift hoặc quét vùng để chọn nhiều. Chọn Đặt hộp để dựng bố cục.",MessageType.None);return;}
        JObject editable=(JObject)root.DeepClone();JObject box=(JObject)A.Array(A.Layout(editable),"Boxes")[_box];
        Header("Hộp "+_box+" — "+BoxTypeLabel(A.Int(box,"BoxType")));
        DrawWorkspaceCubeSecrets(box);
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
        Header("Cube đôi (Linked Cube Pair)");
        bool hasBoxPair = LoopLinkedCubeAuthoring.TryGetLinkedPairForBox(layout, _box, out LinkedCubePairRef boardPair);
        bool boxHasLinkedCube = false;
        int linkedCubeIndex = -1;
        JArray boxCubes = A.Array(box, "Cubes");
        for (int c = 0; c < boxCubes.Count; c++)
        {
            if (A.Int((JObject)boxCubes[c], "LinkedPairId") > 0)
            {
                boxHasLinkedCube = true;
                linkedCubeIndex = c;
                break;
            }
        }

        if (boxHasLinkedCube && hasBoxPair)
        {
            int partnerBox = (_box == boardPair.BoxA) ? boardPair.BoxB : boardPair.BoxA;
            int partnerCube = (_box == boardPair.BoxA) ? boardPair.CubeB : boardPair.CubeA;
            EditorGUILayout.HelpBox($"🔗 Khối #{linkedCubeIndex + 1} của hộp này đang nối với Hộp {partnerBox} (Khối #{partnerCube + 1}).", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"🔍 Chọn Hộp {partnerBox}"))
                {
                    SetCanvasSelection(new[] { partnerBox });
                }
                if (GUILayout.Button("Tháo liên kết"))
                {
                    WorkspaceCommand("Tháo liên kết Cube đôi", next => LoopLinkedCubeAuthoring.Unlink(next, boardPair.Id));
                }
            }
        }
        else
        {
            var otherEligibleBoxes = new List<int>();
            for (int b = 0; b < boxes.Count; b++)
            {
                if (b != _box && A.Int((JObject)boxes[b], "BoxType") == 0 &&
                    !LoopLinkedCubeAuthoring.HasLinkedCube((JObject)boxes[b]) && A.Array((JObject)boxes[b], "Cubes").Count > 0)
                {
                    otherEligibleBoxes.Add(b);
                }
            }

            if (A.Int(box, "BoxType") != 0)
            {
                EditorGUILayout.LabelField("Cube đôi chỉ hỗ trợ hộp thường (Normal).", EditorStyles.wordWrappedMiniLabel);
            }
            else if (boxCubes.Count == 0)
            {
                EditorGUILayout.LabelField("Hộp cần có ít nhất 1 khối để nối Cube đôi.", EditorStyles.wordWrappedMiniLabel);
            }
            else if (otherEligibleBoxes.Count == 0)
            {
                EditorGUILayout.LabelField("Cần ít nhất một hộp thường có khối khác để nối.", EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                string[] options = otherEligibleBoxes.Select(b => $"Hộp {b} ({A.Array((JObject)boxes[b], "Cubes").Count} khối)").ToArray();
                int currentChoiceIndex = Mathf.Clamp(otherEligibleBoxes.IndexOf(_partnerBoxChoice), 0, otherEligibleBoxes.Count - 1);
                currentChoiceIndex = EditorGUILayout.Popup("Chọn hộp đối ứng", currentChoiceIndex, options);
                _partnerBoxChoice = otherEligibleBoxes[currentChoiceIndex];

                _linkedCubeChoice = DrawCubeIndexPopup("Khối hộp này", boxCubes, _linkedCubeChoice);
                JArray partnerCubes = A.Array((JObject)boxes[_partnerBoxChoice], "Cubes");
                _partnerCubeChoice = DrawCubeIndexPopup("Khối hộp đối ứng", partnerCubes, _partnerCubeChoice);
                EditorGUILayout.LabelField("Khối được đánh số từ 1 ở đáy lên trên.", EditorStyles.wordWrappedMiniLabel);

                if (GUILayout.Button($"🔗 Nối khối {_linkedCubeChoice + 1} ↔ khối {_partnerCubeChoice + 1}"))
                {
                    int partner = _partnerBoxChoice;
                    int firstCube = _linkedCubeChoice;
                    int secondCube = _partnerCubeChoice;
                    WorkspaceCommand("Tạo liên kết Cube đôi", next =>
                    {
                        if (LoopLinkedCubeAuthoring.TryLinkCubes(next, _box, firstCube, partner, secondCube, out string err))
                        {
                            _status = $"Đã nối Hộp {_box} (khối {firstCube + 1}) với Hộp {partner} (khối {secondCube + 1}).";
                        }
                        else
                        {
                            _status = "Không thể nối Cube đôi: " + err;
                        }
                    });
                }
            }
        }
    }

    private void DuplicateWorkspace(bool mechanisms)
    {
        WorkspaceCommand(mechanisms?"Nhân bản cụm cơ chế":"Nhân bản hộp (bỏ chìa khóa nhóm)",next=>SetCanvasSelection(LoopLevelCommands.Duplicate(next,_canvasSelection,Vector3.right*2,mechanisms)));
    }
    private void DrawWorkspaceOperations(JObject root)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Căn chỉnh:", GUILayout.Width(75));
            if (GUILayout.Button("Căn X")) WorkspaceCommand("Căn thẳng hàng X", next => LoopLevelBoardLayout.AlignBoxes(A.Array(A.Layout(next), "Boxes"), _canvasSelection, false), true);
            if (GUILayout.Button("Căn Z")) WorkspaceCommand("Căn thẳng hàng Z", next => LoopLevelBoardLayout.AlignBoxes(A.Array(A.Layout(next), "Boxes"), _canvasSelection, true), true);
            if (GUILayout.Button("Giãn đều X")) WorkspaceCommand("Giãn đều X", next => LoopLevelCommands.Align(next, _canvasSelection, true, true), true);
            if (GUILayout.Button("Giãn đều Z")) WorkspaceCommand("Giãn đều Z", next => LoopLevelCommands.Align(next, _canvasSelection, false, true), true);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Xoay hướng:", GUILayout.Width(75));
            if (GUILayout.Button("0° (Bắc)")) WorkspaceCommand("Xoay 0°", next => LoopLevelBoardLayout.RotateBoxes(A.Array(A.Layout(next), "Boxes"), _canvasSelection, 0f), true);
            if (GUILayout.Button("90° (Đông)")) WorkspaceCommand("Xoay 90°", next => LoopLevelBoardLayout.RotateBoxes(A.Array(A.Layout(next), "Boxes"), _canvasSelection, 90f), true);
            if (GUILayout.Button("180° (Nam)")) WorkspaceCommand("Xoay 180°", next => LoopLevelBoardLayout.RotateBoxes(A.Array(A.Layout(next), "Boxes"), _canvasSelection, 180f), true);
            if (GUILayout.Button("270° (Tây)")) WorkspaceCommand("Xoay 270°", next => LoopLevelBoardLayout.RotateBoxes(A.Array(A.Layout(next), "Boxes"), _canvasSelection, 270f), true);
            if (GUILayout.Button("Hướng tâm")) WorkspaceCommand("Xoay hướng tâm", next => LoopLevelBoardLayout.RotateBoxesTowardsCenter(A.Array(A.Layout(next), "Boxes"), _canvasSelection), true);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Bố cục mẫu:", GUILayout.Width(75));
            if (GUILayout.Button("2 Cột"))
                WorkspaceCommand("Xếp 2 cột", next => LoopLevelBoardLayout.ArrangeTwoColumnsFacingOutward(A.Array(A.Layout(next), "Boxes"), _canvasSelection), true);
            if (GUILayout.Button("2 Hàng"))
                WorkspaceCommand("Xếp 2 hàng", next => LoopLevelBoardLayout.ArrangeTwoBanks(A.Array(A.Layout(next), "Boxes"), _canvasSelection), true);
            if (GUILayout.Button("1 Hàng"))
                WorkspaceCommand("Xếp 1 hàng", next => LoopLevelBoardLayout.ArrangeSingleRow(A.Array(A.Layout(next), "Boxes"), _canvasSelection), true);
            if (GUILayout.Button("Lưới"))
                WorkspaceCommand("Xếp dạng lưới", next => LoopLevelBoardLayout.ArrangeGrid(A.Array(A.Layout(next), "Boxes"), _canvasSelection, 2), true);
            if (GUILayout.Button("Phản chiếu X")) PreviewWorkspaceCommand("Phản chiếu cụm", next => LoopLevelCommands.Mirror(next, _canvasSelection), true);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Cube đôi:", GUILayout.Width(75));
            LinkedCubePairRef selectedPair;
            bool hasPair;
            if (_canvasSelection.Count > 0)
                hasPair = LoopLinkedCubeAuthoring.TryGetLinkedPairForBox(A.Layout(root), _canvasSelection[0], out selectedPair);
            else hasPair = LoopLinkedCubeAuthoring.TryGetLinkedPair(A.Layout(root), out selectedPair);
            bool canLinkTwo = _canvasSelection.Count == 2;
            using (new EditorGUI.DisabledScope(!canLinkTwo))
            {
                if (GUILayout.Button(new GUIContent("🔗 Nối 2 hộp đã chọn", "Tạo liên kết khối đỉnh giữa 2 hộp đang chọn trên Canvas/Bảng")))
                {
                    WorkspaceCommand("Tạo liên kết Cube đôi", next =>
                    {
                        if (LoopLinkedCubeAuthoring.TryLinkTopCubes(next, _canvasSelection[0], _canvasSelection[1], out string err))
                        {
                            _status = $"Đã tạo Cube đôi liên kết giữa Hộp {_canvasSelection[0]} và Hộp {_canvasSelection[1]}.";
                        }
                        else
                        {
                            _status = "Không thể nối Cube đôi: " + err;
                        }
                    });
                }
            }
            using (new EditorGUI.DisabledScope(!hasPair))
            {
                if (GUILayout.Button(new GUIContent("❌ Hủy nối Cube đôi", "Xóa liên kết Cube đôi hiện có")))
                {
                    WorkspaceCommand("Hủy nối Cube đôi", next =>
                    {
                        LoopLinkedCubeAuthoring.Unlink(next, selectedPair.Id);
                        _status = "Đã gỡ bỏ liên kết Cube đôi.";
                    });
                }
                if (GUILayout.Button(new GUIContent($"🔍 Chọn cặp ({selectedPair.BoxA} ↔ {selectedPair.BoxB})", "Chọn 2 hộp đang nối dây trên Canvas")))
                {
                    SetCanvasSelection(new[] { selectedPair.BoxA, selectedPair.BoxB });
                }
            }
        }
    }

    private void DrawWorkspaceRoadOperations()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Đường & Cổng:", GUILayout.Width(75));
            if (GUILayout.Button("🔄 Đảo chiều"))
                WorkspaceCommand("Đảo chiều đường", next => {
                    JArray paths = A.Array(A.Layout(next), "Paths");
                    if (paths.Count > 0) { A.ReverseRoad((JObject)paths[0], 0.6f); A.SyncEnterExitGates(A.Layout(next)); }
                });
            if (GUILayout.Button("Đổi Đóng ⇄ Mở"))
                WorkspaceCommand("Đổi Đóng ⇄ Mở đường", next => {
                    JArray paths = A.Array(A.Layout(next), "Paths");
                    if (paths.Count > 0) A.ToggleRoadClosed(A.Layout(next), (JObject)paths[0], 0.6f);
                });
            if (GUILayout.Button("🚪 Đồng bộ cổng"))
                WorkspaceCommand("Đồng bộ cổng Vào/Ra", next => A.SyncEnterExitGates(A.Layout(next)));
            if (GUILayout.Button("⚡ Bo đường quanh hộp"))
                WorkspaceCommand("Bo đường quanh hộp", next => LoopLevelBoardLayout.FitRoadOnly(A.Layout(next), _generatorSettings ?? new LoopLevelGeneratorSettings()));
        }
    }

    private void DeleteSelectedBoxes(JObject root)
    {
        LoopLevelCommands.DeleteBoxes(root, _canvasSelection);
        ClearCanvasSelection();
    }

    private Rect CanvasMapItemRect(Rect rect, JObject item)
    {
        Vector2 center = CanvasProject(rect, A.Vector(item, "Position"));
        string name = item.Value<string>("Name");
        bool isGate = string.Equals(name, "Enter", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(name, "Exit", StringComparison.OrdinalIgnoreCase);
        Vector2 size = isGate
            ? new Vector2(Mathf.Max(38, 2.2f * _canvasScale), Mathf.Max(26, 1.4f * _canvasScale))
            : new Vector2(Mathf.Max(40, 2f * _canvasScale), Mathf.Max(20, _canvasScale));
        return new Rect(center - size * .5f, size);
    }

    private int HitCanvasMapItem(Rect rect, JObject layout, Vector2 mouse)
    {
        JArray map = A.Array(layout, "MapData");
        for (int i = map.Count - 1; i >= 0; i--)
        {
            if (LoopLevelCommands.IsMapItem(layout, i) && CanvasMapItemRect(rect, (JObject)map[i]).Contains(mouse))
                return i;
        }
        return -1;
    }

    private void DrawCanvasMapItems(Rect rect, JObject layout)
    {
        JArray map = A.Array(layout, "MapData");
        for (int i = 0; i < map.Count; i++)
        {
            if (!LoopLevelCommands.IsMapItem(layout, i, out string name)) continue;
            JObject item = (JObject)map[i];
            Rect marker = CanvasMapItemRect(rect, item);
            marker.position -= rect.position;

            bool selected = _canvasMap == i;
            if (LoopLevelCommands.IsCapacity(layout, i))
            {
                Color color = selected ? new Color(.5f, .45f, .15f) : new Color(.15f, .4f, .3f);
                EditorGUI.DrawRect(marker, color);
                GUI.Label(marker, "Capacity 0/" + A.Int(layout, "PathLength"), EditorStyles.whiteMiniLabel);
            }
            else if (LoopLevelCommands.IsEnter(layout, i))
            {
                Color color = selected ? new Color(0.9f, 0.85f, 0.2f) : new Color(0.12f, 0.65f, 0.22f, 0.95f);
                EditorGUI.DrawRect(marker, color);
                GUI.Label(marker, "🚪 VÀO", EditorStyles.whiteBoldLabel);
            }
            else if (LoopLevelCommands.IsExit(layout, i))
            {
                Color color = selected ? new Color(1f, 0.85f, 0.2f) : new Color(0.82f, 0.22f, 0.18f, 0.95f);
                EditorGUI.DrawRect(marker, color);
                GUI.Label(marker, "🚪 RA", EditorStyles.whiteBoldLabel);
            }
            else
            {
                Color color = selected ? Color.yellow : new Color(0.3f, 0.35f, 0.45f);
                EditorGUI.DrawRect(marker, color);
                GUI.Label(marker, name, EditorStyles.whiteMiniLabel);
            }

            if (LoopLevelCommands.IsEnter(layout, i) || LoopLevelCommands.IsExit(layout, i))
            {
                Vector3 itemPos = A.Vector(item, "Position");
                Vector3 itemRot = A.Vector(item, "Rotation");
                float rad = itemRot.y * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
                Vector3 facingCenter = (Vector3)(CanvasProject(rect, itemPos) - rect.position);
                Vector3 facingTip = (Vector3)(CanvasProject(rect, itemPos + dir * 1.8f) - rect.position);
                Handles.color = selected ? Color.yellow : (LoopLevelCommands.IsEnter(layout, i) ? Color.green : new Color(1f, 0.4f, 0.2f));
                Handles.DrawLine(facingCenter, facingTip);
                Handles.DrawSolidDisc(facingTip, Vector3.forward, 3.5f);
            }
        }
    }

    private void DrawMapItemInspector(JObject root)
    {
        JObject layout = A.Layout(root);
        JArray map = A.Array(layout, "MapData");
        if (_canvasMap < 0 || _canvasMap >= map.Count || !(map[_canvasMap] is JObject item)) return;

        string name = item.Value<string>("Name");
        bool isEnter = LoopLevelCommands.IsEnter(layout, _canvasMap);
        bool isExit = LoopLevelCommands.IsExit(layout, _canvasMap);
        bool isCapacity = LoopLevelCommands.IsCapacity(layout, _canvasMap);

        string title = isEnter ? "Cổng Vào (Enter Gate)" : (isExit ? "Cổng Ra (Exit Gate)" : (isCapacity ? "Bảng Capacity" : ("Mục: " + name)));
        Header(title);

        if (isEnter || isExit)
        {
            EditorGUILayout.HelpBox("Cổng đón/xuất khối ở đầu và cuối đường mở (IsCircle = 0). Có thể kéo trên bàn hoặc bấm đồng bộ với đường.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Kéo bằng công cụ Chọn. Kiểm tra hình ảnh prefab trong Game View.", MessageType.None);
        }

        JObject editable = (JObject)root.DeepClone();
        JObject editItem = (JObject)A.Array(A.Layout(editable), "MapData")[_canvasMap];
        EditorGUI.BeginChangeCheck();
        VectorField(editItem, "Position");
        VectorField(editItem, "Rotation");
        VectorField(editItem, "Scale");
        if (EditorGUI.EndChangeCheck() && _canvasGesture == 0)
        {
            Commit(editable, "Sửa " + title);
            GUIUtility.ExitGUI();
        }

        if (isEnter || isExit)
        {
            if (GUILayout.Button("⚡ Tự động căn theo đầu/cuối đường mở"))
            {
                WorkspaceCommand("Đồng bộ cổng vào ra", next => LoopLevelAuthoring.SyncEnterExitGates(A.Layout(next)));
            }
        }

        if (GUILayout.Button("Xóa mục này"))
        {
            WorkspaceCommand("Xóa MapData", DeleteSelectedMapItem);
        }
    }

    private void DeleteSelectedMapItem(JObject root)
    {
        JObject layout = A.Layout(root);
        if (LoopLevelCommands.IsMapItem(layout, _canvasMap))
            LoopLevelCommands.DeleteMapItem(layout, _canvasMap);
        ClearCanvasSelection();
    }

    private void DrawWorkspaceValidation(JObject root)
    {
        JObject layout=A.Layout(root);JArray boxes=A.Array(layout,"Boxes");
        GUILayout.Label(boxes.Count + " hộp · Kiểm tra cấu trúc và lời giải", EditorStyles.wordWrappedMiniLabel);
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("Kiểm tra cấu trúc")){_errors=A.Validate(_canvasProposal ?? root,out _warnings);_hasValidation=true;}
            _solverBudget=EditorGUILayout.IntField("Trạng thái tối đa",_solverBudget,GUILayout.Width(230));
            if(_draftSearch==null){if(GUILayout.Button(_canvasProposal == null ? "Tìm lời giải bản nháp" : "Tìm lời giải bản xem trước"))StartDraftSolver(_canvasProposal ?? root);}
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
        if(!string.IsNullOrEmpty(_draftSolverStatus))GUILayout.Label(IsDraftSolverCurrent() ? _draftSolverStatus : "Dữ liệu đã đổi — cần tìm lời giải lại.",EditorStyles.wordWrappedMiniLabel);
    }
    private void StartDraftSolver(JObject root)
    {
        StopDraftSolver();
        _draftSolverUsesProposal = ReferenceEquals(root, _canvasProposal);
        _draftSolverProposal = _draftSolverUsesProposal ? root : null;
        _draftSolverJson = root.ToString(Formatting.Indented);
        List<string> errors=A.Validate(root,out _);if(errors.Count>0){_status=string.Join("\n",errors.Take(3));_draftSolverStatus = "Dữ liệu có lỗi cấu trúc; chưa tìm lời giải.";return;}
        JObject layout=A.Layout(root);
        if(A.Array(layout,"Covers").Count>0||A.Array(layout,"Boxes").OfType<JObject>().Any(b=>A.Array(b,"LinkedCubes").Count>0||A.Array(b,"MultiColorLock").Count>0||A.Array(b,"Cubes").OfType<JObject>().Any(c=>A.Int(c,"IsFixed")>0)))
        {_draftSolverStatus="Có cơ chế chưa được kiểm chứng bởi mô phỏng này. Hãy chơi thử.";return;}
        _draftSolverStatus="Đang tìm…";
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
        if(!IsDraftSolverCurrent()){StopDraftSolver();_draftSolverStatus="Dữ liệu đã đổi; đã hủy tìm.";return;}
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

    private bool IsDraftSolverCurrent()
    {
        if (_document == null) return false;
        if (_draftSolverUsesProposal)
        {
            if (_canvasProposal == null || _proposalBase != _document.Json) return false;
            // A preview is replaced as a whole; its editing controls stay disabled until Apply/Cancel.
            return ReferenceEquals(_draftSolverProposal, _canvasProposal);
        }
        return _canvasProposal == null && _draftSolverJson == _document.Json;
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

        if (!isClosed)
        {
            Color prevGateBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.4f);
            if (GUILayout.Button(new GUIContent("⚡ Đồng bộ Cổng Vào / Cổng Ra theo đường mở", "Tự động đặt Enter ở điểm đầu và Exit ở điểm cuối đúng góc xoay")))
            {
                WorkspaceCommand("Đồng bộ cổng vào/ra", next => LoopLevelAuthoring.SyncEnterExitGates(A.Layout(next)));
            }
            GUI.backgroundColor = prevGateBg;
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
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Đường mở (Có Cổng Vào/Ra tự động):", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Thẳng ngang (X)"))
                {
                    WorkspaceCommand("Tạo đường thẳng ngang", next =>
                    {
                        JObject layout = A.Layout(next);
                        layout["Paths"] = new JArray(A.StraightRoadCentered(Vector3.zero, 14f, true));
                        A.SyncEnterExitGates(layout);
                        _path = 0; _point = 0;
                    });
                }
                if (GUILayout.Button("Thẳng dọc (Z)"))
                {
                    WorkspaceCommand("Tạo đường thẳng dọc", next =>
                    {
                        JObject layout = A.Layout(next);
                        layout["Paths"] = new JArray(A.StraightRoadCentered(Vector3.zero, 14f, false));
                        A.SyncEnterExitGates(layout);
                        _path = 0; _point = 0;
                    });
                }
                if (GUILayout.Button("Chữ S"))
                {
                    WorkspaceCommand("Tạo đường chữ S", next =>
                    {
                        JObject layout = A.Layout(next);
                        layout["Paths"] = new JArray(A.SCurveRoad(Vector3.zero, 8f, 14f, _roadSmoothing));
                        A.SyncEnterExitGates(layout);
                        _path = 0; _point = 0;
                    });
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Lượn sóng (Winding)"))
                {
                    WorkspaceCommand("Tạo đường lượn sóng", next =>
                    {
                        JObject layout = A.Layout(next);
                        layout["Paths"] = new JArray(A.OpenWindingRoad(Vector3.zero, 10f, 16f, _roadSmoothing));
                        A.SyncEnterExitGates(layout);
                        _path = 0; _point = 0;
                    });
                }
                if (GUILayout.Button("Băng chuyền cạnh hộp"))
                {
                    WorkspaceCommand("Băng chuyền cạnh hộp", next =>
                    {
                        A.FitStraightRoadBesideBoxes(A.Layout(next), 2.5f, true);
                        _path = 0; _point = 0;
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
