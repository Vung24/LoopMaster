#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed class SourceBytesJsonExporter : EditorWindow
{
    private string _source = "Assets/00Game/05LevelData/SourceBytes";
    private string _output = "Assets/LevelJsonExport";
    private int _firstLevel = 1;
    private bool _recursive;
    private bool _includeVariants;
    private string _status;

    [MenuItem("Tools/LoopMaster/Export SourceBytes to JSON")]
    public static void Open() => GetWindow<SourceBytesJsonExporter>("Bytes to JSON");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("SourceBytes → Level JSON", EditorStyles.boldLabel);
        FolderField("Source folder", ref _source);
        FolderField("Output folder", ref _output);
        _firstLevel = Mathf.Max(1, EditorGUILayout.IntField("First level number", _firstLevel));
        _recursive = EditorGUILayout.Toggle("Include subfolders", _recursive);
        _includeVariants = EditorGUILayout.Toggle("Include variants (_0, _1…)", _includeVariants);
        EditorGUILayout.HelpBox("Exports puzzle files p<ID>.bytes. Level numbers are assigned sequentially in puzzle ID order, not the original game's level order. Existing JSON files are skipped. f*.bytes and folders*.bytes are not puzzles.", MessageType.Info);
        if (GUILayout.Button("Export folder")) Export();
        if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
    }

    private static void FolderField(string label, ref string path)
    {
        EditorGUILayout.BeginHorizontal();
        path = EditorGUILayout.TextField(label, path);
        if (GUILayout.Button("Browse…", GUILayout.Width(80)))
        {
            string selected = EditorUtility.OpenFolderPanel(label, path, "");
            if (!string.IsNullOrEmpty(selected)) path = selected;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void Export()
    {
        try
        {
            _status = ExportFolder(_source, _output, _firstLevel, _recursive,
                _includeVariants, (index, count, file) =>
                    EditorUtility.DisplayCancelableProgressBar("Export SourceBytes",
                        file, (float)index / count));
            Debug.Log(_status);
        }
        catch (Exception exception)
        {
            _status = exception.Message;
            Debug.LogException(exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    // Returning true from cancel stops before processing the current file.
    public static string ExportFolder(string source, string output, int firstLevel,
        bool recursive = false, bool includeVariants = false,
        Func<int, int, string, bool> cancel = null)
    {
        if (firstLevel < 1) throw new ArgumentOutOfRangeException(nameof(firstLevel));
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        var pattern = new Regex(includeVariants ? @"^p(\d+)(?:_\d+)?$" : @"^p(\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var files = Directory.GetFiles(source, "*", recursive
                ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(file => string.Equals(Path.GetExtension(file), ".bytes", StringComparison.OrdinalIgnoreCase))
            .Select(file => new { Path = file, Match = pattern.Match(Path.GetFileNameWithoutExtension(file)) })
            .Where(file => file.Match.Success)
            .OrderBy(file => long.Parse(file.Match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0) return "No matching puzzle .bytes files found.";
        checked { _ = firstLevel + files.Length - 1; }
        Directory.CreateDirectory(output);
        var serializer = JsonSerializer.CreateDefault();
        serializer.Converters.Add(new Vector3ArrayConverter());
        var report = new List<string>();
        int exported = 0, skipped = 0, failed = 0;
        bool cancelled = false;
        for (int index = 0; index < files.Length; index++)
        {
            string file = files[index].Path;
            if (cancel != null && cancel(index, files.Length, file))
            {
                cancelled = true;
                break;
            }
            int level = firstLevel + index;
            string destination = Path.Combine(output, $"Level {level}.json");
            try
            {
                if (File.Exists(destination))
                {
                    skipped++;
                    report.Add($"SKIP | {file} | Already exists: {destination}");
                    continue;
                }
                int puzzleId = int.Parse(files[index].Match.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                Puzzle puzzle = PuzzleMessagePackDecoder.Decode(File.ReadAllBytes(file));
                if (puzzle.Boxes.Count == 0 && puzzle.Paths.Count == 0)
                    throw new FormatException("No boxes or paths decoded; not a supported puzzle.");
                JObject layout = JObject.FromObject(puzzle, serializer);
                layout.Remove("MainLevel");
                layout.Remove("InnerIndex");
                var root = new JObject
                {
                    ["level_number"] = level,
                    ["puzzle_id"] = puzzleId,
                    ["source_file"] = Path.GetFileName(file),
                    ["layout"] = layout
                };
                string json = root.ToString(Formatting.Indented);
                var decoded = ExportedPuzzleJsonDecoder.DecodeLevel(json);
                if (decoded.LevelNumber != level || decoded.PuzzleId != puzzleId ||
                    decoded.Puzzle.Boxes.Count != puzzle.Boxes.Count ||
                    decoded.Puzzle.Paths.Count != puzzle.Paths.Count)
                    throw new FormatException("JSON verification failed.");
                // CreateNew also protects against overwriting a file created during export.
                using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(json);
                exported++;
                report.Add($"OK | {file} | {destination}");
            }
            catch (Exception exception)
            {
                failed++;
                report.Add($"ERROR | {file} | {exception.Message}");
            }
        }
        string summary = $"Exported: {exported}; skipped: {skipped}; errors: {failed}; cancelled: {cancelled}. Output: {Path.GetFullPath(output)}";
        report.Insert(0, summary);
        string reportPath = Path.Combine(output, "export-report-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllLines(reportPath, report, new UTF8Encoding(false));
        return summary + "\nReport: " + reportPath;
    }

    private sealed class Vector3ArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type == typeof(Vector3);
        public override bool CanRead => false;
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Vector3 vector = (Vector3)value;
            writer.WriteStartArray();
            writer.WriteValue(vector.x);
            writer.WriteValue(vector.y);
            writer.WriteValue(vector.z);
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type type, object existingValue,
            JsonSerializer serializer) => throw new NotSupportedException();
    }
}
#endif
