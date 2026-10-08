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

public sealed partial class LoopLevelEditorWindow : EditorWindow
{
    [SerializeField] private LoopLevelDocument _document;
    [SerializeField] private string _filePath, _savedJson, _diskText;
    [SerializeField] private string _rawText, _rawBase;
    [SerializeField] private int _tab, _box = -1, _path, _point = -1, _tabLayoutVersion;
    [SerializeField] private bool _scenePreview = true, _showSlots, _showBindings = true, _autoBind = true;
    [SerializeField] private float _snap = 0.25f;
    [SerializeField] private Vector2 _scroll;
    [SerializeField] private int _boxTool, _brushColor = 1, _placementType;
    [SerializeField] private int _cubeBrushMode, _keyBrush;
    [SerializeField] private float _placementHeight, _placementYaw;
    [SerializeField] private bool _showCubeDetails;
    [SerializeField] private int _roadPreviewMode;
    [SerializeField] private bool _previewAllPaths;
    [SerializeField] private Vector2 _previewOrbit = new Vector2(0, 90);
    [SerializeField] private float _previewZoom = 1f;
    [SerializeField] private int _roadPreset;
    [SerializeField] private Vector3 _roadCenter = new Vector3(0, .2f, 0);
    [SerializeField] private float _roadWidth = 8, _roadDepth = 8, _cornerRadius = 1, _roadSmoothing = .6f;
    [SerializeField] private bool _sketchClosed = true, _sketchSmooth = true, _autoSmoothPoints, _showTangents, _showRoadPresets;
    private bool _drawingRoad, _sketchCursorValid;
    private Vector3 _sketchCursor;
    private readonly List<Vector3> _sketchPoints = new List<Vector3>();
    private LoopLevelRoadPreview _sketchPreview;
    private LoopLevelRoadPreview _roadPreview;
    private JObject _root;
    private string _cachedJson, _status;
    private List<Vector3> _slots = new List<Vector3>();
    private readonly List<Vector3[]> _roadLines = new List<Vector3[]>();
    private List<string> _errors = new List<string>(), _warnings = new List<string>();
    private bool _hasValidation;
    private int _dragGroup = -1;
    private static readonly string[] Tabs = { "Màn chơi", "Hộp", "Đường", "Nhóm / Nắp", "JSON" };

    [MenuItem("Tools/LoopMaster/Trình sửa màn chơi")]
    public static void Open() => GetWindow<LoopLevelEditorWindow>("Trình sửa màn chơi").Show();

    [MenuItem("Assets/LoopMaster/Sửa màn chơi JSON", true)]
    private static bool CanOpenSelected() => Selection.activeObject is TextAsset &&
        AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    [MenuItem("Assets/LoopMaster/Sửa màn chơi JSON")]
    private static void OpenSelected()
    {
        var window = GetWindow<LoopLevelEditorWindow>("Trình sửa màn chơi"); window.Show();
        window.TryOpen(AssetDatabase.GetAssetPath(Selection.activeObject));
    }

    private void OnEnable()
    {
        minSize = new Vector2(470, 520);
        if (_tabLayoutVersion == 0)
        {
            // Restore old Groups/JSON selections after removing the Map tab.
            _tab = _tab == 3 ? 0 : _tab > 3 ? _tab - 1 : _tab;
            _tabLayoutVersion = 1;
        }
        _tab = Mathf.Clamp(_tab, 0, Tabs.Length - 1);
        if (_document == null)
        {
            _document = CreateInstance<LoopLevelDocument>();
            _document.hideFlags = HideFlags.HideAndDontSave;
            _document.Json = A.NewLevel(NextLevelNumber()).ToString(Formatting.Indented);
            _savedJson = _document.Json;
        }
        Undo.undoRedoPerformed += OnUndoRedo;
        SceneView.duringSceneGui += DuringSceneGUI;
        EditorApplication.update += UpdateGeneration;
        RestoreGeneratedResult();
        SyncDirty();
    }
    private void OnDisable()
    {
        CaptureGeneratedResult();
        CancelRoadSketch();
        StopGeneration();
        _generatorPreview?.Dispose(); _generatorPreview = null; _generatedPreviewJson = null;
        _templates = null; _templatePreviewRoot = null;
        Undo.undoRedoPerformed -= OnUndoRedo;
        SceneView.duringSceneGui -= DuringSceneGUI;
        EditorApplication.update -= UpdateGeneration;
        _roadPreview?.Dispose(); _roadPreview = null;
        _cachedJson = null; _root = null; _roadLines.Clear();
        SceneView.RepaintAll();
    }
    private void OnDestroy()
    {
        if (_document != null) DestroyImmediate(_document);
    }
    private void OnUndoRedo()
    {
        CancelRoadSketch();
        _cachedJson = null; _root = null; _rawText = _rawBase = null;
        _hasValidation = false; SyncDirty(); Repaint(); SceneView.RepaintAll();
    }
    private static int NextLevelNumber()
    {
        int highest = 0;
        if (Directory.Exists(A.LevelFolder))
            foreach (string file in Directory.GetFiles(A.LevelFolder, "Level *.json"))
                if (int.TryParse(Path.GetFileNameWithoutExtension(file).Substring(6), out int number)) highest = Math.Max(highest, number);
        return highest + 1;
    }
    private bool HasRawChanges => _rawText != null && _rawText != _rawBase;
    private void SyncDirty()
    {
        hasUnsavedChanges = _document != null && (_document.Json != _savedJson || HasRawChanges);
        saveChangesMessage = "Lưu bản nháp trước khi đóng trình sửa màn chơi?";
        titleContent = new GUIContent("Trình sửa màn chơi");
    }
    private JObject Root()
    {
        if (_root != null && _cachedJson == _document.Json) return _root;
        _root = A.Parse(_document.Json); _cachedJson = _document.Json;
        _roadLines.Clear(); _slots.Clear();
        try
        {
            Puzzle puzzle = ExportedPuzzleJsonDecoder.Decode(_document.Json);
            if (_roadPreview == null) _roadPreview = new LoopLevelRoadPreview();
            _roadPreview.Update(A.Array(A.Layout(_root), "Paths").ToString(Formatting.None), puzzle);
            _roadLines.AddRange(_roadPreview.Lines);
            _slots = A.Slots(_root);
        }
        catch (Exception exception)
        {
            _roadPreview?.Dispose(); _roadPreview = null; _roadLines.Clear(); _slots.Clear();
            _status = "Xem trước: " + exception.Message;
        }
        return _root;
    }
    private void Commit(JObject root, string action = "Sửa màn chơi")
    {
        // A stored plan describes the generated layout. Manual geometry / puzzle edits invalidate it.
        if (root["editor_generator"] is JObject metadata && metadata.Value<bool?>("verified") == true)
        {
            JObject before = A.Parse(_document.Json);
            if (JToken.DeepEquals(before["editor_generator"], metadata) && !JToken.DeepEquals(A.Layout(before), A.Layout(root)))
            { metadata["verified"] = false; metadata.Remove("solution_clicks"); }
        }
        string json = root.ToString(Formatting.Indented);
        if (json == _document.Json) return;
        Undo.RecordObject(_document, action); _document.Json = json;
        EditorUtility.SetDirty(_document); _cachedJson = null; _root = null;
        _hasValidation = false; SyncDirty(); Repaint(); SceneView.RepaintAll();
    }
    private void SetDocument(JObject root, string path, string diskText)
    {
        CancelRoadSketch();
        Undo.ClearUndo(_document);
        _document.Json = root.ToString(Formatting.Indented); _savedJson = _document.Json;
        _filePath = path; _diskText = diskText; _root = null; _cachedJson = null;
        _box = _point = -1; _path = 0; _rawText = _rawBase = null;
        _hasValidation = false; _status = string.IsNullOrEmpty(path) ? "Bản nháp màn chơi mới." : "Đã mở " + Path.GetFileName(path);
        SyncDirty(); Repaint(); SceneView.RepaintAll();
    }
    private bool ConfirmReplace()
    {
        SyncDirty(); if (!hasUnsavedChanges) return true;
        int choice = EditorUtility.DisplayDialogComplex("Màn chơi chưa lưu", "Lưu bản nháp này trước khi mở màn chơi khác?", "Lưu", "Hủy", "Bỏ thay đổi");
        return choice == 2 || (choice == 0 && TrySave(false));
    }
    public bool TryOpen(string path)
    {
        try
        {
            string disk = File.ReadAllText(path); JObject root = A.Parse(disk);
            // Check shape before handing data to the inspector. Do not reject drafts with semantic errors.
            ExportedPuzzleJsonDecoder.Decode(disk);
            foreach (string key in new[] { "Boxes", "Paths" })
                if (!(A.Layout(root)[key] is JArray)) throw new FormatException("layout." + key + " phải là mảng.");
            if (!ConfirmReplace()) return false;
            SetDocument(root, Path.GetFullPath(path), disk); return true;
        }
        catch (Exception exception) { _status = "Không thể mở màn chơi: " + exception.Message; Repaint(); return false; }
    }
    private bool ApplyRaw()
    {
        if (!HasRawChanges) return true;
        try
        {
            JObject root = A.Parse(_rawText);
            var errors = A.Validate(root, out _);
            if (errors.Count > 0) throw new FormatException(string.Join("\n", errors));
            Commit(root, "Áp dụng JSON màn chơi"); _rawText = _rawBase = _document.Json;
            _status = "Đã áp dụng JSON."; SyncDirty(); return true;
        }
        catch (Exception exception) { _status = "Chưa áp dụng JSON: " + exception.Message; return false; }
    }
    public bool TrySave(bool saveAs)
    {
        if (_drawingRoad) { _status = "Hoàn tất hoặc hủy đường đang vẽ trước khi lưu."; Repaint(); return false; }
        try
        {
            if (!ApplyRaw()) return false;
            JObject root = Root(); _errors = A.Validate(root, out _warnings); _hasValidation = true;
            if (_errors.Count > 0) { _status = "Sửa các lỗi kiểm tra dữ liệu trước khi lưu."; Repaint(); return false; }
            string path = _filePath;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                string folder = string.IsNullOrEmpty(path) ? Path.GetFullPath(A.LevelFolder) : Path.GetDirectoryName(path);
                path = EditorUtility.SaveFilePanel("Lưu JSON màn chơi", folder, "Level " + A.Int(root, "level_number"), "json");
                if (string.IsNullOrEmpty(path)) return false;
            }
            path = Path.GetFullPath(path);
            bool inLevelFolder = string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(A.LevelFolder), StringComparison.OrdinalIgnoreCase);
            if (inLevelFolder && !string.Equals(Path.GetFileName(path), "Level " + A.Int(root, "level_number") + ".json", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Dùng tên file Level " + A.Int(root, "level_number") + ".json trong thư mục màn chơi. Chọn Lưu thành sau khi đổi số màn.");
            if (File.Exists(path))
            {
                bool same = !string.IsNullOrEmpty(_filePath) && string.Equals(path, Path.GetFullPath(_filePath), StringComparison.OrdinalIgnoreCase);
                bool changedOnDisk = same && File.ReadAllText(path) != _diskText;
                if ((changedOnDisk || !same) && !EditorUtility.DisplayDialog("Ghi đè JSON màn chơi?",
                    changedOnDisk ? "File đã được sửa bên ngoài cửa sổ này. Ghi đè bằng bản nháp của bạn?" : "Đã có màn chơi tại đường dẫn này. Ghi đè?", "Ghi đè", "Hủy")) return false;
            }
            A.WriteFile(path, _document.Json);
            _filePath = path; _savedJson = _document.Json; _diskText = File.ReadAllText(path);
            SyncDirty();
            string project = Path.GetFullPath(".") + Path.DirectorySeparatorChar;
            if (path.StartsWith(project, StringComparison.OrdinalIgnoreCase))
            {
                string relative = path.Substring(project.Length).Replace('\\', '/');
                AssetDatabase.ImportAsset(relative, ImportAssetOptions.ForceSynchronousImport);
                if (inLevelFolder)
                {
                    try { BuiltinLevelCatalogBuild.RefreshCatalog(); }
                    catch (Exception exception) { _status = "Đã lưu JSON nhưng không cập nhật được danh mục: " + exception.Message; return true; }
                }
            }
            _status = "Đã lưu " + Path.GetFileName(path); return true;
        }
        catch (Exception exception) { _status = "Lưu thất bại: " + exception.Message; Repaint(); return false; }
    }
    public override void SaveChanges()
    {
        if (!TrySave(false)) throw new OperationCanceledException("Chưa lưu bản nháp. Sửa lỗi hoặc dùng Lưu thành.");
        base.SaveChanges();
    }
    public override void DiscardChanges()
    {
        if (_document != null && _savedJson != null) _document.Json = _savedJson;
        _cachedJson = null; _root = null; _rawText = _rawBase = null;
        base.DiscardChanges(); Repaint(); SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        if (_document == null) return;
        HandleShortcuts();
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Tạo mới", EditorStyles.toolbarButton) && ConfirmReplace()) SetDocument(A.NewLevel(NextLevelNumber()), null, null);
            if (GUILayout.Button("Mở…", EditorStyles.toolbarButton))
            {
                string file = EditorUtility.OpenFilePanel("Mở JSON màn chơi", A.LevelFolder, "json");
                if (!string.IsNullOrEmpty(file)) TryOpen(file);
            }
            if (GUILayout.Button("Mẫu", EditorStyles.toolbarButton)) { _editorMode = 2; _scroll = Vector2.zero; }
            if (GUILayout.Button("Lưu", EditorStyles.toolbarButton)) TrySave(false);
            if (GUILayout.Button("Lưu thành…", EditorStyles.toolbarButton)) TrySave(true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Hoàn tác", EditorStyles.toolbarButton)) Undo.PerformUndo();
            if (GUILayout.Button("Làm lại", EditorStyles.toolbarButton)) Undo.PerformRedo();
        }
        EditorGUILayout.LabelField(string.IsNullOrEmpty(_filePath) ? "Chưa có tên — lưu thành Level N.json" : _filePath, EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            _scenePreview = EditorGUILayout.ToggleLeft("Xem ở Scene", _scenePreview, GUILayout.Width(110));
            _showSlots = EditorGUILayout.ToggleLeft("Số ô đường", _showSlots, GUILayout.Width(100));
            _showBindings = EditorGUILayout.ToggleLeft("Liên kết hộp", _showBindings, GUILayout.Width(105));
            if (GUILayout.Button("Nhìn từ trên", GUILayout.Width(95))) Frame();
        }
        _editorMode = GUILayout.Toolbar(_editorMode, new[] { "Chỉnh thủ công", "Tạo tự động", "Mẫu màn chơi" });
        if (_editorMode == 0)
        {
            using (new EditorGUI.DisabledScope(HasRawChanges || _drawingRoad || LoopLevelPlaytest.Busy || EditorApplication.isCompiling))
                if (GUILayout.Button("▶ Chơi thử bản nháp trong Game View")) PlayInGameView(Root());
            _tab = GUILayout.Toolbar(_tab, Tabs);
        }
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        try
        {
            JObject root = Root(), layout = A.Layout(root);
            if (_editorMode == 1) DrawAutoGenerator();
            else if (_editorMode == 2) DrawTemplates();
            else if (_tab == 4) DrawJson();
            else if (HasRawChanges)
                EditorGUILayout.HelpBox("JSON có thay đổi chưa áp dụng. Áp dụng hoặc đặt lại ở tab JSON trước khi sửa bằng giao diện.", MessageType.Warning);
            else
            {
                EditorGUI.BeginChangeCheck();
                switch (_tab)
                {
                    case 0: DrawLevel(root, layout); break;
                    case 1: DrawBoxes(root, layout); break;
                    case 2: DrawPaths(layout); break;
                    case 3: DrawGroups(layout); break;
                }
                EditorGUI.EndChangeCheck(); // Compare JSON so list-button changes are recorded as well.
                Commit(root);
            }
        }
        catch (ExitGUIException) { throw; }
        catch (Exception exception) { EditorGUILayout.HelpBox("Không thể hiển thị bản nháp: " + exception.Message + "\nDùng tab JSON để sửa hoặc mở lại file.", MessageType.Error); }
        EditorGUILayout.Space();
        if (_hasValidation)
        {
            foreach (string error in _errors.Take(20)) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (string warning in _warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }
        if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
        if (!string.IsNullOrEmpty(LoopLevelPlaytest.Status)) EditorGUILayout.HelpBox(LoopLevelPlaytest.Status, MessageType.Info);
        EditorGUILayout.EndScrollView(); SyncDirty();
        if (GUI.changed) { Repaint(); SceneView.RepaintAll(); }
    }
    private void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown || !(e.control || e.command) || e.keyCode != KeyCode.S) return;
        TrySave(e.shift); e.Use();
    }
    private void PlayInGameView(JObject level)
    {
        _status = LoopLevelPlaytest.Play(level, out string error) ? null : error;
        Repaint();
    }
    private void DrawLevel(JObject root, JObject layout)
    {
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || HasRawChanges || _drawingRoad))
            if (GUILayout.Button("Tạo màn chơi mới từ bản nháp này"))
            { BeginTemplateDraft(A.FromTemplate(root, NextLevelNumber())); GUIUtility.ExitGUI(); }
        Header("Thiết lập màn chơi");
        Integer(root, "level_number", "Số màn chơi"); Integer(root, "puzzle_id", "ID màn chơi");
        Integer(layout, "Version", "Phiên bản dữ liệu", Puzzle.LegacyVersion);
        int difficulty = EditorGUILayout.Popup("Độ khó", A.Int(layout, "HardLevel"), new[] { "Thường", "Khó", "Rất khó" });
        if (difficulty != A.Int(layout, "HardLevel")) layout["HardLevel"] = difficulty;
        Number(layout, "Size", "Kích thước bàn chơi", 10f); Integer(layout, "PathLength", "Sức chứa đường (nhóm)");
        Number(layout, "PathSectionDistance", "Khoảng cách ô trên đường", 0.2f);
        EditorGUILayout.HelpBox("PathLength là sức chứa đường theo nhóm. ID màu và chỉ số hộp theo định dạng JSON của game.", MessageType.None);
        EditorGUILayout.LabelField("Nội dung", A.Array(layout, "Boxes").Count + " hộp · " + A.Array(layout, "Paths").Count + " đường · " + _slots.Count + " ô trên đường");
        if (GUILayout.Button("Kiểm tra bản nháp")) { _errors = A.Validate(root, out _warnings); _hasValidation = true; _status = _errors.Count == 0 ? "Cấu trúc dữ liệu hợp lệ." : _errors.Count + " lỗi."; }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || _drawingRoad || A.Array(layout, "Boxes").Count == 0))
            if (GUILayout.Button("Xếp hộp và chỉnh đường bao quanh"))
            {
                LoopLevelBoardLayout.Arrange(layout, _generatorSettings ?? new LoopLevelGeneratorSettings(), A.Int(root, "puzzle_id"));
                _status = "Đã xếp hộp, chỉnh đường và liên kết. Giữ nguyên các khối và chỉ số hộp. Hoàn tác để khôi phục bàn chơi cũ.";
            }
        if (GUILayout.Button("Liên kết mọi hộp với ô đường gần nhất"))
            foreach (JObject box in A.Array(layout, "Boxes")) box["PathIndexV2"] = A.NearestSlot(box, _slots);
        EditorGUILayout.Space(); Header("Chơi thử");
        EditorGUILayout.HelpBox("Chơi thử trong Game View dùng bản nháp chưa lưu. Dừng Play Mode để quay lại chỉnh sửa. Thắng màn vẫn ghi tiến trình game; chọn số màn phù hợp để thử.", MessageType.None);
    }
    private void DrawBoxes(JObject root, JObject layout)
    {
        DrawBoxTools(layout);
        JArray boxes = A.Array(layout, "Boxes"); Header("Hộp — chọn trong bảng hoặc bấm hộp ở Scene View");
        DrawBoardTable(layout);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Thêm hộp")) { boxes.Add(A.NewBox(Vector3.zero)); _box = boxes.Count - 1; }
            using (new EditorGUI.DisabledScope(_box < 0 || _box >= boxes.Count))
            {
                if (GUILayout.Button("Nhân bản")) { var copy = (JObject)boxes[_box].DeepClone(); copy["Position"] = A.Vector(A.Vector(copy, "Position") + Vector3.right * 2); copy["PathIndexV2"] = 0; boxes.Add(copy); _box = boxes.Count - 1; }
                if (GUILayout.Button("Xóa")) { A.RemoveBox(root, _box); _box = Math.Min(_box, boxes.Count - 1); }
            }
        }
        if (_box < 0 || _box >= boxes.Count) return;
        JObject box = (JObject)boxes[_box]; Header("Hộp " + _box);
        Header("Khóa / quy tắc đặc biệt");
        Integer(box, "LockColor", "Khóa màu"); Integer(box, "NumLock", "Khóa số");
        Integer(box, "LimitedColor", "Chỉ nhận màu"); Flag(box, "IsEnterOnly", "Chỉ nhận vào"); Flag(box, "IsAdjacentLocked", "Khóa liền kề");
        _showBoxDetails = EditorGUILayout.Foldout(_showBoxDetails, "Nâng cao: loại hộp, vị trí và liên kết", true);
        if (_showBoxDetails)
        {
            Vector3 previous = A.Vector(box, "Position"), previousRotation = A.Vector(box, "Rotation");
            int previousType = A.Int(box, "BoxType");
            int nextType = EditorGUILayout.Popup("Loại hộp", previousType, new[] { "Thường", "Đơn (hàng đợi)", "Dài" });
            if (nextType != previousType) box["BoxType"] = nextType;
            VectorField(box, "Position"); VectorField(box, "Rotation");
            _autoBind = EditorGUILayout.Toggle("Tự liên kết khi di chuyển hộp", _autoBind);
            _snap = Mathf.Max(0, EditorGUILayout.FloatField("Bám lưới trong Scene", _snap));
            if (_autoBind && (previous != A.Vector(box, "Position") || previousRotation != A.Vector(box, "Rotation"))) box["PathIndexV2"] = A.NearestSlot(box, _slots);
            Integer(box, "PathIndexV2", "Liên kết ô đường (từ 1)");
            EditorGUILayout.HelpBox("Giá trị 0 tự tìm ô gần nhất khi chạy game. Di chuyển đường không đổi liên kết đã gán; dùng Liên kết mọi hộp khi cần.", MessageType.None);
            Integer(box, "PathIndex", "Chỉ số đường kiểu cũ");
            Indices(box, "MultiColorLock", "ID màu khóa nhiều màu", true);
        }
        Header("Khối — từ dưới lên trên");
        DrawCubeStrip(box);
        _showCubeDetails = EditorGUILayout.Foldout(_showCubeDetails, "Nâng cao: ID màu, khối ẩn, chìa khóa và khóa", true);
        JArray cubes = A.Array(box, "Cubes"); int capacity = A.Int(box, "BoxType") == 2 ? 6 : A.Int(box, "BoxType") == 1 ? 64 : 4;
        if (A.Int(box, "BoxType") == 1) EditorGUILayout.HelpBox("Hộp đơn chứa hàng đợi, mỗi lần bấm thả một khối. Trình sửa giới hạn hàng đợi 64 khối.", MessageType.None);
        for (int i = 0; _showCubeDetails && i < cubes.Count; i++)
        {
            var cube = (JObject)cubes[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Khối " + i, GUILayout.Width(55));
                    cube["Type"] = EditorGUILayout.IntField("ID màu", A.Int(cube, "Type"));
                    EditorGUI.DrawRect(GUILayoutUtility.GetRect(20, 18, GUILayout.Width(20)), A.ColorFor(A.Int(cube, "Type")));
                    if (GUILayout.Button("↑", GUILayout.Width(25)) && i + 1 < cubes.Count) { A.SwapCubes(box, i, i + 1); break; }
                    if (GUILayout.Button("×", GUILayout.Width(25))) { A.RemoveCube(box, i); break; }
                }
                Flag(cube, "IsSecret", "Ẩn"); Flag(cube, "IsFixed", "Cố định");
                Integer(cube, "GroupKeyId", "ID chìa khóa nhóm"); Integer(cube, "NumLock", "Khóa số");
            }
        }
        using (new EditorGUI.DisabledScope(cubes.Count >= capacity))
            if (GUILayout.Button("Thêm khối (" + cubes.Count + " / " + capacity + ")")) A.PaintCube(box, cubes.Count, _brushColor);
        EditorGUILayout.HelpBox("Màu thường: 1–14. Màu hộp dài: 101–105. Màu bất ngờ: 201+; khai báo thay thế trong layout.SurpriseDatas ở tab JSON. Có thể sửa LinkedCubes tại đó.", MessageType.None);
    }

    private void DrawBoxTools(JObject layout)
    {
        Header("Công cụ sửa hộp nhanh");
        _boxTool = GUILayout.Toolbar(_boxTool, new[] { "Chọn / Di chuyển", "Đặt hộp", "Tô khối", "Xóa khối" });
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sửa ở Scene / Nhìn từ trên")) { _scenePreview = true; Frame(); SceneView.lastActiveSceneView?.Focus(); }
            _snap = Mathf.Max(0, EditorGUILayout.FloatField("Bám lưới", _snap));
        }
        if (_boxTool == 1)
        {
            _placementType = EditorGUILayout.Popup("Loại hộp mới", _placementType, new[] { "Thường", "Đơn", "Dài" });
            _placementHeight = EditorGUILayout.FloatField("Độ cao đặt hộp", _placementHeight);
            _placementYaw = EditorGUILayout.FloatField("Góc xoay Y", _placementYaw);
        }
        _cubeBrushMode = GUILayout.Toolbar(_cubeBrushMode, new[] { "Màu", "Ẩn / Hiện", "Chìa khóa nhóm" });
        if (_cubeBrushMode == 2)
        {
            int[] ids = new[] { 0 }.Concat(A.Array(layout, "Groups").OfType<JObject>().Select(g => A.Int(g, "Id")).Where(id => id > 0)).Distinct().ToArray();
            int selected = Math.Max(0, System.Array.IndexOf(ids, _keyBrush));
            _keyBrush = ids[EditorGUILayout.Popup("Chìa khóa cho nhóm", selected, ids.Select(id => id == 0 ? "Không có chìa khóa" : "Nhóm " + id).ToArray())];
            if (ids.Length == 1) EditorGUILayout.HelpBox("Tạo nhóm ở tab Nhóm / Nắp trước, sau đó chọn chìa khóa tại đây.", MessageType.Info);
        }
        Header("Màu để tô");
        JArray boxes = A.Array(layout, "Boxes");
        bool longBrush = _boxTool == 1 ? _placementType == 2 : _box >= 0 && _box < boxes.Count && A.Int((JObject)boxes[_box], "BoxType") == 2;
        if (longBrush) _brushColor = Mathf.Clamp(_brushColor, 1, 5);
        for (int row = 0; row < 2; row++)
            using (new EditorGUILayout.HorizontalScope())
                for (int column = 0; column < 7; column++)
                {
                    int color = row * 7 + column + 1;
                    Color previous = GUI.backgroundColor; GUI.backgroundColor = A.ColorFor(color);
                    using (new EditorGUI.DisabledScope(longBrush && color > 5))
                        if (GUILayout.Toggle(_brushColor == color, (longBrush ? 100 + color : color).ToString(), "Button", GUILayout.Height(28))) _brushColor = color;
                    GUI.backgroundColor = previous;
                }
        EditorGUILayout.HelpBox("Chọn Màu, Ẩn / Hiện hoặc Chìa khóa nhóm rồi bấm khối trong bảng hoặc chế độ Tô ở Scene. + thêm khối khi tô màu; chuột phải xóa. Đọc từ trái sang phải, theo thứ tự dưới lên trên. Alt điều khiển camera; Esc về chế độ chọn. Hộp dài dùng màu 101–105.", MessageType.None);
    }

    private void DrawCubeStrip(JObject box)
    {
        JArray cubes = A.Array(box, "Cubes");
        int count = cubes.Count + (cubes.Count < A.CubeCapacity(box) && _cubeBrushMode == 0 ? 1 : 0);
        for (int first = 0; first < count; first += 8)
            using (new EditorGUILayout.HorizontalScope())
                for (int i = first; i < Math.Min(first + 8, count); i++)
                {
                    bool occupied = i < cubes.Count;
                    string label = occupied ? A.Int((JObject)cubes[i], "Type").ToString() : "+";
                    if (occupied && A.Int((JObject)cubes[i], "IsSecret") > 0) label += " ?";
                    if (occupied && A.Int((JObject)cubes[i], "GroupKeyId") > 0) label += " K" + A.Int((JObject)cubes[i], "GroupKeyId");
                    Rect rect = GUILayoutUtility.GetRect(42, 34, GUILayout.MaxWidth(65));
                    Event e = Event.current;
                    bool erase = occupied && e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition);
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = occupied ? A.ColorFor(A.Int((JObject)cubes[i], "Type")) : Color.white;
                    bool click = GUI.Button(rect, new GUIContent(label, "Khối " + i + " (từ dưới lên trên). Bấm để áp dụng công cụ; chuột phải để xóa."));
                    GUI.backgroundColor = previous;
                    if (erase)
                    { A.RemoveCube(box, i); e.Use(); return; }
                    if (click) { A.ApplyCubeBrush(box, i, _brushColor, _cubeBrushMode, _keyBrush); return; }
                }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Lấp đầy bằng màu đã chọn")) A.FillBox(box, _brushColor);
            using (new EditorGUI.DisabledScope(cubes.Count == 0))
                if (GUILayout.Button("Xóa khối trên cùng")) A.RemoveCube(box, cubes.Count - 1);
        }
    }
    private void DrawPaths(JObject layout)
    {
        JArray paths = A.Array(layout, "Paths");
        DrawQuickRoadTools(paths);
        if (_drawingRoad) return;
        if (paths.Count == 0) return;
        _path = Mathf.Clamp(_path, 0, paths.Count - 1);
        _path = EditorGUILayout.Popup("Đường đang chọn", _path, Enumerable.Range(0, paths.Count).Select(i => "Đường " + i).ToArray());
        DrawPathInspectorPreview();
        JObject path = (JObject)paths[_path]; Flag(path, "IsCircle", "Vòng kín");
        _autoSmoothPoints = EditorGUILayout.Toggle(new GUIContent("Tự làm mượt khi di chuyển điểm", "Tính lại tiếp tuyến sau khi di chuyển, thêm hoặc xóa điểm."), _autoSmoothPoints);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Làm mượt đường")) A.SmoothRoad(path, _roadSmoothing);
            if (GUILayout.Button("Làm thẳng đường")) A.SmoothRoad(path, 0);
        }
        if (GUILayout.Button("Xóa đường đang chọn")) { paths.RemoveAt(_path); _point = -1; return; }
        JArray points = A.Array(path, "ControlPoints");
        bool repeatedEnd = points.Count > 1 && (A.Vector((JObject)points[0], "Position") - A.Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        for (int i = 0; i < points.Count; i++)
            if (GUILayout.Toggle(_point == i, "Điểm " + i + "    " + A.Vector((JObject)points[i], "Position").ToString("F2"), "Button")) _point = i;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(points.Count >= (A.Int(path, "IsCircle") > 0 && !repeatedEnd ? 127 : 128)))
                if (GUILayout.Button("Thêm điểm cuối"))
                {
                    int lastAnchor = points.Count - (repeatedEnd ? 2 : 1);
                    Vector3 position = lastAnchor < 0 ? Vector3.zero : A.Vector((JObject)points[lastAnchor], "Position") + Vector3.right * 2;
                    _point = points.Count - (repeatedEnd ? 1 : 0);
                    points.Insert(_point, A.Point(position, position, position));
                    if (_autoSmoothPoints) A.SmoothRoad(path, _roadSmoothing);
                }
            using (new EditorGUI.DisabledScope(_point < 0 || _point >= points.Count - 1 || points.Count >= 128))
                if (GUILayout.Button("Chia đoạn sau điểm này")) _point = A.SplitSegment(points, _point);
        }
        if (_point < 0 || _point >= points.Count) return;
        JObject point = (JObject)points[_point]; Header("Điểm " + _point + " — kéo trong Scene View");
        Vector3 positionBefore = A.Vector(point, "Position");
        Vector3 positionAfter = EditorGUILayout.Vector3Field("Vị trí", positionBefore);
        if (positionAfter != positionBefore)
        {
            A.MovePoint(points, _point, positionAfter);
            if (_autoSmoothPoints) A.SmoothRoad(path, _roadSmoothing);
        }
        _showTangents = EditorGUILayout.Foldout(_showTangents, "Nâng cao: sửa tiếp tuyến", true);
        if (_showTangents) { VectorField(point, "InTangent"); VectorField(point, "OutTangent"); }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (_showTangents)
            {
                if (GUILayout.Button("Tiếp tuyến thẳng")) point["InTangent"] = point["OutTangent"] = A.Vector(A.Vector(point, "Position"));
                if (GUILayout.Button("Đối xứng tiếp tuyến ra → vào")) point["InTangent"] = A.Vector(A.Vector(point, "Position") * 2 - A.Vector(point, "OutTangent"));
            }
            bool seam = points.Count > 1 && (A.Vector((JObject)points[0], "Position") - A.Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
            int anchors = points.Count - (seam ? 1 : 0);
            using (new EditorGUI.DisabledScope(anchors <= (A.Int(path, "IsCircle") > 0 || seam ? 3 : 2)))
                if (GUILayout.Button("Xóa điểm"))
                {
                    A.RemoveRoadPoint(path, _point); _point = Math.Min(_point, points.Count - 1);
                    if (_autoSmoothPoints) A.SmoothRoad(path, _roadSmoothing);
                }
        }
        EditorGUILayout.HelpBox(_showTangents ? "Tiếp tuyến dùng vị trí tuyệt đối và di chuyển cùng điểm. Chia đoạn thêm điểm giữa, giữ nguyên đường cong." :
            "Kéo các điểm để chỉnh hình đường. Bật tự làm mượt để giữ các khúc cong mềm. Mở Nâng cao để chỉnh tiếp tuyến riêng.", MessageType.None);
    }
    private void DrawQuickRoadTools(JArray paths)
    {
        Header("Dựng đường — bấm đặt điểm hoặc dùng hình mẫu");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _sketchClosed = EditorGUILayout.Toggle("Vòng kín", _sketchClosed);
            _sketchSmooth = EditorGUILayout.Toggle("Bo cong các góc", _sketchSmooth);
            if (_sketchSmooth) _roadSmoothing = EditorGUILayout.Slider("Độ mượt", _roadSmoothing, 0, 1);
            _roadCenter.y = EditorGUILayout.FloatField("Độ cao đường (Y)", _roadCenter.y);
            _snap = Mathf.Max(0, EditorGUILayout.FloatField("Bám lưới (0 = tắt)", _snap));
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                if (!_drawingRoad && GUILayout.Button("Vẽ đường trong Scene View")) StartRoadSketch();
            if (_drawingRoad)
            {
                EditorGUILayout.HelpBox("Bấm để đặt điểm. Enter / Hoàn tất để tạo đường; Backspace xóa điểm cuối; Esc hủy. Vòng kín cần 3 điểm, đường mở cần 2 điểm.", MessageType.Info);
                EditorGUILayout.LabelField("Số điểm đã đặt", _sketchPoints.Count.ToString());
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!CanFinishSketch))
                        if (GUILayout.Button("Hoàn tất đường")) FinishRoadSketch();
                    if (GUILayout.Button("Bỏ điểm cuối")) RemoveSketchPoint();
                    if (GUILayout.Button("Hủy")) CancelRoadSketch();
                }
                UpdateSketchPreview(false);
                Rect rect = GUILayoutUtility.GetRect(100, 200, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint && _sketchPoints.Count >= 2)
                    _sketchPreview?.DrawInspector(rect, 0, true, _roadPreviewMode, new Vector2(0, 90), 1, -1);
            }
            _showRoadPresets = EditorGUILayout.Foldout(_showRoadPresets, "Hoặc tạo đường từ hình mẫu", true);
            if (_showRoadPresets)
            {
                _roadPreset = EditorGUILayout.Popup("Hình mẫu", _roadPreset, new[] { "Chữ nhật bo góc", "Bầu dục / tròn" });
                _roadCenter = EditorGUILayout.Vector3Field("Tâm", _roadCenter);
                _roadWidth = Mathf.Max(.5f, EditorGUILayout.FloatField("Chiều rộng (X)", _roadWidth));
                _roadDepth = Mathf.Max(.5f, EditorGUILayout.FloatField("Chiều sâu (Z)", _roadDepth));
                if (_roadPreset == 0) _cornerRadius = EditorGUILayout.Slider("Bán kính góc", _cornerRadius, 0, Mathf.Min(_roadWidth, _roadDepth) * .5f);
                using (new EditorGUI.DisabledScope(_drawingRoad))
                    if (GUILayout.Button("Tạo đường theo hình mẫu"))
                    {
                        paths.Add(_roadPreset == 0 ? A.RoundedRectangleRoad(_roadCenter, _roadWidth, _roadDepth, _cornerRadius) : A.OvalRoad(_roadCenter, _roadWidth, _roadDepth));
                        _path = paths.Count - 1; _point = 0; _scenePreview = true;
                    }
            }
        }
    }

    private bool CanFinishSketch => _sketchPoints.Count >= (_sketchClosed ? 3 : 2);
    private void StartRoadSketch()
    {
        CancelRoadSketch(); _drawingRoad = true; _scenePreview = true; _tab = 2;
        Frame();
        var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>(); view.wantsMouseMove = true; view.Focus();
        _status = "Đang vẽ đường: bấm trong Scene View, nhấn Enter để hoàn tất.";
        Repaint(); SceneView.RepaintAll();
    }
    private void CancelRoadSketch()
    {
        _drawingRoad = false; _sketchCursorValid = false; _sketchPoints.Clear();
        _sketchPreview?.Dispose(); _sketchPreview = null;
        Repaint(); SceneView.RepaintAll();
    }
    private void AddSketchPoint(Vector3 position)
    {
        if (_sketchPoints.Count >= (_sketchClosed ? 127 : 128)) { _status = "Đã đạt giới hạn số điểm; hãy hoàn tất đường."; return; }
        if (_sketchPoints.Count > 0 && (_sketchPoints[_sketchPoints.Count - 1] - position).sqrMagnitude < .000001f) return;
        if (_sketchClosed && _sketchPoints.Count > 0 && (_sketchPoints[0] - position).sqrMagnitude < .000001f) return;
        _sketchPoints.Add(position); Repaint(); SceneView.RepaintAll();
    }
    private void RemoveSketchPoint()
    {
        if (_sketchPoints.Count > 0) _sketchPoints.RemoveAt(_sketchPoints.Count - 1);
        _sketchCursorValid = false; Repaint(); SceneView.RepaintAll();
    }
    private void FinishRoadSketch()
    {
        if (!CanFinishSketch) { _status = "Đặt thêm điểm trước khi hoàn tất."; return; }
        JObject path = A.RoadFromPositions(_sketchPoints, _sketchClosed, _sketchSmooth ? _roadSmoothing : 0);
        JObject root = Root(); JArray paths = A.Array(A.Layout(root), "Paths");
        paths.Add(path); _path = paths.Count - 1; _point = 0;
        // One finished road is one Undo step; cancelled sketches never change the document.
        Undo.IncrementCurrentGroup(); Commit(root, "Draw road"); Undo.IncrementCurrentGroup();
        _autoSmoothPoints = _sketchSmooth;
        CancelRoadSketch(); _status = "Đã tạo đường. Kéo các điểm để chỉnh hình dạng.";
    }
    private void UpdateSketchPreview(bool includeCursor)
    {
        var positions = new List<Vector3>(_sketchPoints);
        bool atStart = positions.Count > 0 && (positions[0] - _sketchCursor).sqrMagnitude < .000001f;
        if (includeCursor && _sketchCursorValid && positions.Count > 0 && positions.Count < (_sketchClosed ? 127 : 128) &&
            (positions[positions.Count - 1] - _sketchCursor).sqrMagnitude > .000001f && !(_sketchClosed && atStart)) positions.Add(_sketchCursor);
        if (positions.Count < 2) { _sketchPreview?.Dispose(); _sketchPreview = null; return; }
        // Preview stays open until a loop has enough anchors, avoiding invalid partial paths.
        JObject path = A.RoadFromPositions(positions, _sketchClosed && positions.Count >= 3, _sketchSmooth ? _roadSmoothing : 0);
        string json = new JObject { ["layout"] = new JObject { ["Paths"] = new JArray(path) } }.ToString(Formatting.None);
        if (_sketchPreview == null) _sketchPreview = new LoopLevelRoadPreview();
        _sketchPreview.Update(json, ExportedPuzzleJsonDecoder.Decode(json));
    }

    private void DrawRoadSketch(SceneView view)
    {
        Event e = Event.current;
        Rect panel = new Rect(12, 12, Mathf.Min(560, view.position.width - 24), 110);
        int control = GUIUtility.GetControlID(FocusType.Passive);
        bool canvas = !panel.Contains(e.mousePosition);
        if (e.type == EventType.Layout && !e.alt) HandleUtility.AddDefaultControl(control);
        if (canvas && !e.alt && (e.type == EventType.MouseMove || e.type == EventType.MouseDown || e.type == EventType.Repaint))
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            _sketchCursorValid = new Plane(Vector3.up, new Vector3(0, _roadCenter.y, 0)).Raycast(ray, out float distance);
            if (_sketchCursorValid) _sketchCursor = Snap(ray.GetPoint(distance));
        }
        if (e.type == EventType.MouseMove) view.Repaint();
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && canvas && _sketchCursorValid)
        {
            bool close = _sketchClosed && CanFinishSketch && Vector2.Distance(e.mousePosition, HandleUtility.WorldToGUIPoint(_sketchPoints[0])) < 12;
            if (close) FinishRoadSketch(); else AddSketchPoint(_sketchCursor);
            e.Use();
        }
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Escape) { CancelRoadSketch(); e.Use(); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { FinishRoadSketch(); e.Use(); }
            else if (e.keyCode == KeyCode.Backspace) { RemoveSketchPoint(); e.Use(); }
        }
        if (!_drawingRoad) return;
        UpdateSketchPreview(canvas);
        if (e.type == EventType.Repaint && _roadPreviewMode != 2) _sketchPreview?.DrawSceneMeshes();
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        if (_sketchPreview != null && _roadPreviewMode != 1)
            foreach (Vector3[] line in _sketchPreview.Lines) if (line.Length > 1) { Handles.color = Color.cyan; Handles.DrawAAPolyLine(3, line); }
        for (int i = 0; i < _sketchPoints.Count; i++)
        {
            Handles.color = i == 0 && _sketchClosed ? Color.green : Color.yellow;
            Handles.SphereHandleCap(0, _sketchPoints[i], Quaternion.identity, .18f, EventType.Repaint);
            Handles.Label(_sketchPoints[i] + Vector3.up * .25f, i == 0 && _sketchClosed ? "Điểm đầu / bấm để khép kín" : i.ToString());
        }
        if (_sketchCursorValid && canvas)
        {
            Handles.color = Color.white; Handles.DrawWireDisc(_sketchCursor, Vector3.up, .18f);
            Handles.Label(_sketchCursor + Vector3.up * .3f, "Bấm để đặt điểm");
        }
        Handles.BeginGUI();
        GUILayout.BeginArea(panel, EditorStyles.helpBox);
        GUILayout.Label("VẼ ĐƯỜNG — " + _sketchPoints.Count + " điểm", EditorStyles.boldLabel);
        GUILayout.Label("Bấm để đặt • Backspace xóa • Enter hoàn tất • Esc hủy");
        GUILayout.Label(_sketchClosed ? "Bấm điểm đầu màu xanh để khép kín vòng." : "Đường mở: đặt ít nhất hai điểm.");
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!CanFinishSketch)) if (GUILayout.Button("Hoàn tất đường")) FinishRoadSketch();
            if (GUILayout.Button("Bỏ điểm cuối")) RemoveSketchPoint();
            if (GUILayout.Button("Hủy")) CancelRoadSketch();
        }
        GUILayout.EndArea(); Handles.EndGUI();
    }
    private void DrawPathInspectorPreview()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _roadPreviewMode = EditorGUILayout.Popup("Xem trước", _roadPreviewMode, new[] { "Mặt đường + tuyến", "Mặt đường", "Tuyến đường" });
            using (new EditorGUILayout.HorizontalScope())
            {
                _previewAllPaths = GUILayout.Toggle(_previewAllPaths, "Mọi đường", GUILayout.Width(80));
                if (GUILayout.Button("Từ trên")) { _previewOrbit = new Vector2(0, 90); _previewZoom = 1f; }
                if (GUILayout.Button("3D")) { _previewOrbit = new Vector2(35, 45); _previewZoom = 1f; }
                if (GUILayout.Button("Sửa ở Scene")) { _scenePreview = true; Frame(); }
            }
            Rect rect = GUILayoutUtility.GetRect(100, 240, GUILayout.ExpandWidth(true));
            int id = GUIUtility.GetControlID("RoadPreviewOrbit".GetHashCode(), FocusType.Passive, rect);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && e.button == 0)
            { GUIUtility.hotControl = id; e.Use(); }
            if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                _previewOrbit.x += e.delta.x * .5f;
                _previewOrbit.y = Mathf.Clamp(_previewOrbit.y - e.delta.y * .5f, 5, 90);
                e.Use(); Repaint();
            }
            if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
            if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
            { _previewZoom = Mathf.Clamp(_previewZoom * Mathf.Exp(e.delta.y * .06f), .35f, 3f); e.Use(); Repaint(); }
            if (e.type == EventType.Repaint)
                _roadPreview?.DrawInspector(rect, _path, _previewAllPaths, _roadPreviewMode, _previewOrbit, _previewZoom, _point);
            EditorGUILayout.LabelField("Kéo để xoay / cuộn để thu phóng. Sửa điểm và tiếp tuyến trong Scene View.", EditorStyles.miniLabel);
        }
    }
    private void DrawGroups(JObject layout)
    {
        Header("Nhóm — chỉ số hộp bắt đầu từ 0"); JArray groups = A.Array(layout, "Groups");
        for (int i = 0; i < groups.Count; i++)
        {
            JObject group = (JObject)groups[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Integer(group, "Id", "ID nhóm"); Integer(group, "LockedByColor", "Màu khóa");
                DrawBoxMembership(layout, group);
                if (GUILayout.Button("Xóa nhóm")) { groups.RemoveAt(i); break; }
            }
        }
        if (GUILayout.Button("Thêm nhóm")) { groups.Add(new JObject { ["Id"] = groups.OfType<JObject>().Select(g => A.Int(g, "Id")).DefaultIfEmpty().Max() + 1, ["BoxIndexes"] = new JArray(_box >= 0 ? _box : 0), ["LockedByColor"] = 0 }); layout["Groups"] = groups; }
        Header("Nắp che cuốn"); JArray covers = A.Array(layout, "Covers");
        for (int i = 0; i < covers.Count; i++)
        {
            JObject cover = (JObject)covers[i]; EditorGUILayout.LabelField("Nắp " + i); DrawBoxMembership(layout, cover);
            if (GUILayout.Button("Xóa nắp " + i)) { covers.RemoveAt(i); break; }
        }
        if (GUILayout.Button("Thêm nắp")) { covers.Add(new JObject { ["BoxIndexes"] = new JArray(_box >= 0 ? _box : 0) }); layout["Covers"] = covers; }
        EditorGUILayout.HelpBox("Xóa hộp tự cập nhật chỉ số trong nhóm/nắp. Các trường JSON khác và dữ liệu thay thế màu bất ngờ được giữ nguyên.", MessageType.None);
    }
    private void DrawJson()
    {
        if (_rawText == null || (!HasRawChanges && _rawBase != _document.Json)) _rawText = _rawBase = _document.Json;
        EditorGUILayout.HelpBox("Trường nâng cao: LinkedCubes, SurpriseDatas và dữ liệu tùy chỉnh. Áp dụng JSON kiểm tra dữ liệu và tạo một bước hoàn tác. Thay đổi chưa áp dụng vẫn được báo khi đóng.", MessageType.None);
        _rawText = EditorGUILayout.TextArea(_rawText, GUILayout.MinHeight(350), GUILayout.ExpandHeight(true));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Áp dụng JSON")) ApplyRaw();
            if (GUILayout.Button("Đặt lại theo bản nháp")) { _rawText = _rawBase = _document.Json; SyncDirty(); }
        }
    }
    private static void Header(string title) { EditorGUILayout.Space(); EditorGUILayout.LabelField(title, EditorStyles.boldLabel); }
    private static void Integer(JObject obj, string key, string label, int fallback = 0)
    {
        int previous = A.Int(obj, key, fallback), next = EditorGUILayout.IntField(label, previous);
        if (previous != next) obj[key] = next;
    }
    private static void Number(JObject obj, string key, string label, float fallback = 0f)
    {
        float previous = A.Float(obj, key, fallback), next = EditorGUILayout.FloatField(label, previous);
        if (previous != next) obj[key] = next;
    }
    private static void VectorField(JObject obj, string key)
    {
        string label = key == "Position" ? "Vị trí" : key == "Rotation" ? "Góc xoay" :
            key == "InTangent" ? "Tiếp tuyến vào" : key == "OutTangent" ? "Tiếp tuyến ra" : key;
        Vector3 previous = A.Vector(obj, key), next = EditorGUILayout.Vector3Field(label, previous);
        if (previous != next) obj[key] = A.Vector(next);
    }
    private static string BoxTypeLabel(int type) => type == 0 ? "Thường" : type == 1 ? "Đơn" : type == 2 ? "Dài" : "Không rõ (" + type + ")";
    private static void Flag(JObject obj, string key, string label)
    {
        bool previous = A.Int(obj, key) != 0;
        bool next = EditorGUILayout.Toggle(label, previous); if (previous != next) obj[key] = next ? 1 : 0;
    }
    private void Indices(JObject obj, string key, string label, bool nullable = false)
    {
        var array = obj[key] as JArray;
        string previous = array == null ? "" : string.Join(", ", array.Select(v => v.Value<int>()));
        string next = EditorGUILayout.DelayedTextField(label, previous);
        if (next == previous) return;
        try
        {
            int[] values = next.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            obj[key] = nullable && values.Length == 0 ? (JToken)JValue.CreateNull() : new JArray(values);
        }
        catch { _status = label + ": nhập ID số nguyên, ngăn cách bằng dấu phẩy."; }
    }

    private void Frame()
    {
        try
        {
            JObject layout = _editorMode == 1 && _generationJob?.Candidate != null ? A.Layout(_generationJob.Candidate.Json) : A.Layout(Root());
            var bounds = new Bounds(Vector3.zero, Vector3.one * 8);
            foreach (JObject box in A.Array(layout, "Boxes")) bounds.Encapsulate(A.Vector(box, "Position"));
            if (_editorMode == 1 && _generationJob?.Candidate != null) { EnsureGeneratedPreview(); bounds.Encapsulate(_generatorPreview.BoundsFor(0, true)); }
            else foreach (Vector3[] road in _roadLines) foreach (Vector3 point in road) bounds.Encapsulate(point);
            var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            view.LookAt(bounds.center, Quaternion.LookRotation(Vector3.down, Vector3.forward), Mathf.Max(bounds.size.x, bounds.size.z) * 0.65f + 2, true);
            view.Repaint();
        }
        catch (Exception exception) { _status = exception.Message; }
    }
    private Vector3 Snap(Vector3 value) => _snap <= 0 ? value : new Vector3(Mathf.Round(value.x / _snap) * _snap, value.y, Mathf.Round(value.z / _snap) * _snap);

    private void DuringSceneGUI(SceneView view)
    {
        if (!_scenePreview || _document == null || EditorApplication.isPlaying || HasRawChanges) return;
        try
        {
            if (_editorMode == 1) { DrawGeneratedScene(); return; }
            if (_editorMode == 2) return;
            JObject root = Root(), layout = A.Layout(root);
            Event e = Event.current;
            if (_drawingRoad && _tab == 2)
            {
                using (new Handles.DrawingScope(Matrix4x4.identity)) { DrawRoadPreview(); DrawRoadSketch(view); }
                return;
            }
            if (_tab == 1 && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            { _boxTool = 0; e.Use(); Repaint(); }
            if (_tab == 1) DrawSceneBoxToolbar();
            if (e.type == EventType.MouseDown && e.button == 0) { Undo.IncrementCurrentGroup(); _dragGroup = Undo.GetCurrentGroup(); }
            using (new Handles.DrawingScope(Matrix4x4.identity))
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                DrawRoadPreview(); DrawBoxPreview(layout, _tab != 1 || _boxTool == 0);
                // Control points sit inside the generated surface. Keep edit handles visible above it.
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                if (_showSlots)
                    for (int i = 0; i < _slots.Count; i += Math.Max(1, _slots.Count / 500))
                    {
                        Handles.color = Color.yellow; Handles.DotHandleCap(0, _slots[i], Quaternion.identity, 0.04f, EventType.Repaint);
                        Handles.Label(_slots[i] + Vector3.up * 0.1f, (i + 1).ToString());
                    }
                if (_tab == 2) DrawPointHandles(layout);
                else if (_tab == 1 && _boxTool == 0 && _box >= 0 && _box < A.Array(layout, "Boxes").Count)
                    TransformHandle((JObject)A.Array(layout, "Boxes")[_box]);
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                if (_tab == 1 && _boxTool == 1) PlaceSceneBox(view, root);
                else if (_tab == 1 && (_boxTool == 2 || _boxTool == 3)) DrawSceneCubeButtons(layout);
            }
            Commit(root, _tab == 1 && _boxTool != 0 ? "Edit boxes in Scene" : "Move level element");
            if (e.rawType == EventType.MouseUp && _dragGroup >= 0) { Undo.CollapseUndoOperations(_dragGroup); _dragGroup = -1; }
        }
        catch (Exception exception) { _status = "Sửa ở Scene: " + exception.Message; /* Keep invalid drafts repairable without logging on every repaint. */ }
    }

    private void DrawSceneBoxToolbar()
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(12, 12, 400, 60), EditorStyles.helpBox);
        int next = GUILayout.Toolbar(_boxTool, new[] { "Chọn", "Đặt", "Tô", "Xóa" });
        if (next != _boxTool) { _boxTool = next; Repaint(); }
        GUILayout.Label("Màu tô " + _brushColor + "  |  Alt: camera  |  Esc: chọn", EditorStyles.miniLabel);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private void PlaceSceneBox(SceneView view, JObject root)
    {
        Event e = Event.current;
        if (e.alt || new Rect(12, 12, 400, 60).Contains(e.mousePosition)) return;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!new Plane(Vector3.up, new Vector3(0, _placementHeight, 0)).Raycast(ray, out float distance)) return;
        Vector3 position = Snap(ray.GetPoint(distance));
        Bounds footprint = LoopLevelBoardLayout.LocalBounds(_placementType);
        Quaternion rotation = Quaternion.Euler(0, _placementYaw, 0);
        using (new Handles.DrawingScope(Matrix4x4.TRS(position, rotation, Vector3.one)))
        { Handles.color = Color.green; Handles.DrawWireCube(footprint.center, footprint.size); }
        Handles.Label(position + Vector3.up, "Bấm để đặt hộp " + BoxTypeLabel(_placementType));
        if (e.type == EventType.MouseDown && e.button == 0 && HandleUtility.nearestControl == control && GUIUtility.hotControl == 0)
        {
            JObject box = A.NewBox(position); box["BoxType"] = _placementType;
            box["Rotation"] = A.Vector(new Vector3(0, _placementYaw, 0));
            if (_autoBind) box["PathIndexV2"] = A.NearestSlot(box, _slots);
            JArray boxes = A.Array(A.Layout(root), "Boxes"); boxes.Add(box); A.Layout(root)["Boxes"] = boxes;
            _box = boxes.Count - 1; e.Use(); Repaint();
        }
        if (e.type == EventType.MouseMove) view.Repaint();
    }

    private void DrawSceneCubeButtons(JObject layout)
    {
        Event e = Event.current;
        if (e.alt || new Rect(12, 12, 400, 60).Contains(e.mousePosition)) return;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        float nearest = float.PositiveInfinity;
        int hitBox = -1, hitCube = -1;
        JArray boxes = A.Array(layout, "Boxes");
        for (int b = 0; b < boxes.Count; b++)
        {
            JObject box = (JObject)boxes[b]; int type = A.Int(box, "BoxType");
            JArray cubes = A.Array(box, "Cubes");
            int visible = type == 1 ? Math.Min(1, cubes.Count) : cubes.Count;
            Matrix4x4 matrix = Matrix4x4.TRS(A.Vector(box, "Position"), Quaternion.Euler(A.Vector(box, "Rotation")), Vector3.one);
            Ray localRay = new Ray(matrix.inverse.MultiplyPoint3x4(ray.origin), matrix.inverse.MultiplyVector(ray.direction));
            using (new Handles.DrawingScope(matrix))
            {
                for (int c = 0; c < visible; c++)
                {
                    int index = type == 1 ? cubes.Count - 1 : c;
                    Vector3 position = type == 1 ? new Vector3(0, 1.12f, 0) :
                        new Vector3(0, .8f, (type == 2 ? -2.8f : -1.2f) + c * .8f);
                    Handles.color = A.ColorFor(A.Int((JObject)cubes[index], "Type"));
                    if (e.type == EventType.Repaint) Handles.DrawWireCube(position, Vector3.one * .78f);
                    if (new Bounds(position, Vector3.one * .8f).IntersectRay(localRay, out float distance) && distance < nearest)
                    { nearest = distance; hitBox = b; hitCube = index; }
                }
                if (_boxTool != 2 || _cubeBrushMode != 0 || cubes.Count >= A.CubeCapacity(box)) continue;
                Vector3 append = type == 1 ? new Vector3(0, 1.7f, 0) :
                    new Vector3(0, .8f, (type == 2 ? -2.8f : -1.2f) + cubes.Count * .8f);
                Handles.color = Color.green; Handles.Label(append + Vector3.up * .4f, "+");
                if (e.type == EventType.Repaint) Handles.SphereHandleCap(0, append, Quaternion.identity, .32f, EventType.Repaint);
                if (new Bounds(append, Vector3.one * .5f).IntersectRay(localRay, out float appendDistance) && appendDistance < nearest)
                { nearest = appendDistance; hitBox = b; hitCube = cubes.Count; }
            }
        }
        // Pick the nearest visible cube in world space, including boxes rotated off the grid.
        if (hitBox >= 0 && e.type == EventType.MouseDown && e.button == 0 && GUIUtility.hotControl == 0)
        {
            JObject box = (JObject)boxes[hitBox]; _box = hitBox;
            if (_boxTool == 3) A.RemoveCube(box, hitCube); else A.ApplyCubeBrush(box, hitCube, _brushColor, _cubeBrushMode, _keyBrush);
            e.Use(); Repaint();
        }
    }
    private void DrawRoadPreview()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (_roadPreviewMode != 2) _roadPreview?.DrawSceneMeshes();
        if (_roadPreviewMode == 1) return;
        Handles.color = new Color(.25f, .9f, 1f);
        foreach (Vector3[] line in _roadLines)
        {
            if (line.Length < 2) continue;
            // The curve is the centerline, slightly lifted to avoid fighting the road's depth buffer.
            Handles.DrawAAPolyLine(3f, line.Select(p => p + Vector3.up * .12f).ToArray());
        }
    }
    private void DrawBoxPreview(JObject layout, bool interactive = true, List<Vector3> previewSlots = null)
    {
        JArray boxes = A.Array(layout, "Boxes");
        for (int i = 0; i < boxes.Count; i++)
        {
            JObject box = (JObject)boxes[i]; Vector3 position = A.Vector(box, "Position"); Quaternion rotation = Quaternion.Euler(A.Vector(box, "Rotation"));
            int type = A.Int(box, "BoxType"), capacity = type == 2 ? 6 : 4;
            if (type == 1) rotation = Quaternion.identity; // Single ignores Rotation at runtime.
            Bounds footprint = LoopLevelBoardLayout.LocalBounds(type);
            using (new Handles.DrawingScope(Matrix4x4.TRS(position, rotation, Vector3.one)))
            {
                Handles.color = interactive && _tab == 1 && _box == i ? Color.yellow : Color.white;
                Handles.DrawWireCube(footprint.center, footprint.size);
                JArray cubes = A.Array(box, "Cubes");
                int visible = type == 1 ? Math.Min(1, cubes.Count) : cubes.Count;
                for (int c = 0; c < visible; c++)
                {
                    JObject cube = (JObject)cubes[type == 1 ? cubes.Count - 1 : c];
                    Vector3 cubePos = type == 1 ? new Vector3(0, 1.12f, 0) :
                        new Vector3(0, .8f, (type == 2 ? -2.8f : -1.2f) + c * .8f);
                    bool hidden = A.Int(cube, "IsSecret") > 0;
                    Handles.color = hidden ? Color.gray : A.ColorFor(A.Int(cube, "Type"));
                    Handles.CubeHandleCap(0, cubePos, Quaternion.identity, .72f, EventType.Repaint);
                    if (hidden) Handles.Label(cubePos + Vector3.up * .35f, "?");
                    if (A.Int(cube, "GroupKeyId") > 0) Handles.Label(cubePos + Vector3.up * .35f, "Chìa khóa " + A.Int(cube, "GroupKeyId"));
                }
                if (type == 1) Handles.Label(Vector3.up * 1.6f, "Hàng đợi: " + cubes.Count);
                else { Handles.color = Color.cyan; Handles.DrawAAPolyLine(2, new Vector3(0,.35f,1.8f), new Vector3(0,.35f,2.5f)); }
            }
            Handles.color = interactive && _box == i && _tab == 1 ? Color.yellow : Color.white;
            if (interactive && Handles.Button(position + Vector3.up * .75f, rotation, .16f, .2f, Handles.SphereHandleCap)) { _box = i; _tab = 1; Repaint(); }
            string label = "Hộp " + i;
            if (A.Int(box, "LockColor") > 0) label += " · color lock " + A.Int(box, "LockColor");
            if (A.Int(box, "NumLock") > 0) label += " · number lock " + A.Int(box, "NumLock");
            Handles.Label(position + Vector3.up * 1.1f, label);
            List<Vector3> slots = previewSlots ?? _slots;
            if (_showBindings && slots.Count > 0)
            {
                int slot = A.Int(box, "PathIndexV2"); if (slot < 1) slot = A.NearestSlot(box, slots);
                if (slot <= slots.Count && slot > 0)
                {
                    Handles.color = new Color(1,.8f,.2f,.7f); Handles.DrawDottedLine(position, slots[slot - 1], 4);
                    if (interactive && _tab == 1 && _box == i) Handles.Label(slots[slot - 1], "Liên kết " + slot);
                }
            }
        }
    }
    private void TransformHandle(JObject obj)
    {
        Vector3 previous = A.Vector(obj, "Position"), angles = A.Vector(obj, "Rotation");
        Quaternion rotation = Quaternion.Euler(angles);
        EditorGUI.BeginChangeCheck();
        if (Tools.current == Tool.Rotate)
        {
            Quaternion next = Handles.RotationHandle(rotation, previous);
            if (EditorGUI.EndChangeCheck()) obj["Rotation"] = A.Vector(next.eulerAngles);
        }
        else
        {
            Vector3 next = Handles.PositionHandle(previous, Tools.pivotRotation == PivotRotation.Local ? rotation : Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) obj["Position"] = A.Vector(Snap(next));
        }
        if (_autoBind && (previous != A.Vector(obj, "Position") || angles != A.Vector(obj, "Rotation"))) obj["PathIndexV2"] = A.NearestSlot(obj, _slots);
    }
    private void DrawPointHandles(JObject layout)
    {
        JArray paths = A.Array(layout, "Paths");
        for (int p = 0; p < paths.Count; p++)
        {
            JArray points = A.Array((JObject)paths[p], "ControlPoints");
            for (int i = 0; i < points.Count; i++)
            {
                JObject point = (JObject)points[i]; Vector3 position = A.Vector(point, "Position");
                Handles.color = _path == p && _point == i ? Color.yellow : Color.cyan;
                if (Handles.Button(position, Quaternion.identity, .13f, .18f, Handles.SphereHandleCap)) { _path = p; _point = i; Repaint(); }
                Handles.Label(position + Vector3.up * .3f, p + ":" + i);
                if (_path != p || _point != i) continue;
                EditorGUI.BeginChangeCheck(); Vector3 next = Handles.PositionHandle(position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    A.MovePoint(points, i, Snap(next));
                    if (_autoSmoothPoints) A.SmoothRoad((JObject)paths[p], _roadSmoothing);
                    position = A.Vector(point, "Position");
                }
                if (!_showTangents) continue;
                foreach (string key in new[] { "InTangent", "OutTangent" })
                {
                    Vector3 tangent = A.Vector(point, key); Handles.color = key == "InTangent" ? Color.magenta : Color.green;
                    Handles.DrawDottedLine(position, tangent, 4); Handles.Label(tangent, key);
                    EditorGUI.BeginChangeCheck();
                    Vector3 moved = Handles.FreeMoveHandle(tangent, HandleUtility.GetHandleSize(tangent) * .06f, Vector3.zero, Handles.CubeHandleCap);
                    if (EditorGUI.EndChangeCheck()) point[key] = A.Vector(moved);
                }
            }
        }
    }
}
#endif
