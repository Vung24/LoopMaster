#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using A = LoopLevelAuthoring;

public sealed partial class LoopLevelEditorWindow
{
    [SerializeField] private int _editorMode, _generatedLevelNumber;
    [SerializeField] private LoopLevelGeneratorSettings _generatorSettings = new LoopLevelGeneratorSettings();
    [SerializeField] private bool _generatorAdvanced;
    [SerializeField] private int _batchStartLevel = 301, _batchCount = 20;
    [SerializeField] private bool _batchAutoDifficulty = true, _showBatchSection = true;
    private LoopLevelGenerationJob _generationJob;
    private LoopLevelRoadPreview _generatorPreview;
    private string _generatedPreviewJson;
    [SerializeField] private string _generationSettingsJson, _generationResultJson;
    private List<Vector3> _generatedSlots = new List<Vector3>();

    private void DrawAutoGenerator()
    {
        if (_generatorSettings == null) _generatorSettings = new LoopLevelGeneratorSettings();
        if (_generatedLevelNumber <= 0) _generatedLevelNumber = NextLevelNumber();
        var s = _generatorSettings;
        bool running = _generationJob != null && !_generationJob.Done;
        Header("Tạo tự động — chọn thiết lập, tạo và dùng kết quả");
        using (new EditorGUI.DisabledScope(running))
        {
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.5f);
            if (GUILayout.Button($"⭐ Áp dụng thiết lập chuẩn cho Level {_generatedLevelNumber} (Tiến trình chính thức)"))
            {
                s.ApplyOfficial30Rules(_generatedLevelNumber);
            }
            GUI.backgroundColor = prevBg;

            if (GUILayout.Button("Thiết lập theo bộ 300 level: hộp thường, không Single / Long"))
            { s.ApplyDifficulty(1); s.SingleBoxes = s.LongBoxes = 0; }
            int difficulty = EditorGUILayout.Popup("Thiết lập độ khó", s.Difficulty, new[] { "Dễ", "Thường", "Khó", "Rất khó" });
            if (difficulty != s.Difficulty) s.ApplyDifficulty(difficulty);
            using (new EditorGUILayout.HorizontalScope())
            {
                s.Seed = EditorGUILayout.IntField("Hạt giống (seed)", s.Seed);
                if (GUILayout.Button("Seed mới", GUILayout.Width(90))) s.Seed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            }
            _generatedLevelNumber = Mathf.Max(1, EditorGUILayout.IntField("Số màn chơi", _generatedLevelNumber));
            s.BoxCount = EditorGUILayout.IntSlider("Tổng số hộp", s.BoxCount, 2, 24);
            s.ColorCount = EditorGUILayout.IntSlider("Số màu thường", s.ColorCount, 1, 14);
            s.EmptyBoxes = EditorGUILayout.IntSlider("Số hộp trống dự phòng", s.EmptyBoxes, 0, 4);
            s.RoadShape = EditorGUILayout.Popup("Hình đường", s.RoadShape, new[] {
                "Chữ nhật bo góc (Đóng)",
                "Bầu dục (Đóng)",
                "Tròn (Đóng)",
                "Đường thẳng mở (Cổng Vào/Ra)",
                "Đường chữ U mở (Cổng Vào & Ra ở trên)",
                "Đường chữ S mở (Luồn giữa 2 dãy)",
                "Đường uốn lượn mở (Cổng Vào/Ra)"
            });
            s.BoardLayout = EditorGUILayout.Popup("Cách xếp hộp", s.BoardLayout, new[] { "Hai dãy hướng ra đường", "Các hàng cân đối" });
            s.BoxGap = EditorGUILayout.Slider("Khoảng cách giữa mép hộp", s.BoxGap, .35f, 2);
            s.RoadClearance = EditorGUILayout.Slider("Khoảng cách tới thành đường", s.RoadClearance, .05f, 3);
            s.RoadCapacity = EditorGUILayout.IntSlider("Sức chứa đường (nhóm)", s.RoadCapacity, 2, 16);
            s.AddCapacity = EditorGUILayout.Toggle("Tạo bảng Capacity", s.AddCapacity);
            Header("Cơ chế đặc biệt");
            s.SingleBoxes = EditorGUILayout.IntSlider("Hộp đơn (hàng đợi)", s.SingleBoxes, 0, 3);
            s.LongBoxes = EditorGUILayout.IntSlider("Hộp dài", s.LongBoxes, 0, 6);
            s.SecretPercent = EditorGUILayout.IntSlider("Khối ẩn (%)", s.SecretPercent, 0, 70);
            s.ColorLocks = EditorGUILayout.IntSlider("Hộp khóa màu", s.ColorLocks, 0, 3);
            s.NumberLocks = EditorGUILayout.IntSlider("Hộp khóa số", s.NumberLocks, 0, 3);
            s.Groups = EditorGUILayout.IntSlider("Nhóm khóa + chìa khóa", s.Groups, 0, 3);
            s.EnterOnlyBoxes = EditorGUILayout.IntSlider("Hộp chỉ nhận vào", s.EnterOnlyBoxes, 0, 3);
            s.LimitedColorBoxes = EditorGUILayout.IntSlider("Hộp chỉ nhận màu", s.LimitedColorBoxes, 0, 3);
            s.CubePairs = EditorGUILayout.IntSlider("Cube đôi (cặp nối)", s.CubePairs, 0, 1);
            EditorGUILayout.HelpBox("Trong LoopMaster, băng chuyền đóng vai trò bộ đệm nên số hộp trống dự phòng chuẩn là 0 (hoặc 1–2 nếu muốn dễ hơn). Mỗi màu thường tương ứng 1 hộp 4 khối.", MessageType.None);
            s.VerifySolution = EditorGUILayout.Toggle("Tìm lời giải trước khi dùng", s.VerifySolution);
            _generatorAdvanced = EditorGUILayout.Foldout(_generatorAdvanced, "Thiết lập tạo nâng cao", true);
            if (_generatorAdvanced)
            {
                s.ScrambleMoves = EditorGUILayout.IntSlider("Số bước trộn màu", s.ScrambleMoves, 4, 160);
                using (new EditorGUI.DisabledScope(s.BoardLayout == 0))
                {
                    s.BoardColumns = EditorGUILayout.IntSlider("Số cột tối đa (0 = tự động)", s.BoardColumns, 0, 24);
                    s.BoardAspect = EditorGUILayout.Slider("Tỉ lệ rộng / sâu bàn chơi", s.BoardAspect, .5f, 1.5f);
                }
                s.MaxStates = EditorGUILayout.IntSlider("Số trạng thái tìm tối đa / seed", s.MaxStates, 100, 20000);
                s.Attempts = EditorGUILayout.IntSlider("Số seed thử", s.Attempts, 1, 16);
            }
        }
        List<string> errors = s.Validate();
        foreach (string error in errors) EditorGUILayout.HelpBox(error, MessageType.Warning);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(running || errors.Count > 0 || EditorApplication.isPlaying || _drawingRoad))
                if (GUILayout.Button("Tạo bản xem trước")) StartGeneration();
            using (new EditorGUI.DisabledScope(!running))
                if (GUILayout.Button("Hủy")) StopGeneration();
        }
        EditorGUILayout.Space(6);
        _showBatchSection = EditorGUILayout.Foldout(_showBatchSection, "Tạo danh sách level hàng loạt (Batch Generator)", true, EditorStyles.foldoutHeader);
        if (_showBatchSection)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Tạo và lưu tự động nhiều level liên tiếp theo chuẩn bộ 300 level.", EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _batchStartLevel = Mathf.Max(1, EditorGUILayout.IntField("Level bắt đầu", _batchStartLevel));
                    _batchCount = Mathf.Clamp(EditorGUILayout.IntField("Số lượng level", _batchCount), 1, 200);
                }
                _batchAutoDifficulty = EditorGUILayout.ToggleLeft("Tự động tăng dần độ khó (Dễ → Thường → Khó) theo dải level", _batchAutoDifficulty);
                EditorGUILayout.LabelField($"Sẽ tạo từ Level {_batchStartLevel} đến Level {_batchStartLevel + _batchCount - 1}.", EditorStyles.miniLabel);

                using (new EditorGUI.DisabledScope(running || EditorApplication.isPlaying || _drawingRoad || EditorApplication.isCompiling))
                {
                    if (GUILayout.Button($"⚡ Tạo và Lưu {_batchCount} level mới (Level {_batchStartLevel}..{_batchStartLevel + _batchCount - 1})", GUILayout.Height(30)))
                    {
                        GenerateBatchLevels();
                    }
                }
            }
        }
        if (_generationJob == null) return;
        EditorGUILayout.HelpBox(_generationJob.Status ?? "Đang chuẩn bị tạo…", running ? MessageType.Info : _generationJob.Result != null ? MessageType.Info : MessageType.Warning);
        LoopGeneratedLevel candidate = _generationJob.Candidate;
        if (candidate == null) return;
        bool changedSettings = GenerationSettingsChanged;
        if (changedSettings) EditorGUILayout.HelpBox("Thiết lập đã thay đổi. Tạo lại để xem trước và dùng thiết lập mới.", MessageType.Info);
        EditorGUILayout.LabelField("Seed kết quả", candidate.Seed.ToString());
        EditorGUILayout.LabelField("Nội dung", A.Array(A.Layout(candidate.Json), "Boxes").Count + " hộp · " + candidate.MixedBoxes + " hộp trộn màu · " + candidate.HiddenCubes + " khối ẩn");
        Bounds boardBounds = LoopLevelBoardLayout.BoardBounds(A.Layout(candidate.Json), true);
        EditorGUILayout.LabelField("Kích thước toàn bàn", boardBounds.size.x.ToString("F1") + " × " + boardBounds.size.z.ToString("F1") + " đơn vị thế giới");
        EditorGUILayout.HelpBox("Hai dãy đặt hộp gần đường, hướng thả của hộp thường/dài quay ra ngoài. Các hàng cân đối theo tỉ lệ đã chọn. Đường được chỉnh theo mép hộp; sức chứa đường ảnh hưởng cách chơi, không đổi bề rộng đường.", MessageType.None);
        EnsureGeneratedPreview();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Từ trên")) { _previewOrbit = new Vector2(0, 90); _previewZoom = 1; }
            if (GUILayout.Button("3D")) { _previewOrbit = new Vector2(35, 45); _previewZoom = 1; }
            if (GUILayout.Button("Căn khung ở Scene")) { _scenePreview = true; Frame(); }
        }
        Rect rect = GUILayoutUtility.GetRect(100, 210, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
            _generatorPreview?.DrawInspector(rect, 0, true, 0, _previewOrbit, _previewZoom, -1);
        EditorGUILayout.HelpBox("Scene hiển thị bàn vừa tạo. Dùng kết quả sẽ mở bản nháp chưa lưu; Lưu thành để ghi JSON. Chơi thử để đánh giá độ khó.", MessageType.None);
        using (new EditorGUI.DisabledScope(running || changedSettings || _generationJob.Result == null || HasRawChanges || EditorApplication.isPlaying))
        {
            string applyLabel = (candidate.Solution != null && candidate.Solution.Count > 0)
                ? $"Dùng màn vừa tạo (đã tìm thấy lời giải: {candidate.Solution.Count} lần bấm)"
                : "Dùng màn vừa tạo (chuẩn 300 level, luồng khóa DAG hợp lệ)";
            if (GUILayout.Button(applyLabel)) ApplyGeneratedLevel();
        }
        using (new EditorGUI.DisabledScope(running || changedSettings || _generationJob.Result == null || LoopLevelPlaytest.Busy || EditorApplication.isCompiling))
            if (GUILayout.Button("▶ Chơi thử bản vừa tạo trong Game View")) PlayInGameView(_generationJob.Result.Json);
        EditorGUILayout.HelpBox("Chơi trực tiếp bản xem trước mà chưa cần áp dụng hay lưu; mọi dữ liệu lưu trong phiên thử được giữ tạm.", MessageType.None);
    }
    private void StartGeneration()
    {
        StopGeneration();
        _generationResultJson = null;
        _generationSettingsJson = SettingsSignature();
        _generationJob = new LoopLevelGenerationJob(_generatorSettings, _generatedLevelNumber);
        _scenePreview = true; _status = null; Repaint(); SceneView.RepaintAll();
    }
    private void StopGeneration()
    {
        if (_generationJob != null && !_generationJob.Done) { _generationJob.Dispose(); _status = "Đã hủy tạo tự động; bản nháp không thay đổi."; }
    }
    private void UpdateGeneration()
    {
        if (_generationJob == null || _generationJob.Done) return;
        if (EditorApplication.isPlaying) { StopGeneration(); return; }
        LoopGeneratedLevel previous = _generationJob.Candidate;
        _generationJob.Tick();
        if (_generationJob.Done) CaptureGeneratedResult();
        if (_editorMode == 1 && _generationJob.Candidate != null && _generationJob.Candidate != previous) Frame();
        Repaint(); SceneView.RepaintAll();
    }
    private void CaptureGeneratedResult()
    {
        if (_generationJob?.Result != null)
            _generationResultJson = JsonConvert.SerializeObject(_generationJob.Result);
    }
    private void RestoreGeneratedResult()
    {
        if (_generationJob != null || string.IsNullOrEmpty(_generationResultJson)) return;
        try
        {
            var result = JsonConvert.DeserializeObject<LoopGeneratedLevel>(_generationResultJson);
            if (result?.Json != null) _generationJob = new LoopLevelGenerationJob(result);
        }
        catch (Exception exception) { _status = "Không thể khôi phục bản xem trước đã tạo: " + exception.Message; _generationResultJson = null; }
    }
    private void EnsureGeneratedPreview()
    {
        LoopGeneratedLevel candidate = _generationJob?.Candidate;
        if (candidate == null) return;
        string json = candidate.Json.ToString(Formatting.None);
        if (_generatedPreviewJson == json && _generatorPreview != null) return;
        if (_generatorPreview == null) _generatorPreview = new LoopLevelRoadPreview();
        _generatorPreview.Update(A.Array(A.Layout(candidate.Json), "Paths").ToString(Formatting.None), ExportedPuzzleJsonDecoder.Decode(json));
        _generatedSlots = A.Slots(candidate.Json); _generatedPreviewJson = json;
    }
    private void DrawGeneratedScene()
    {
        if (_generationJob?.Candidate == null) return;
        EnsureGeneratedPreview();
        using (new Handles.DrawingScope(Matrix4x4.identity))
        {
            if (Event.current.type == EventType.Repaint && _roadPreviewMode != 2) _generatorPreview.DrawSceneMeshes();
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            if (_roadPreviewMode != 1)
                foreach (Vector3[] line in _generatorPreview.Lines) { Handles.color = Color.cyan; Handles.DrawAAPolyLine(2, line); }
            DrawBoxPreview(A.Layout(_generationJob.Candidate.Json), false, _generatedSlots);
            foreach (JObject item in A.Array(A.Layout(_generationJob.Candidate.Json), "MapData"))
            {
                if (item["Name"]?.Value<string>() != "Capacity") continue;
                Handles.color = Color.green;
                Vector3 position = A.Vector(item, "Position");
                Handles.DrawWireCube(position, new Vector3(2, .5f, 1));
                Handles.Label(position + Vector3.up * .5f, "Capacity 0/" + A.Int(A.Layout(_generationJob.Candidate.Json), "PathLength"));
            }
            if (_showSlots)
                for (int i = 0; i < _generatedSlots.Count; i += Math.Max(1, _generatedSlots.Count / 500))
                { Handles.color = Color.yellow; Handles.Label(_generatedSlots[i] + Vector3.up * .1f, (i + 1).ToString()); }
            foreach (JObject group in A.Array(A.Layout(_generationJob.Candidate.Json), "Groups"))
                foreach (JToken index in A.Array(group, "BoxIndexes"))
                    Handles.Label(A.Vector((JObject)A.Array(A.Layout(_generationJob.Candidate.Json), "Boxes")[index.Value<int>()], "Position") + Vector3.up * 1.5f, "Nhóm " + A.Int(group, "Id"));
        }
    }
    private void ApplyGeneratedLevel()
    {
        if (_generationJob?.Result == null || GenerationSettingsChanged || HasRawChanges || EditorApplication.isPlaying) return;
        CancelRoadSketch();
        CancelCanvasGesture(); ClearCanvasSelection(); StopDraftSolver(); _canvasFramed = false; _workspace = true;
        Undo.IncrementCurrentGroup(); Undo.RecordObject(this, "Dùng màn vừa tạo");
        Commit((JObject)_generationJob.Result.Json.DeepClone(), "Dùng màn vừa tạo");
        _filePath = _diskText = null; _rawText = _rawBase = null;
        _editorMode = 0; _tab = 0; _box = _point = -1; _path = 0;
        Undo.IncrementCurrentGroup();
        _status = "Bản nháp vừa tạo đã sẵn sàng. Lưu thành Level " + A.Int(Root(), "level_number") + ".json.";
        SyncDirty(); Frame(); Repaint(); SceneView.RepaintAll();
    }
    private string SettingsSignature() => _generatedLevelNumber + ":" + JObject.FromObject(_generatorSettings).ToString(Formatting.None);
    private bool GenerationSettingsChanged => _generationSettingsJson != SettingsSignature();

    private void GenerateBatchLevels()
    {
        if (_batchCount <= 0 || _batchStartLevel <= 0) return;
        string title = "Đang tạo danh sách level...";
        int createdCount = 0;
        try
        {
            for (int i = 0; i < _batchCount; i++)
            {
                int levelNum = _batchStartLevel + i;
                float progress = (float)i / _batchCount;
                if (EditorUtility.DisplayCancelableProgressBar(title, $"Đang tạo Level {levelNum} ({i + 1}/{_batchCount})...", progress))
                {
                    _status = $"Đã dừng tạo hàng loạt tại Level {levelNum}. Đã tạo {createdCount} level.";
                    break;
                }

                var settings = _generatorSettings.Copy();
                if (_batchAutoDifficulty)
                {
                    if (levelNum <= 30)
                    {
                        settings.ApplyOfficial30Rules(levelNum);
                    }
                    else
                    {
                        float ratio = (float)i / Math.Max(1, _batchCount - 1);
                        int diff = ratio < 0.25f ? 0 : ratio < 0.65f ? 1 : ratio < 0.9f ? 2 : 3;
                        settings.ApplyDifficulty(diff);
                    }
                }
                settings.Seed = unchecked((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF) + levelNum * 7919);
                settings.VerifySolution = false; // Fast batch generation with guaranteed DAG locks

                LoopGeneratedLevel level = LoopLevelGenerator.Generate(settings, levelNum, settings.Seed);
                if (level?.Json != null)
                {
                    string fileName = $"Level {levelNum}.json";
                    string jsonText = level.Json.ToString(Formatting.Indented);

                    string targetFolder = Path.GetFullPath(A.LevelFolder);
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }
                    File.WriteAllText(Path.Combine(targetFolder, fileName), jsonText);

                    createdCount++;
                }
            }
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Lỗi tạo hàng loạt", ex.Message, "Đóng");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            try
            {
                BuiltinLevelCatalogBuild.RefreshCatalog();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BatchGenerator] RefreshCatalog: " + ex.Message);
            }
            _status = $"Hoàn tất tạo hàng loạt: Đã tạo và lưu {createdCount} level (từ Level {_batchStartLevel} đến Level {_batchStartLevel + createdCount - 1}). Catalog đã được cập nhật!";
            _generatedLevelNumber = _batchStartLevel + createdCount;
            Repaint();
        }
    }
}
#endif
