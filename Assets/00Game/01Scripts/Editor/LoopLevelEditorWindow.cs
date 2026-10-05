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

public sealed class LoopLevelEditorWindow : EditorWindow
{
    [SerializeField] private LoopLevelDocument _document;
    [SerializeField] private string _filePath, _savedJson, _diskText;
    [SerializeField] private string _rawText, _rawBase;
    [SerializeField] private int _tab, _box = -1, _path, _point = -1, _map = -1;
    [SerializeField] private bool _scenePreview = true, _showSlots, _showBindings = true, _autoBind = true;
    [SerializeField] private float _snap = 0.25f;
    [SerializeField] private Vector2 _scroll;
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
    private static readonly string[] Tabs = { "Level", "Boxes", "Paths", "Map", "Groups / Covers", "JSON" };

    [MenuItem("Tools/LoopMaster/Level Editor")]
    public static void Open() => GetWindow<LoopLevelEditorWindow>("Level Editor").Show();

    [MenuItem("Assets/LoopMaster/Edit Level JSON", true)]
    private static bool CanOpenSelected() => Selection.activeObject is TextAsset &&
        AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    [MenuItem("Assets/LoopMaster/Edit Level JSON")]
    private static void OpenSelected()
    {
        var window = GetWindow<LoopLevelEditorWindow>("Level Editor"); window.Show();
        window.TryOpen(AssetDatabase.GetAssetPath(Selection.activeObject));
    }

    private void OnEnable()
    {
        minSize = new Vector2(470, 520);
        if (_document == null)
        {
            _document = CreateInstance<LoopLevelDocument>();
            _document.hideFlags = HideFlags.HideAndDontSave;
            _document.Json = A.NewLevel(NextLevelNumber()).ToString(Formatting.Indented);
            _savedJson = _document.Json;
        }
        Undo.undoRedoPerformed += OnUndoRedo;
        SceneView.duringSceneGui += DuringSceneGUI;
        SyncDirty();
    }
    private void OnDisable()
    {
        CancelRoadSketch();
        Undo.undoRedoPerformed -= OnUndoRedo;
        SceneView.duringSceneGui -= DuringSceneGUI;
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
        saveChangesMessage = "Save the level draft before closing Level Editor?";
        titleContent = new GUIContent("Level Editor");
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
            _status = "Preview: " + exception.Message;
        }
        return _root;
    }
    private void Commit(JObject root, string action = "Edit level")
    {
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
        _box = _point = _map = -1; _path = 0; _rawText = _rawBase = null;
        _hasValidation = false; _status = string.IsNullOrEmpty(path) ? "New level draft." : "Opened " + Path.GetFileName(path);
        SyncDirty(); Repaint(); SceneView.RepaintAll();
    }
    private bool ConfirmReplace()
    {
        SyncDirty(); if (!hasUnsavedChanges) return true;
        int choice = EditorUtility.DisplayDialogComplex("Unsaved level", "Save this draft before opening another level?", "Save", "Cancel", "Discard");
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
                if (!(A.Layout(root)[key] is JArray)) throw new FormatException("layout." + key + " must be an array.");
            if (!ConfirmReplace()) return false;
            SetDocument(root, Path.GetFullPath(path), disk); return true;
        }
        catch (Exception exception) { _status = "Cannot open level: " + exception.Message; Repaint(); return false; }
    }
    private bool ApplyRaw()
    {
        if (!HasRawChanges) return true;
        try
        {
            JObject root = A.Parse(_rawText);
            var errors = A.Validate(root, out _);
            if (errors.Count > 0) throw new FormatException(string.Join("\n", errors));
            Commit(root, "Apply level JSON"); _rawText = _rawBase = _document.Json;
            _status = "JSON applied."; SyncDirty(); return true;
        }
        catch (Exception exception) { _status = "JSON not applied: " + exception.Message; return false; }
    }
    public bool TrySave(bool saveAs)
    {
        if (_drawingRoad) { _status = "Finish or cancel the road sketch before saving."; Repaint(); return false; }
        try
        {
            if (!ApplyRaw()) return false;
            JObject root = Root(); _errors = A.Validate(root, out _warnings); _hasValidation = true;
            if (_errors.Count > 0) { _status = "Fix validation errors before saving."; Repaint(); return false; }
            string path = _filePath;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                string folder = string.IsNullOrEmpty(path) ? Path.GetFullPath(A.LevelFolder) : Path.GetDirectoryName(path);
                path = EditorUtility.SaveFilePanel("Save level JSON", folder, "Level " + A.Int(root, "level_number"), "json");
                if (string.IsNullOrEmpty(path)) return false;
            }
            path = Path.GetFullPath(path);
            bool inLevelFolder = string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(A.LevelFolder), StringComparison.OrdinalIgnoreCase);
            if (inLevelFolder && !string.Equals(Path.GetFileName(path), "Level " + A.Int(root, "level_number") + ".json", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Use filename Level " + A.Int(root, "level_number") + ".json in the level folder. Choose Save As after changing the level number.");
            if (File.Exists(path))
            {
                bool same = !string.IsNullOrEmpty(_filePath) && string.Equals(path, Path.GetFullPath(_filePath), StringComparison.OrdinalIgnoreCase);
                bool changedOnDisk = same && File.ReadAllText(path) != _diskText;
                if ((changedOnDisk || !same) && !EditorUtility.DisplayDialog("Replace level JSON?",
                    changedOnDisk ? "The file changed outside this window. Replace it with your draft?" : "A level already exists at this path. Replace it?", "Replace", "Cancel")) return false;
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
                    catch (Exception exception) { _status = "JSON saved, but catalog refresh failed: " + exception.Message; return true; }
                }
            }
            _status = "Saved " + Path.GetFileName(path); return true;
        }
        catch (Exception exception) { _status = "Save failed: " + exception.Message; Repaint(); return false; }
    }
    public override void SaveChanges()
    {
        if (!TrySave(false)) throw new OperationCanceledException("Level draft was not saved. Resolve errors or use Save As.");
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
            if (GUILayout.Button("New", EditorStyles.toolbarButton) && ConfirmReplace()) SetDocument(A.NewLevel(NextLevelNumber()), null, null);
            if (GUILayout.Button("Open…", EditorStyles.toolbarButton))
            {
                string file = EditorUtility.OpenFilePanel("Open level JSON", A.LevelFolder, "json");
                if (!string.IsNullOrEmpty(file)) TryOpen(file);
            }
            if (GUILayout.Button("Save", EditorStyles.toolbarButton)) TrySave(false);
            if (GUILayout.Button("Save As…", EditorStyles.toolbarButton)) TrySave(true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Undo", EditorStyles.toolbarButton)) Undo.PerformUndo();
            if (GUILayout.Button("Redo", EditorStyles.toolbarButton)) Undo.PerformRedo();
        }
        EditorGUILayout.LabelField(string.IsNullOrEmpty(_filePath) ? "Untitled — save as Level N.json" : _filePath, EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            _scenePreview = EditorGUILayout.ToggleLeft("Scene preview", _scenePreview, GUILayout.Width(110));
            _showSlots = EditorGUILayout.ToggleLeft("Slot numbers", _showSlots, GUILayout.Width(100));
            _showBindings = EditorGUILayout.ToggleLeft("Box bindings", _showBindings, GUILayout.Width(105));
            if (GUILayout.Button("Frame / Top", GUILayout.Width(95))) Frame();
        }
        _tab = GUILayout.Toolbar(_tab, Tabs);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        try
        {
            JObject root = Root(), layout = A.Layout(root);
            if (_tab == 5) DrawJson();
            else if (HasRawChanges)
                EditorGUILayout.HelpBox("There are unapplied JSON edits. Apply or reset them on the JSON tab before using the inspector.", MessageType.Warning);
            else
            {
                EditorGUI.BeginChangeCheck();
                switch (_tab)
                {
                    case 0: DrawLevel(root, layout); break;
                    case 1: DrawBoxes(root, layout); break;
                    case 2: DrawPaths(layout); break;
                    case 3: DrawMap(layout); break;
                    case 4: DrawGroups(layout); break;
                }
                EditorGUI.EndChangeCheck(); // Compare JSON so list-button changes are recorded as well.
                Commit(root);
            }
        }
        catch (Exception exception) { EditorGUILayout.HelpBox("Cannot inspect draft: " + exception.Message + "\nUse JSON to repair or reopen the file.", MessageType.Error); }
        EditorGUILayout.Space();
        if (_hasValidation)
        {
            foreach (string error in _errors.Take(20)) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (string warning in _warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }
        if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
        EditorGUILayout.EndScrollView(); SyncDirty();
        if (GUI.changed) { Repaint(); SceneView.RepaintAll(); }
    }
    private void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown || !(e.control || e.command) || e.keyCode != KeyCode.S) return;
        TrySave(e.shift); e.Use();
    }
    private void DrawLevel(JObject root, JObject layout)
    {
        Header("Level settings");
        Integer(root, "level_number", "Level number"); Integer(root, "puzzle_id", "Puzzle ID");
        Integer(layout, "Version", "Data version", Puzzle.LegacyVersion);
        int difficulty = EditorGUILayout.Popup("Difficulty", A.Int(layout, "HardLevel"), new[] { "Normal", "Hard", "Very Hard" });
        if (difficulty != A.Int(layout, "HardLevel")) layout["HardLevel"] = difficulty;
        Number(layout, "Size", "Board size", 10f); Integer(layout, "PathLength", "Road capacity (groups)");
        Number(layout, "PathSectionDistance", "Conveyor slot distance", 0.2f);
        EditorGUILayout.HelpBox("PathLength is road capacity in groups, not the Bézier length. Color IDs and box indices follow the game's JSON format.", MessageType.None);
        EditorGUILayout.LabelField("Content", A.Array(layout, "Boxes").Count + " boxes · " + A.Array(layout, "Paths").Count + " paths · " + _slots.Count + " conveyor slots");
        if (GUILayout.Button("Validate draft")) { _errors = A.Validate(root, out _warnings); _hasValidation = true; _status = _errors.Count == 0 ? "Structural validation passed." : _errors.Count + " error(s)."; }
        if (GUILayout.Button("Rebind all boxes to nearest slots"))
            foreach (JObject box in A.Array(layout, "Boxes")) box["PathIndexV2"] = A.NearestSlot(box, _slots);
        EditorGUILayout.Space(); Header("Playtest");
        EditorGUILayout.HelpBox("Open GameScene and enter Play Mode, then send this draft to the active controller. Winning records normal progress; use a test level number when needed.", MessageType.None);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || GameController.Current == null))
            if (GUILayout.Button("Play draft on current GameController"))
            {
                var errors = A.Validate(root, out _);
                if (errors.Count > 0) { _errors = errors; _hasValidation = true; _status = "Fix validation errors before playtesting."; }
                else { GameController.Current.Initialize(GameLevelDataConverter.FromJson(root.ToString(Formatting.None))); DebugLevelWindow.Open(); _status = "Draft sent to gameplay."; }
            }
    }
    private void DrawBoxes(JObject root, JObject layout)
    {
        JArray boxes = A.Array(layout, "Boxes"); Header("Boxes — select here or click a box in Scene View");
        for (int i = 0; i < boxes.Count; i++)
        {
            var item = (JObject)boxes[i];
            if (GUILayout.Toggle(_box == i, "Box " + i + "   " + (BoxType)A.Int(item, "BoxType") + "   " + A.Array(item, "Cubes").Count + " cubes", "Button")) _box = i;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add box")) { boxes.Add(A.NewBox(Vector3.zero)); _box = boxes.Count - 1; }
            using (new EditorGUI.DisabledScope(_box < 0 || _box >= boxes.Count))
            {
                if (GUILayout.Button("Duplicate")) { var copy = (JObject)boxes[_box].DeepClone(); copy["Position"] = A.Vector(A.Vector(copy, "Position") + Vector3.right * 2); copy["PathIndexV2"] = 0; boxes.Add(copy); _box = boxes.Count - 1; }
                if (GUILayout.Button("Delete")) { A.RemoveBox(root, _box); _box = Math.Min(_box, boxes.Count - 1); }
            }
        }
        if (_box < 0 || _box >= boxes.Count) return;
        JObject box = (JObject)boxes[_box]; Header("Box " + _box);
        Vector3 previous = A.Vector(box, "Position"), previousRotation = A.Vector(box, "Rotation");
        box["BoxType"] = (int)(BoxType)EditorGUILayout.EnumPopup("Box type", (BoxType)A.Int(box, "BoxType"));
        VectorField(box, "Position"); VectorField(box, "Rotation");
        _autoBind = EditorGUILayout.Toggle("Auto-bind moved boxes", _autoBind);
        _snap = Mathf.Max(0, EditorGUILayout.FloatField("Scene grid snap", _snap));
        if (_autoBind && (previous != A.Vector(box, "Position") || previousRotation != A.Vector(box, "Rotation"))) box["PathIndexV2"] = A.NearestSlot(box, _slots);
        Integer(box, "PathIndexV2", "Conveyor binding (1-based)");
        EditorGUILayout.HelpBox("Binding 0 finds the nearest slot at runtime. Moving a path does not change explicit bindings; use Rebind all boxes when needed.", MessageType.None);
        Integer(box, "PathIndex", "Legacy path index");
        Header("Locks / special rules");
        Integer(box, "LockColor", "Color lock"); Integer(box, "NumLock", "Number lock");
        Integer(box, "LimitedColor", "Limited color"); Flag(box, "IsEnterOnly", "Enter only"); Flag(box, "IsAdjacentLocked", "Adjacent locked");
        Indices(box, "MultiColorLock", "Multi-color lock IDs", true);
        Header("Cubes — bottom to top");
        JArray cubes = A.Array(box, "Cubes"); int capacity = A.Int(box, "BoxType") == 2 ? 6 : A.Int(box, "BoxType") == 1 ? 64 : 4;
        if (A.Int(box, "BoxType") == 1) EditorGUILayout.HelpBox("Single boxes hold a queue and release one cube per click. The editor limits this queue to 64 entries.", MessageType.None);
        for (int i = 0; i < cubes.Count; i++)
        {
            var cube = (JObject)cubes[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Cube " + i, GUILayout.Width(55));
                    cube["Type"] = EditorGUILayout.IntField("Color ID", A.Int(cube, "Type"));
                    EditorGUI.DrawRect(GUILayoutUtility.GetRect(20, 18, GUILayout.Width(20)), A.ColorFor(A.Int(cube, "Type")));
                    if (GUILayout.Button("↑", GUILayout.Width(25)) && i + 1 < cubes.Count) { A.SwapCubes(box, i, i + 1); break; }
                    if (GUILayout.Button("×", GUILayout.Width(25))) { A.RemoveCube(box, i); break; }
                }
                Flag(cube, "IsSecret", "Secret"); Flag(cube, "IsFixed", "Fixed");
                Integer(cube, "GroupKeyId", "Group key ID"); Integer(cube, "NumLock", "Number lock");
            }
        }
        using (new EditorGUI.DisabledScope(cubes.Count >= capacity))
            if (GUILayout.Button("Add cube (" + cubes.Count + " / " + capacity + ")")) { cubes.Add(A.NewCube(1)); box["Cubes"] = cubes; }
        EditorGUILayout.HelpBox("Normal colors: 1–14. Long colors: 101–105. Surprise colors: 201+; define replacements in layout.SurpriseDatas on the JSON tab. LinkedCubes is preserved and editable there.", MessageType.None);
    }
    private void DrawPaths(JObject layout)
    {
        JArray paths = A.Array(layout, "Paths");
        DrawQuickRoadTools(paths);
        if (_drawingRoad) return;
        if (paths.Count == 0) return;
        _path = Mathf.Clamp(_path, 0, paths.Count - 1);
        _path = EditorGUILayout.Popup("Selected path", _path, Enumerable.Range(0, paths.Count).Select(i => "Path " + i).ToArray());
        DrawPathInspectorPreview();
        JObject path = (JObject)paths[_path]; Flag(path, "IsCircle", "Loop");
        _autoSmoothPoints = EditorGUILayout.Toggle(new GUIContent("Auto-smooth moved points", "Recalculate tangents after moving, adding or deleting a point."), _autoSmoothPoints);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Smooth road")) A.SmoothRoad(path, _roadSmoothing);
            if (GUILayout.Button("Straight road")) A.SmoothRoad(path, 0);
        }
        if (GUILayout.Button("Delete selected path")) { paths.RemoveAt(_path); _point = -1; return; }
        JArray points = A.Array(path, "ControlPoints");
        bool repeatedEnd = points.Count > 1 && (A.Vector((JObject)points[0], "Position") - A.Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
        for (int i = 0; i < points.Count; i++)
            if (GUILayout.Toggle(_point == i, "Point " + i + "    " + A.Vector((JObject)points[i], "Position").ToString("F2"), "Button")) _point = i;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(points.Count >= (A.Int(path, "IsCircle") > 0 && !repeatedEnd ? 127 : 128)))
                if (GUILayout.Button("Append point"))
                {
                    int lastAnchor = points.Count - (repeatedEnd ? 2 : 1);
                    Vector3 position = lastAnchor < 0 ? Vector3.zero : A.Vector((JObject)points[lastAnchor], "Position") + Vector3.right * 2;
                    _point = points.Count - (repeatedEnd ? 1 : 0);
                    points.Insert(_point, A.Point(position, position, position));
                    if (_autoSmoothPoints) A.SmoothRoad(path, _roadSmoothing);
                }
            using (new EditorGUI.DisabledScope(_point < 0 || _point >= points.Count - 1 || points.Count >= 128))
                if (GUILayout.Button("Split segment after point")) _point = A.SplitSegment(points, _point);
        }
        if (_point < 0 || _point >= points.Count) return;
        JObject point = (JObject)points[_point]; Header("Point " + _point + " — drag in Scene View");
        Vector3 positionBefore = A.Vector(point, "Position");
        Vector3 positionAfter = EditorGUILayout.Vector3Field("Position", positionBefore);
        if (positionAfter != positionBefore)
        {
            A.MovePoint(points, _point, positionAfter);
            if (_autoSmoothPoints) A.SmoothRoad(path, _roadSmoothing);
        }
        _showTangents = EditorGUILayout.Foldout(_showTangents, "Advanced: edit tangents", true);
        if (_showTangents) { VectorField(point, "InTangent"); VectorField(point, "OutTangent"); }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (_showTangents)
            {
                if (GUILayout.Button("Straight tangents")) point["InTangent"] = point["OutTangent"] = A.Vector(A.Vector(point, "Position"));
                if (GUILayout.Button("Mirror outgoing → incoming")) point["InTangent"] = A.Vector(A.Vector(point, "Position") * 2 - A.Vector(point, "OutTangent"));
            }
            bool seam = points.Count > 1 && (A.Vector((JObject)points[0], "Position") - A.Vector((JObject)points[points.Count - 1], "Position")).sqrMagnitude < .000001f;
            int anchors = points.Count - (seam ? 1 : 0);
            using (new EditorGUI.DisabledScope(anchors <= (A.Int(path, "IsCircle") > 0 || seam ? 3 : 2)))
                if (GUILayout.Button("Delete point"))
                {
                    A.RemoveRoadPoint(path, _point); _point = Math.Min(_point, points.Count - 1);
                    if (_autoSmoothPoints) A.SmoothRoad(path, _roadSmoothing);
                }
        }
        EditorGUILayout.HelpBox(_showTangents ? "Tangents are absolute positions. Moving a point moves its tangents with it. Split inserts a midpoint without changing the curve." :
            "Drag points to shape the road. Auto-smooth moved points keeps bends smooth. Use Advanced only when you need to adjust an individual bend.", MessageType.None);
    }
    private void DrawQuickRoadTools(JArray paths)
    {
        Header("Build road — click points or use a shape");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _sketchClosed = EditorGUILayout.Toggle("Closed loop", _sketchClosed);
            _sketchSmooth = EditorGUILayout.Toggle("Smooth bends", _sketchSmooth);
            if (_sketchSmooth) _roadSmoothing = EditorGUILayout.Slider("Smoothing", _roadSmoothing, 0, 1);
            _roadCenter.y = EditorGUILayout.FloatField("Road height (Y)", _roadCenter.y);
            _snap = Mathf.Max(0, EditorGUILayout.FloatField("Grid snap (0 = off)", _snap));
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                if (!_drawingRoad && GUILayout.Button("Draw road in Scene View")) StartRoadSketch();
            if (_drawingRoad)
            {
                EditorGUILayout.HelpBox("Click to place points. Enter / Finish creates the road; Backspace removes the last point; Esc cancels. A closed loop needs 3 points, an open road needs 2.", MessageType.Info);
                EditorGUILayout.LabelField("Placed points", _sketchPoints.Count.ToString());
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!CanFinishSketch))
                        if (GUILayout.Button("Finish road")) FinishRoadSketch();
                    if (GUILayout.Button("Undo point")) RemoveSketchPoint();
                    if (GUILayout.Button("Cancel")) CancelRoadSketch();
                }
                UpdateSketchPreview(false);
                Rect rect = GUILayoutUtility.GetRect(100, 200, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint && _sketchPoints.Count >= 2)
                    _sketchPreview?.DrawInspector(rect, 0, true, _roadPreviewMode, new Vector2(0, 90), 1, -1);
            }
            _showRoadPresets = EditorGUILayout.Foldout(_showRoadPresets, "Or create a shape road", true);
            if (_showRoadPresets)
            {
                _roadPreset = EditorGUILayout.Popup("Shape", _roadPreset, new[] { "Rounded rectangle", "Oval / circle" });
                _roadCenter = EditorGUILayout.Vector3Field("Center", _roadCenter);
                _roadWidth = Mathf.Max(.5f, EditorGUILayout.FloatField("Width (X)", _roadWidth));
                _roadDepth = Mathf.Max(.5f, EditorGUILayout.FloatField("Depth (Z)", _roadDepth));
                if (_roadPreset == 0) _cornerRadius = EditorGUILayout.Slider("Corner radius", _cornerRadius, 0, Mathf.Min(_roadWidth, _roadDepth) * .5f);
                using (new EditorGUI.DisabledScope(_drawingRoad))
                    if (GUILayout.Button("Create shape road"))
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
        _status = "Drawing road: click in Scene View, then press Enter to finish.";
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
        if (_sketchPoints.Count >= (_sketchClosed ? 127 : 128)) { _status = "Point limit reached; finish the road."; return; }
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
        if (!CanFinishSketch) { _status = "Place more points before finishing."; return; }
        JObject path = A.RoadFromPositions(_sketchPoints, _sketchClosed, _sketchSmooth ? _roadSmoothing : 0);
        JObject root = Root(); JArray paths = A.Array(A.Layout(root), "Paths");
        paths.Add(path); _path = paths.Count - 1; _point = 0;
        // One finished road is one Undo step; cancelled sketches never change the document.
        Undo.IncrementCurrentGroup(); Commit(root, "Draw road"); Undo.IncrementCurrentGroup();
        _autoSmoothPoints = _sketchSmooth;
        CancelRoadSketch(); _status = "Road created. Drag its points to adjust the shape.";
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
            Handles.Label(_sketchPoints[i] + Vector3.up * .25f, i == 0 && _sketchClosed ? "Start / click to close" : i.ToString());
        }
        if (_sketchCursorValid && canvas)
        {
            Handles.color = Color.white; Handles.DrawWireDisc(_sketchCursor, Vector3.up, .18f);
            Handles.Label(_sketchCursor + Vector3.up * .3f, "Click to place point");
        }
        Handles.BeginGUI();
        GUILayout.BeginArea(panel, EditorStyles.helpBox);
        GUILayout.Label("DRAW ROAD — " + _sketchPoints.Count + " points", EditorStyles.boldLabel);
        GUILayout.Label("Click to place • Backspace to remove • Enter to finish • Esc to cancel");
        GUILayout.Label(_sketchClosed ? "Click the green start point to close the loop." : "Open road: place at least two points.");
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!CanFinishSketch)) if (GUILayout.Button("Finish road")) FinishRoadSketch();
            if (GUILayout.Button("Undo point")) RemoveSketchPoint();
            if (GUILayout.Button("Cancel")) CancelRoadSketch();
        }
        GUILayout.EndArea(); Handles.EndGUI();
    }
    private void DrawPathInspectorPreview()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _roadPreviewMode = EditorGUILayout.Popup("Preview", _roadPreviewMode, new[] { "Road + path", "Road", "Path" });
            using (new EditorGUILayout.HorizontalScope())
            {
                _previewAllPaths = GUILayout.Toggle(_previewAllPaths, "All paths", GUILayout.Width(80));
                if (GUILayout.Button("Top")) { _previewOrbit = new Vector2(0, 90); _previewZoom = 1f; }
                if (GUILayout.Button("3D")) { _previewOrbit = new Vector2(35, 45); _previewZoom = 1f; }
                if (GUILayout.Button("Edit in Scene")) { _scenePreview = true; Frame(); }
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
            EditorGUILayout.LabelField("Drag to orbit / scroll to zoom. Edit points and tangents in Scene View.", EditorStyles.miniLabel);
        }
    }
    private void DrawMap(JObject layout)
    {
        JArray items = A.Array(layout, "MapData"); Header("Map decorations");
        for (int i = 0; i < items.Count; i++)
            if (GUILayout.Toggle(_map == i, i + ": " + items[i].Value<string>("Name"), "Button")) _map = i;
        if (GUILayout.Button("Add map item"))
        {
            items.Add(new JObject { ["Name"] = "Decor9", ["Position"] = A.Vector(Vector3.zero), ["Rotation"] = A.Vector(Vector3.zero), ["Scale"] = A.Vector(Vector3.one) }); layout["MapData"] = items; _map = items.Count - 1;
        }
        if (_map < 0 || _map >= items.Count) return;
        JObject item = (JObject)items[_map];
        item["Name"] = EditorGUILayout.TextField("Prefab name", item.Value<string>("Name"));
        VectorField(item, "Position"); VectorField(item, "Rotation"); VectorField(item, "Scale");
        if (GUILayout.Button("Delete map item")) { items.RemoveAt(_map); _map = Math.Min(_map, items.Count - 1); }
        EditorGUILayout.HelpBox("Use the existing map prefab name (for example Decor9). Scene preview shows decoration bounds and labels, not prefab artwork.", MessageType.None);
    }
    private void DrawGroups(JObject layout)
    {
        Header("Groups — zero-based box indices"); JArray groups = A.Array(layout, "Groups");
        for (int i = 0; i < groups.Count; i++)
        {
            JObject group = (JObject)groups[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Integer(group, "Id", "Group ID"); Integer(group, "LockedByColor", "Lock color");
                Indices(group, "BoxIndexes", "Box indices");
                if (GUILayout.Button("Delete group")) { groups.RemoveAt(i); break; }
            }
        }
        if (GUILayout.Button("Add group")) { groups.Add(new JObject { ["Id"] = groups.OfType<JObject>().Select(g => A.Int(g, "Id")).DefaultIfEmpty().Max() + 1, ["BoxIndexes"] = new JArray(_box >= 0 ? _box : 0), ["LockedByColor"] = 0 }); layout["Groups"] = groups; }
        Header("Rolling covers"); JArray covers = A.Array(layout, "Covers");
        for (int i = 0; i < covers.Count; i++)
        {
            JObject cover = (JObject)covers[i]; Indices(cover, "BoxIndexes", "Cover " + i + " boxes");
            if (GUILayout.Button("Delete cover " + i)) { covers.RemoveAt(i); break; }
        }
        if (GUILayout.Button("Add cover")) { covers.Add(new JObject { ["BoxIndexes"] = new JArray(_box >= 0 ? _box : 0) }); layout["Covers"] = covers; }
        EditorGUILayout.HelpBox("Deleting a box removes it from groups/covers and shifts higher indices. Unknown JSON fields and surprise replacements stay in the document.", MessageType.None);
    }
    private void DrawJson()
    {
        if (_rawText == null || (!HasRawChanges && _rawBase != _document.Json)) _rawText = _rawBase = _document.Json;
        EditorGUILayout.HelpBox("Advanced fields: LinkedCubes, SurpriseDatas and custom metadata. Apply JSON validates and creates one Undo step. Unapplied edits are included in the unsaved-changes prompt.", MessageType.None);
        _rawText = EditorGUILayout.TextArea(_rawText, GUILayout.MinHeight(350), GUILayout.ExpandHeight(true));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply JSON")) ApplyRaw();
            if (GUILayout.Button("Reset text to draft")) { _rawText = _rawBase = _document.Json; SyncDirty(); }
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
        Vector3 previous = A.Vector(obj, key), next = EditorGUILayout.Vector3Field(key, previous);
        if (previous != next) obj[key] = A.Vector(next);
    }
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
        catch { _status = label + ": use integer IDs separated by commas."; }
    }

    private void Frame()
    {
        try
        {
            JObject layout = A.Layout(Root()); var bounds = new Bounds(Vector3.zero, Vector3.one * 8);
            foreach (JObject box in A.Array(layout, "Boxes")) bounds.Encapsulate(A.Vector(box, "Position"));
            foreach (Vector3[] road in _roadLines) foreach (Vector3 point in road) bounds.Encapsulate(point);
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
            JObject root = Root(), layout = A.Layout(root);
            Event e = Event.current;
            if (_drawingRoad && _tab == 2)
            {
                using (new Handles.DrawingScope(Matrix4x4.identity)) { DrawRoadPreview(); DrawRoadSketch(view); }
                return;
            }
            if (e.type == EventType.MouseDown && e.button == 0) { Undo.IncrementCurrentGroup(); _dragGroup = Undo.GetCurrentGroup(); }
            using (new Handles.DrawingScope(Matrix4x4.identity))
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                DrawRoadPreview(); DrawBoxPreview(layout); DrawMapPreview(layout);
                // Control points sit inside the generated surface. Keep edit handles visible above it.
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                if (_showSlots)
                    for (int i = 0; i < _slots.Count; i += Math.Max(1, _slots.Count / 500))
                    {
                        Handles.color = Color.yellow; Handles.DotHandleCap(0, _slots[i], Quaternion.identity, 0.04f, EventType.Repaint);
                        Handles.Label(_slots[i] + Vector3.up * 0.1f, (i + 1).ToString());
                    }
                if (_tab == 2) DrawPointHandles(layout);
                else if (_tab == 1 && _box >= 0 && _box < A.Array(layout, "Boxes").Count)
                    TransformHandle((JObject)A.Array(layout, "Boxes")[_box], true);
                else if (_tab == 3 && _map >= 0 && _map < A.Array(layout, "MapData").Count)
                    TransformHandle((JObject)A.Array(layout, "MapData")[_map], false);
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            }
            Commit(root, "Move level element");
            if (e.rawType == EventType.MouseUp && _dragGroup >= 0) { Undo.CollapseUndoOperations(_dragGroup); _dragGroup = -1; }
        }
        catch (Exception) { /* Invalid drafts remain repairable in the window; do not spam every repaint. */ }
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
    private void DrawBoxPreview(JObject layout)
    {
        JArray boxes = A.Array(layout, "Boxes");
        for (int i = 0; i < boxes.Count; i++)
        {
            JObject box = (JObject)boxes[i]; Vector3 position = A.Vector(box, "Position"); Quaternion rotation = Quaternion.Euler(A.Vector(box, "Rotation"));
            int type = A.Int(box, "BoxType"), capacity = type == 2 ? 6 : 4;
            Vector3 size = type == 1 ? new Vector3(.8f,.5f,.8f) : new Vector3(type == 2 ? 2.3f : 1.6f,.5f,1.6f);
            using (new Handles.DrawingScope(Matrix4x4.TRS(position, rotation, Vector3.one)))
            {
                Handles.color = _tab == 1 && _box == i ? Color.yellow : Color.white;
                Handles.DrawWireCube(Vector3.up * .25f, size);
                JArray cubes = A.Array(box, "Cubes"); int columns = type == 1 ? 1 : type == 2 ? 3 : 2;
                for (int c = 0; c < cubes.Count; c++)
                {
                    Vector3 cubePos = type == 1 ? new Vector3(0, .5f + c * .65f, 0) :
                        new Vector3((c % columns - (columns - 1) * .5f) * .7f, .5f, (c / columns - (capacity / columns - 1) * .5f) * .7f);
                    Handles.color = A.ColorFor(A.Int((JObject)cubes[c], "Type"));
                    Handles.CubeHandleCap(0, cubePos, Quaternion.identity, .58f, EventType.Repaint);
                }
            }
            Handles.color = _box == i && _tab == 1 ? Color.yellow : Color.white;
            if (Handles.Button(position + Vector3.up * .75f, rotation, .16f, .2f, Handles.SphereHandleCap)) { _box = i; _tab = 1; Repaint(); }
            Handles.Label(position + Vector3.up * 1.1f, "Box " + i);
            if (_showBindings && _slots.Count > 0)
            {
                int slot = A.Int(box, "PathIndexV2"); if (slot < 1) slot = A.NearestSlot(box, _slots);
                if (slot <= _slots.Count && slot > 0)
                {
                    Handles.color = new Color(1,.8f,.2f,.7f); Handles.DrawDottedLine(position, _slots[slot - 1], 4);
                    if (_tab == 1 && _box == i) Handles.Label(_slots[slot - 1], "Binding " + slot);
                }
            }
        }
    }
    private void DrawMapPreview(JObject layout)
    {
        JArray items = A.Array(layout, "MapData");
        for (int i = 0; i < items.Count; i++)
        {
            JObject item = (JObject)items[i]; Vector3 position = A.Vector(item, "Position");
            Quaternion rotation = Quaternion.Euler(A.Vector(item, "Rotation"));
            Handles.color = new Color(.8f,.5f,1f);
            using (new Handles.DrawingScope(Matrix4x4.TRS(position, rotation, A.Vector(item, "Scale")))) Handles.DrawWireCube(Vector3.up * .5f, Vector3.one);
            Handles.Label(position, item.Value<string>("Name"));
            if (_tab == 3 && Handles.Button(position, rotation, .12f, .15f, Handles.CubeHandleCap)) { _map = i; Repaint(); }
        }
    }
    private void TransformHandle(JObject obj, bool box)
    {
        Vector3 previous = A.Vector(obj, "Position"), angles = A.Vector(obj, "Rotation");
        Quaternion rotation = Quaternion.Euler(angles);
        EditorGUI.BeginChangeCheck();
        if (Tools.current == Tool.Rotate)
        {
            Quaternion next = Handles.RotationHandle(rotation, previous);
            if (EditorGUI.EndChangeCheck()) obj["Rotation"] = A.Vector(next.eulerAngles);
        }
        else if (!box && Tools.current == Tool.Scale)
        {
            Vector3 scale = Handles.ScaleHandle(A.Vector(obj, "Scale"), previous, rotation, HandleUtility.GetHandleSize(previous));
            if (EditorGUI.EndChangeCheck()) obj["Scale"] = A.Vector(scale);
        }
        else
        {
            Vector3 next = Handles.PositionHandle(previous, Tools.pivotRotation == PivotRotation.Local ? rotation : Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) obj["Position"] = A.Vector(Snap(next));
        }
        if (box && _autoBind && (previous != A.Vector(obj, "Position") || angles != A.Vector(obj, "Rotation"))) obj["PathIndexV2"] = A.NearestSlot(obj, _slots);
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
