#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using A = LoopLevelAuthoring;

public sealed partial class LoopLevelEditorWindow
{
    private sealed class TemplateEntry
    {
        public string Path;
        public JObject Preview;
        public int Number, Boxes, Hidden, Locks, Groups, EnterOnly, Limited, Adjacent;
        public string Summary => Boxes + " hộp | khối ẩn " + Hidden + " | khóa " + Locks + " | nhóm " + Groups;
    }
    [SerializeField] private string _templatePath, _templateSearch = "";
    [SerializeField] private int _templateFilter, _templatePage, _templateTargetNumber;
    [SerializeField] private bool _showBoxDetails, _showRawMembership;
    private List<TemplateEntry> _templates;
    private JObject _templatePreviewRoot;
    private const int TemplatePageSize = 8;
    [SerializeField] private bool _templatePreserveMap = true;
    [SerializeField] private int _templateMinBoxes = 2, _templateMaxBoxes = 24, _templateMechanisms;
    [SerializeField] private int _templateSubTab;
    [SerializeField] private int _selectedOfficialLevel = 1;
    [SerializeField] private int _selectedArchetypeIndex;
    private Vector2 _archetypeScroll;

    private void RefreshTemplates()
    {
        _templates = new List<TemplateEntry>(); int invalid = 0;
        if (Directory.Exists(A.LevelFolder))
            foreach (string path in Directory.GetFiles(A.LevelFolder, "Level *.json"))
                try
                {
                    JObject root = A.Parse(File.ReadAllText(path)); JObject layout = A.Layout(root);
                    var boxes = A.Array(layout, "Boxes").OfType<JObject>().ToArray();
                    _templates.Add(new TemplateEntry {
                        Path = path, Number = A.Int(root, "level_number"), Boxes = boxes.Length,
                        Preview = root,
                        Hidden = boxes.Sum(b => A.Array(b, "Cubes").OfType<JObject>().Count(c => A.Int(c, "IsSecret") > 0)),
                        Locks = boxes.Count(b => A.Int(b, "LockColor") > 0 || A.Int(b, "NumLock") > 0 || A.Int(b, "IsAdjacentLocked") > 0 || A.Array(b, "MultiColorLock").Count > 0),
                        Groups = A.Array(layout, "Groups").Count,
                        EnterOnly = boxes.Count(b => A.Int(b, "IsEnterOnly") > 0), Limited = boxes.Count(b => A.Int(b, "LimitedColor") > 0),
                        Adjacent = boxes.Count(b => A.Int(b, "IsAdjacentLocked") > 0)
                    });
                }
                catch { invalid++; }
        _templates.Sort((first, second) => first.Number.CompareTo(second.Number));
        _templatePage = 0; _templatePreviewRoot = null;
        if (invalid > 0) _status = invalid + " file mẫu không đọc được.";
    }

    private void DrawTemplates()
    {
        if (_templates == null) RefreshTemplates();
        if (_templateTargetNumber < 1) _templateTargetNumber = NextLevelNumber();
        Header("Bắt đầu từ màn chơi mẫu & Archetypes");
        _templateSubTab = GUILayout.Toolbar(_templateSubTab, new[] { "13 Mẫu Archetypes (Level 1–30)", "Chọn nhanh Level 1–30 gốc", "Duyệt thư viện màn chơi" });
        EditorGUILayout.Space(2);
        switch (_templateSubTab)
        {
            case 0: DrawArchetypeTemplates(); break;
            case 1: DrawOfficialLevelsShortcuts(); break;
            default: DrawLibraryTemplates(); break;
        }
    }

    private void DrawArchetypeTemplates()
    {
        EditorGUILayout.HelpBox("13 Mẫu thiết kế chuẩn hóa theo cấu trúc của 30 màn đầu tiên: Băng chuyền mở có cổng Vào/Ra, vòng khép kín oval/chữ nhật/chữ U, 1 hàng/1 cột/2 dãy đối xứng, cùng cơ chế khối ẩn và khóa màu hợp lệ.", MessageType.Info);
        var archetypes = LoopLevelArchetypes.All;
        if (archetypes.Count == 0) return;
        _selectedArchetypeIndex = Mathf.Clamp(_selectedArchetypeIndex, 0, archetypes.Count - 1);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_archetypeScroll, GUILayout.Width(270), GUILayout.Height(390)))
            {
                _archetypeScroll = scroll.scrollPosition;
                for (int i = 0; i < archetypes.Count; i++)
                {
                    var a = archetypes[i];
                    bool isSelected = _selectedArchetypeIndex == i;
                    string label = a.Title;
                    if (GUILayout.Toggle(isSelected, label, "Button", GUILayout.Height(28)))
                    {
                        _selectedArchetypeIndex = i;
                    }
                }
            }

            var selected = archetypes[_selectedArchetypeIndex];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Header(selected.Title);
                EditorGUILayout.LabelField(selected.Description, EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Số hộp đích:", selected.BoxCount + " hộp");
                EditorGUILayout.LabelField("Số màu sử dụng:", selected.ColorCount + " màu");
                EditorGUILayout.LabelField("Kiểu đường:", selected.IsOpenRoad ? "Đường mở (Open Road có Cổng Vào & Ra)" : "Vòng khép kín (Closed Loop)");
                string mech = (selected.HasSecrets ? "Khối ẩn (Secret)  " : "") + (selected.HasLocks ? "Khóa màu (Color Lock)  " : "");
                if (string.IsNullOrEmpty(mech)) mech = "Cơ bản";
                EditorGUILayout.LabelField("Cơ chế đặc biệt:", mech);
                EditorGUILayout.Space(6);

                _templateTargetNumber = Mathf.Max(1, EditorGUILayout.IntField("Số màn chơi mới", _templateTargetNumber));
                EditorGUILayout.Space(4);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || HasRawChanges || _drawingRoad || EditorApplication.isCompiling))
                {
                    if (GUILayout.Button("⚡ Tạo Level từ Archetype này (Áp dụng ngay)", GUILayout.Height(36)))
                    {
                        ApplyArchetype(selected, _templateTargetNumber);
                        GUIUtility.ExitGUI();
                    }
                }
                GUI.backgroundColor = prev;
            }
        }
    }

    private void ApplyArchetype(LoopLevelArchetypes.ArchetypeDef archetype, int levelNumber)
    {
        if (archetype == null || archetype.Create == null) return;
        string targetPath = Path.Combine(A.LevelFolder, "Level " + levelNumber + ".json");
        if (File.Exists(targetPath))
        {
            if (!EditorUtility.DisplayDialog("Ghi đè màn chơi?", "Màn " + levelNumber + " đã tồn tại. Ghi đè bằng màn mới từ archetype này?", "Ghi đè", "Hủy"))
                return;
        }
        JObject draft = archetype.Create(levelNumber);
        if (!ConfirmReplace()) return;
        BeginTemplateDraft(draft);
        _workspace = true;
        _editorMode = 0;
        _status = "Đã tạo màn chơi mới từ: " + archetype.Title;
    }

    private void DrawOfficialLevelsShortcuts()
    {
        EditorGUILayout.HelpBox("Chọn nhanh trong 30 màn chơi gốc có sẵn. Bấm để mở sửa trực tiếp hoặc nhân bản sang màn mới.", MessageType.Info);
        int columns = 6;
        for (int row = 0; row < 5; row++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int col = 0; col < columns; col++)
                {
                    int lvl = row * columns + col + 1;
                    bool isSelected = _selectedOfficialLevel == lvl;
                    string filePath = Path.Combine(A.LevelFolder, "Level " + lvl + ".json");
                    bool exists = File.Exists(filePath);
                    Color prevBg = GUI.backgroundColor;
                    if (isSelected) GUI.backgroundColor = Color.cyan;
                    else if (!exists) GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f);
                    if (GUILayout.Toggle(isSelected, "Màn " + lvl, "Button", GUILayout.Height(24)))
                    {
                        if (_selectedOfficialLevel != lvl)
                        {
                            _selectedOfficialLevel = lvl;
                            _templatePreviewRoot = null;
                        }
                    }
                    GUI.backgroundColor = prevBg;
                }
            }
        }

        EditorGUILayout.Space(6);
        string path = Path.Combine(A.LevelFolder, "Level " + _selectedOfficialLevel + ".json");
        if (!File.Exists(path))
        {
            EditorGUILayout.HelpBox("Không tìm thấy file Level " + _selectedOfficialLevel + ".json trong thư mục Resources/LevelData.", MessageType.Warning);
            return;
        }

        try
        {
            if (_templatePreviewRoot == null) _templatePreviewRoot = A.Parse(File.ReadAllText(path));
            JObject layout = A.Layout(_templatePreviewRoot);
            JArray boxes = A.Array(layout, "Boxes");
            JArray paths = A.Array(layout, "Paths");
            bool isCircle = paths.Count > 0 && A.Int((JObject)paths[0], "IsCircle") > 0;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Header("Màn " + _selectedOfficialLevel + " — " + Path.GetFileName(path));
                EditorGUILayout.LabelField("Cấu trúc:", boxes.Count + " hộp · " + (isCircle ? "Vòng khép kín" : "Đường mở (Open Road)"));
                DrawTemplateDiagram(_templatePreviewRoot, GUILayoutUtility.GetRect(100, 180, GUILayout.ExpandWidth(true)));

                using (new EditorGUILayout.HorizontalScope())
                {
                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.35f, 0.75f, 1f);
                    if (GUILayout.Button("▶ Mở sửa trực tiếp (Vào Bàn 2D)", GUILayout.Height(30)))
                    {
                        if (ConfirmReplace())
                        {
                            TryOpen(path);
                            _editorMode = 0;
                            _workspace = true;
                            GUIUtility.ExitGUI();
                        }
                    }
                    GUI.backgroundColor = prev;

                    _templateTargetNumber = Mathf.Max(1, EditorGUILayout.IntField("Số màn mới", _templateTargetNumber, GUILayout.Width(130)));
                    if (GUILayout.Button("Nhân bản sang màn mới", GUILayout.Height(30)))
                    {
                        if (TryUseTemplate(path, _templateTargetNumber))
                        {
                            _editorMode = 0;
                            _workspace = true;
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
        }
        catch (ExitGUIException) { throw; }
        catch (Exception ex)
        {
            EditorGUILayout.HelpBox("Lỗi đọc màn: " + ex.Message, MessageType.Warning);
        }
    }

    private void DrawLibraryTemplates()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            string search = EditorGUILayout.TextField("Số màn / tên file", _templateSearch);
            if (search != _templateSearch) { _templateSearch = search; _templatePage = 0; }
            if (GUILayout.Button("Làm mới", GUILayout.Width(70))) RefreshTemplates();
        }
        int filter = EditorGUILayout.Popup("Cơ chế", _templateFilter, new[] { "Tất cả", "Cơ bản", "Khối ẩn", "Khóa hộp", "Nhóm / chìa khóa", "Chỉ nhận vào", "Chỉ nhận màu", "Khóa liền kề" });
        if (filter != _templateFilter) { _templateFilter = filter; _templatePage = 0; }
        using (new EditorGUILayout.HorizontalScope())
        {
            _templateMinBoxes = EditorGUILayout.IntField("Hộp từ", _templateMinBoxes);
            _templateMaxBoxes = EditorGUILayout.IntField("đến", _templateMaxBoxes);
        }
        _templateMechanisms = EditorGUILayout.MaskField("Kết hợp cơ chế cần có", _templateMechanisms, new[] { "Khối ẩn", "Khóa", "Nhóm / chìa khóa", "Chỉ nhận vào", "Chỉ nhận màu", "Liền kề" });
        var matches = _templates.Where(t =>
            t.Boxes >= _templateMinBoxes && t.Boxes <= _templateMaxBoxes &&
            ((_templateMechanisms & 1) == 0 || t.Hidden > 0) && ((_templateMechanisms & 2) == 0 || t.Locks > 0) &&
            ((_templateMechanisms & 4) == 0 || t.Groups > 0) && ((_templateMechanisms & 8) == 0 || t.EnterOnly > 0) &&
            ((_templateMechanisms & 16) == 0 || t.Limited > 0) && ((_templateMechanisms & 32) == 0 || t.Adjacent > 0) &&
            (string.IsNullOrWhiteSpace(_templateSearch) || Path.GetFileName(t.Path).IndexOf(_templateSearch.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) &&
            (_templateFilter == 0 || _templateFilter == 1 && t.Hidden + t.Locks + t.Groups + t.EnterOnly + t.Limited == 0 ||
             _templateFilter == 2 && t.Hidden > 0 || _templateFilter == 3 && t.Locks > 0 || _templateFilter == 4 && t.Groups > 0 ||
             _templateFilter == 5 && t.EnterOnly > 0 || _templateFilter == 6 && t.Limited > 0 || _templateFilter == 7 && t.Adjacent > 0)).ToArray();
        int pages = Math.Max(1, (matches.Length + TemplatePageSize - 1) / TemplatePageSize);
        _templatePage = Mathf.Clamp(_templatePage, 0, pages - 1);
        EditorGUILayout.LabelField(matches.Length + " màn phù hợp / " + _templates.Count + " trong thư viện", EditorStyles.miniLabel);
        foreach (TemplateEntry entry in matches.Skip(_templatePage * TemplatePageSize).Take(TemplatePageSize))
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTemplateDiagram(entry.Preview, GUILayoutUtility.GetRect(85, 64, GUILayout.Width(85)));
                if (GUILayout.Toggle(_templatePath == entry.Path, "Màn " + entry.Number, "Button", GUILayout.Width(85)) && _templatePath != entry.Path)
                { _templatePath = entry.Path; _templatePreviewRoot = null; }
                EditorGUILayout.LabelField(entry.Summary, EditorStyles.miniLabel);
            }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(_templatePage == 0)) if (GUILayout.Button("Trang trước")) _templatePage--;
            GUILayout.Label((_templatePage + 1) + " / " + pages, GUILayout.Width(60));
            using (new EditorGUI.DisabledScope(_templatePage == pages - 1)) if (GUILayout.Button("Trang sau")) _templatePage++;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Chọn JSON màn chơi khác…"))
            {
                string path = EditorUtility.OpenFilePanel("Chọn mẫu", A.LevelFolder, "json");
                if (!string.IsNullOrEmpty(path)) { _templatePath = path; _templatePreviewRoot = null; }
            }
            using (new EditorGUI.DisabledScope(!(Selection.activeObject is TextAsset)))
                if (GUILayout.Button("Dùng JSON đang chọn trong Project"))
                { _templatePath = AssetDatabase.GetAssetPath(Selection.activeObject); _templatePreviewRoot = null; }
        }
        if (string.IsNullOrEmpty(_templatePath)) return;
        try
        {
            if (_templatePreviewRoot == null) _templatePreviewRoot = A.Parse(File.ReadAllText(_templatePath));
            EditorGUILayout.LabelField("Đã chọn", Path.GetFileName(_templatePath));
            DrawTemplateDiagram(_templatePreviewRoot, GUILayoutUtility.GetRect(100, 220, GUILayout.ExpandWidth(true)));
            _templateTargetNumber = Mathf.Max(1, EditorGUILayout.IntField("Số màn chơi mới", _templateTargetNumber));
            _templatePreserveMap = EditorGUILayout.ToggleLeft("Sao chép nguyên level (giữ MapData / trang trí)", _templatePreserveMap);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || HasRawChanges || _drawingRoad || EditorApplication.isCompiling))
                if (GUILayout.Button("Tạo bản nháp mới từ màn này", GUILayout.Height(30)))
                { if (TryUseTemplate(_templatePath, _templateTargetNumber)) GUIUtility.ExitGUI(); }
        }
        catch (ExitGUIException) { throw; }
        catch (Exception exception) { EditorGUILayout.HelpBox("Không thể xem trước mẫu: " + exception.Message, MessageType.Warning); }
    }

    public bool TryUseTemplate(string path, int levelNumber)
    {
        try
        {
            if (EditorApplication.isPlaying || HasRawChanges || _drawingRoad) return false;
            string targetPath = Path.Combine(A.LevelFolder, "Level " + levelNumber + ".json");
            if (File.Exists(targetPath))
            {
                if (!EditorUtility.DisplayDialog("Ghi đè màn chơi?", "Màn " + levelNumber + " đã tồn tại. Bạn có muốn ghi đè lên màn này không?", "Ghi đè", "Hủy"))
                    return false;
            }
            JObject draft = A.FromTemplate(A.Parse(File.ReadAllText(path)), levelNumber, _templatePreserveMap);
            List<string> errors = A.Validate(draft, out _);
            if (errors.Count > 0) throw new FormatException(string.Join("\n", errors.Take(5)));
            if (!ConfirmReplace()) return false;
            BeginTemplateDraft(draft); return true;
        }
        catch (Exception exception) { _status = "Không thể tạo bản nháp từ mẫu: " + exception.Message; Repaint(); return false; }
    }

    private void BeginTemplateDraft(JObject draft)
    {
        CancelCanvasGesture(); ClearWorkspaceProposal(); ClearCanvasSelection(); StopDraftSolver(); _canvasFramed = false; _workspace = true;
        CancelRoadSketch(); StopGeneration();
        Undo.IncrementCurrentGroup(); Undo.RecordObject(this, "Tạo màn chơi từ mẫu");
        Commit(draft, "Tạo màn chơi từ mẫu");
        _filePath = _diskText = null; _rawText = _rawBase = null;
        _savedJson = A.NewLevel(A.Int(draft, "level_number")).ToString(Formatting.Indented);
        _editorMode = 0; _tab = 1; _box = A.Array(A.Layout(draft), "Boxes").Count > 0 ? 0 : -1;
        _point = -1; _path = 0; _scroll = Vector2.zero; _boxTool = _cubeBrushMode = 0;
        Undo.IncrementCurrentGroup(); _scenePreview = true;
        _status = "Đã tạo bản nháp từ mẫu. Sửa trong bảng, chơi thử rồi lưu thành Level " + A.Int(draft, "level_number") + ".json.";
        SyncDirty(); Repaint(); SceneView.RepaintAll();
    }

    private static void DrawTemplateDiagram(JObject root, Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;
        JObject layout = A.Layout(root); Bounds bounds = LoopLevelBoardLayout.BoardBounds(layout, true);
        float scale = Math.Min((rect.width - 24) / Math.Max(1, bounds.size.x), (rect.height - 24) / Math.Max(1, bounds.size.z));
        Func<Vector3, Vector3> project = p => new Vector3(rect.center.x + (p.x - bounds.center.x) * scale, rect.center.y - (p.z - bounds.center.z) * scale, 0);
        EditorGUI.DrawRect(rect, new Color(.1f, .12f, .16f)); Handles.BeginGUI();
        Color oldColor = Handles.color;
        try
        {
            Handles.color = Color.cyan;
            foreach (JObject path in A.Array(layout, "Paths"))
            {
                JArray points = A.Array(path, "ControlPoints"); if (points.Count < 2) continue;
                var line = new List<Vector3>();
                for (int s = 0; s < points.Count - 1; s++)
                {
                    JObject a = (JObject)points[s], b = (JObject)points[s + 1];
                    for (int k = 0; k <= 20; k++)
                    {
                        float t = k / 20f, u = 1 - t;
                        line.Add(project(u*u*u*A.Vector(a, "Position") + 3*u*u*t*A.Vector(a, "OutTangent") +
                            3*u*t*t*A.Vector(b, "InTangent") + t*t*t*A.Vector(b, "Position")));
                    }
                }
                Handles.DrawAAPolyLine(3, line.ToArray());
            }
            JArray boxes = A.Array(layout, "Boxes");
            for (int i = 0; i < boxes.Count; i++)
            {
                JObject box = (JObject)boxes[i]; Vector3[] corners = LoopLevelBoardLayout.Corners(box);
                Vector3[] outline = new[] { corners[0], corners[1], corners[5], corners[4] }.Select(project).ToArray();
                Handles.color = Color.gray; Handles.DrawAAConvexPolygon(outline);
                Handles.color = Color.white; Handles.DrawAAPolyLine(1, outline.Concat(new[] { outline[0] }).ToArray());
                Vector3 center = project(A.Vector(box, "Position")); GUI.Label(new Rect(center.x - 10, center.y - 10, 30, 20), i.ToString(), EditorStyles.whiteMiniLabel);
                JArray cubes = A.Array(box, "Cubes"); int type = A.Int(box, "BoxType");
                for (int c = 0; c < (type == 1 ? Math.Min(1, cubes.Count) : cubes.Count); c++)
                {
                    JObject cube = (JObject)cubes[type == 1 ? cubes.Count - 1 : c];
                    Vector3 local = type == 1 ? Vector3.zero : new Vector3(0, 0, (type == 2 ? -2.8f : -1.2f) + c*.8f);
                    Vector3 p = project(A.Vector(box, "Position") + Quaternion.Euler(A.Vector(box, "Rotation")) * local);
                    Rect cell = new Rect(p.x - scale*.3f, p.y - scale*.3f, scale*.6f, scale*.6f);
                    EditorGUI.DrawRect(cell, A.ColorFor(A.Int(cube, "Type")));
                    if (A.Int(cube, "IsSecret") > 0) GUI.Label(cell, "?", EditorStyles.whiteMiniLabel);
                }
            }
        }
        finally { Handles.color = oldColor; Handles.EndGUI(); }
    }

    private void DrawBoardTable(JObject layout)
    {
        EditorGUILayout.LabelField("Mọi hộp: từ dưới lên trên. ? khối ẩn | K chìa khóa nhóm", EditorStyles.miniLabel);
        if (_workspace && _canvasTool == 0)
            EditorGUILayout.LabelField("Kéo cube sang hộp khác: thả lên cube để đổi chỗ, thả vào + để chuyển. Kéo trong cùng hộp để đổi thứ tự.", EditorStyles.wordWrappedMiniLabel);
        JArray boxes = A.Array(layout, "Boxes");
        for (int b = 0; b < boxes.Count; b++)
        {
            JObject box = (JObject)boxes[b]; JArray cubes = A.Array(box, "Cubes");
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string boxLabel = b + ": " + BoxTypeLabel(A.Int(box, "BoxType"));
                if (_manualPinnedBoxes.Contains(b)) boxLabel = "📌 " + boxLabel;
                if (GUILayout.Toggle(_box == b, boxLabel, "Button", GUILayout.Width(90))) _box = b;
                int first = Math.Max(0, cubes.Count - 8);
                if (first > 0) GUILayout.Label("...", GUILayout.Width(18));
                int end = cubes.Count + (cubes.Count < A.CubeCapacity(box) && _cubeBrushMode == 0 && (!_workspace || _canvasTool == 0 || _canvasTool == 2) ? 1 : 0);
                for (int c = first; c < end; c++)
                {
                    bool occupied = c < cubes.Count; JObject cube = occupied ? (JObject)cubes[c] : null;
                    string label = occupied ? A.Int(cube, "Type").ToString() : "+";
                    if (occupied && A.Int(cube, "IsSecret") > 0) label += "?";
                    if (occupied && A.Int(cube, "GroupKeyId") > 0) label += "K" + A.Int(cube, "GroupKeyId");
                    if (occupied && A.Int(cube, "LinkedPairId") > 0) label += "🔗";
                    bool concealed = _workspace && _canvasPlayerView && occupied && A.Int(cube, "IsSecret") > 0;
                    if (concealed) label = "?";
                    Rect cell = GUILayoutUtility.GetRect(34, 28, GUILayout.MaxWidth(48)); Event e = Event.current;
                    int dragControl = GUIUtility.GetControlID("LoopCubeDrag".GetHashCode(), FocusType.Passive, cell);
                    if (GUI.enabled && _workspace && _canvasTool == 0 && occupied && e.type == EventType.MouseDown && e.button == 0 && cell.Contains(e.mousePosition))
                    { _box = b; _tableDragBox = b; _tableDragCube = c; _tableDragControl = dragControl; GUIUtility.hotControl = dragControl; e.Use(); }
                    if (GUI.enabled && _workspace && _canvasTool == 0 && _tableDragBox >= 0 && e.rawType == EventType.MouseUp && cell.Contains(e.mousePosition))
                    {
                        CompleteTableCubeDrop(layout, b, c);
                        e.Use(); return;
                    }
                    if (GUI.enabled && occupied && e.type == EventType.MouseDown && e.button == 1 && cell.Contains(e.mousePosition))
                    { A.RemoveCube(box, c); _box = b; e.Use(); return; }
                    Color previous = GUI.backgroundColor; GUI.backgroundColor = concealed ? Color.gray : occupied ? A.ColorFor(A.Int(cube, "Type")) : Color.white;
                    string tooltip = "Hộp " + b + ", khối " + c + (occupied && A.Int(cube, "LinkedPairId") > 0 ? " [Cube đôi #" + A.Int(cube, "LinkedPairId") + "]" : "");
                    bool click = GUI.Button(cell, new GUIContent(label, tooltip)); GUI.backgroundColor = previous;
                    if (click)
                    {
                        _box = b;
                        if (!_workspace || (_canvasTool >= 2 && _canvasTool <= 4)) A.ApplyCubeBrush(box, c, _brushColor, _cubeBrushMode, _keyBrush);
                        else if (_canvasTool == 5 && occupied) A.RemoveCube(box, c);
                        return;
                    }
                }
                GUILayout.FlexibleSpace();
                string rule = A.Int(box, "LockColor") > 0 ? "M" + A.Int(box, "LockColor") : "";
                if (A.Int(box, "NumLock") > 0) rule += " N" + A.Int(box, "NumLock");
                if (A.Int(box, "IsEnterOnly") > 0) rule += " Vào";
                if (A.Int(box, "LimitedColor") > 0) rule += " Màu " + A.Int(box, "LimitedColor");
                if (A.Int(box, "IsAdjacentLocked") > 0) rule += " Kề";
                if (LoopLinkedCubeAuthoring.HasLinkedCube(box)) rule += " 🔗";
                GUILayout.Label(rule, EditorStyles.miniLabel, GUILayout.MaxWidth(105));
            }
        }
        if (_tableDragBox >= 0 && Event.current.rawType == EventType.MouseUp)
        { _tableDragBox = _tableDragCube = -1; if (GUIUtility.hotControl == _tableDragControl) GUIUtility.hotControl = 0; }
    }

    private void DrawBoxMembership(JObject layout, JObject entry)
    {
        JArray indexes = A.Array(entry, "BoxIndexes"), boxes = A.Array(layout, "Boxes");
        for (int row = 0; row < boxes.Count; row += 4)
            using (new EditorGUILayout.HorizontalScope())
                for (int b = row; b < Math.Min(row + 4, boxes.Count); b++)
                {
                    bool previous = indexes.Any(i => i.Value<int>() == b);
                    bool selected = GUILayout.Toggle(previous, "Hộp " + b, "Button");
                    if (selected == previous) continue;
                    if (selected) indexes.Add(b);
                    else for (int i = indexes.Count - 1; i >= 0; i--) if (indexes[i].Value<int>() == b) indexes.RemoveAt(i);
                    entry["BoxIndexes"] = indexes;
                }
        _showRawMembership = EditorGUILayout.Foldout(_showRawMembership, "Nâng cao: chỉ số hộp", true);
        if (_showRawMembership) Indices(entry, "BoxIndexes", "Chỉ số hộp");
    }
}
#endif
