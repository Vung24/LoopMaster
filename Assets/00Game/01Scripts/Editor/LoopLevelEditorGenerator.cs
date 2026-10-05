#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        Header("Auto Generate — choose a preset, generate, then use the result");
        using (new EditorGUI.DisabledScope(running))
        {
            int difficulty = EditorGUILayout.Popup("Difficulty preset", s.Difficulty, new[] { "Easy", "Normal", "Hard", "Very hard" });
            if (difficulty != s.Difficulty) s.ApplyDifficulty(difficulty);
            using (new EditorGUILayout.HorizontalScope())
            {
                s.Seed = EditorGUILayout.IntField("Seed", s.Seed);
                if (GUILayout.Button("New seed", GUILayout.Width(90))) s.Seed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            }
            _generatedLevelNumber = Mathf.Max(1, EditorGUILayout.IntField("Level number", _generatedLevelNumber));
            s.BoxCount = EditorGUILayout.IntSlider("Total boxes", s.BoxCount, 5, 24);
            s.ColorCount = EditorGUILayout.IntSlider("Normal colors", s.ColorCount, 2, 14);
            s.EmptyBoxes = EditorGUILayout.IntSlider("Normal buffer boxes", s.EmptyBoxes, 1, 4);
            s.RoadShape = EditorGUILayout.Popup("Road shape", s.RoadShape, new[] { "Rounded rectangle", "Oval", "Circle" });
            s.BoardLayout = EditorGUILayout.Popup("Box arrangement", s.BoardLayout, new[] { "Two banks facing the road", "Balanced rows" });
            s.BoxGap = EditorGUILayout.Slider("Gap between box edges", s.BoxGap, .35f, 2);
            s.RoadClearance = EditorGUILayout.Slider("Gap to inner road wall", s.RoadClearance, .4f, 3);
            s.RoadCapacity = EditorGUILayout.IntSlider("Road capacity (groups)", s.RoadCapacity, 2, 16);
            Header("Special mechanics");
            s.SingleBoxes = EditorGUILayout.IntSlider("Single queue boxes", s.SingleBoxes, 0, 3);
            s.LongBoxes = EditorGUILayout.IntSlider("Long boxes", s.LongBoxes, 0, 6);
            s.SecretPercent = EditorGUILayout.IntSlider("Hidden cubes (%)", s.SecretPercent, 0, 70);
            s.ColorLocks = EditorGUILayout.IntSlider("Color-locked boxes", s.ColorLocks, 0, 3);
            s.NumberLocks = EditorGUILayout.IntSlider("Number-locked boxes", s.NumberLocks, 0, 3);
            s.Groups = EditorGUILayout.IntSlider("Locked groups + keys", s.Groups, 0, 3);
            EditorGUILayout.HelpBox("Total boxes includes buffers, Single and Long. Buffers reserve spare capacity; mixing can partially fill them. Long needs at least 2 boxes. Each group receives a key outside the locks.", MessageType.None);
            s.VerifySolution = EditorGUILayout.Toggle("Find a solution before use", s.VerifySolution);
            _generatorAdvanced = EditorGUILayout.Foldout(_generatorAdvanced, "Advanced generation settings", true);
            if (_generatorAdvanced)
            {
                s.ScrambleMoves = EditorGUILayout.IntSlider("Mixing moves", s.ScrambleMoves, 4, 160);
                using (new EditorGUI.DisabledScope(s.BoardLayout == 0))
                {
                    s.BoardColumns = EditorGUILayout.IntSlider("Max columns (0 = auto)", s.BoardColumns, 0, 24);
                    s.BoardAspect = EditorGUILayout.Slider("Board width / depth", s.BoardAspect, .5f, 1.5f);
                }
                s.MaxStates = EditorGUILayout.IntSlider("Max search states / seed", s.MaxStates, 100, 20000);
                s.Attempts = EditorGUILayout.IntSlider("Seed attempts", s.Attempts, 1, 16);
            }
        }
        List<string> errors = s.Validate();
        foreach (string error in errors) EditorGUILayout.HelpBox(error, MessageType.Warning);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(running || errors.Count > 0 || EditorApplication.isPlaying || _drawingRoad))
                if (GUILayout.Button("Generate preview")) StartGeneration();
            using (new EditorGUI.DisabledScope(!running))
                if (GUILayout.Button("Cancel")) StopGeneration();
        }
        if (_generationJob == null) return;
        EditorGUILayout.HelpBox(_generationJob.Status ?? "Preparing generation…", running ? MessageType.Info : _generationJob.Result != null ? MessageType.Info : MessageType.Warning);
        LoopGeneratedLevel candidate = _generationJob.Candidate;
        if (candidate == null) return;
        bool changedSettings = GenerationSettingsChanged;
        if (changedSettings) EditorGUILayout.HelpBox("Settings changed. Generate again to preview and use the new settings.", MessageType.Info);
        EditorGUILayout.LabelField("Result seed", candidate.Seed.ToString());
        EditorGUILayout.LabelField("Content", A.Array(A.Layout(candidate.Json), "Boxes").Count + " boxes · " + candidate.MixedBoxes + " mixed boxes · " + candidate.HiddenCubes + " hidden cubes");
        Bounds boardBounds = LoopLevelBoardLayout.BoardBounds(A.Layout(candidate.Json), true);
        EditorGUILayout.LabelField("Board footprint", boardBounds.size.x.ToString("F1") + " × " + boardBounds.size.z.ToString("F1") + " world units");
        EditorGUILayout.HelpBox("Two banks put each box close to the road with Normal / Long exits facing outward. Balanced rows fit the chosen aspect ratio. Both use full footprints and fit the road to box edges; road capacity affects gameplay, not physical width.", MessageType.None);
        EnsureGeneratedPreview();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Top")) { _previewOrbit = new Vector2(0, 90); _previewZoom = 1; }
            if (GUILayout.Button("3D")) { _previewOrbit = new Vector2(35, 45); _previewZoom = 1; }
            if (GUILayout.Button("Frame in Scene")) { _scenePreview = true; Frame(); }
        }
        Rect rect = GUILayoutUtility.GetRect(100, 210, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
            _generatorPreview?.DrawInspector(rect, 0, true, 0, _previewOrbit, _previewZoom, -1);
        EditorGUILayout.HelpBox("Scene preview shows the generated board. Use generated level opens it as an unsaved draft; Save As writes the JSON. Difficulty is a preset; playtest to judge the challenge.", MessageType.None);
        using (new EditorGUI.DisabledScope(running || changedSettings || _generationJob.Result == null || HasRawChanges || EditorApplication.isPlaying))
            if (GUILayout.Button(candidate.Verified ? "Use generated level (solution found)" : "Use generated level (unchecked)")) ApplyGeneratedLevel();
        using (new EditorGUI.DisabledScope(running || changedSettings || _generationJob.Result == null || LoopLevelPlaytest.Busy || EditorApplication.isCompiling))
            if (GUILayout.Button("▶ Play generated preview in Game View")) PlayInGameView(_generationJob.Result.Json);
        EditorGUILayout.HelpBox("Play runs this preview directly without applying or saving it. Winning records normal progress; use a test level number when needed.", MessageType.None);
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
        if (_generationJob != null && !_generationJob.Done) { _generationJob.Dispose(); _status = "Generation cancelled; the draft is unchanged."; }
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
        catch (Exception exception) { _status = "Cannot restore generated preview: " + exception.Message; _generationResultJson = null; }
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
            if (_showSlots)
                for (int i = 0; i < _generatedSlots.Count; i += Math.Max(1, _generatedSlots.Count / 500))
                { Handles.color = Color.yellow; Handles.Label(_generatedSlots[i] + Vector3.up * .1f, (i + 1).ToString()); }
            foreach (JObject group in A.Array(A.Layout(_generationJob.Candidate.Json), "Groups"))
                foreach (JToken index in A.Array(group, "BoxIndexes"))
                    Handles.Label(A.Vector((JObject)A.Array(A.Layout(_generationJob.Candidate.Json), "Boxes")[index.Value<int>()], "Position") + Vector3.up * 1.5f, "Group " + A.Int(group, "Id"));
        }
    }
    private void ApplyGeneratedLevel()
    {
        if (_generationJob?.Result == null || GenerationSettingsChanged || HasRawChanges || EditorApplication.isPlaying) return;
        CancelRoadSketch();
        Undo.IncrementCurrentGroup(); Undo.RecordObject(this, "Use generated level");
        Commit((JObject)_generationJob.Result.Json.DeepClone(), "Use generated level");
        _filePath = _diskText = null; _rawText = _rawBase = null;
        _editorMode = 0; _tab = 0; _box = _point = _map = -1; _path = 0;
        Undo.IncrementCurrentGroup();
        _status = "Generated draft ready. Save As Level " + A.Int(Root(), "level_number") + ".json.";
        SyncDirty(); Frame(); Repaint(); SceneView.RepaintAll();
    }
    private string SettingsSignature() => _generatedLevelNumber + ":" + JObject.FromObject(_generatorSettings).ToString(Formatting.None);
    private bool GenerationSettingsChanged => _generationSettingsJson != SettingsSignature();
}
#endif
