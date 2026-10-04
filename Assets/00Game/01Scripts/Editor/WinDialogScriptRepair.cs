#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WinDialogScriptRepair
{
    private const string PrefabPath = "Assets/00Game/AAResources/Game/Prefab/Done/WinDialog.prefab";
    private const string RequestPath = "ReverseEngineering/WinDialogRepair/repair.request";
    private const string ReportPath = "ReverseEngineering/WinDialogRepair/unity-validation.txt";

    static WinDialogScriptRepair() { EditorApplication.update += RunRequested; }

    private static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(RequestPath))
        {
            File.Delete(RequestPath);
            Repair();
        }
        EditorApplication.update -= RunRequested;
    }

    [MenuItem("Tools/LoopMaster/Repair WinDialog Script Imports")]
    public static void Repair()
    {
        var lines = new List<string> { "Unity " + Application.unityVersion, "Checked: " + DateTimeOffset.Now.ToString("o") };
        var paths = new HashSet<string>();
        int errors = 0, repaired = 0;
        try
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(PrefabPath), @"m_Script: \{fileID: \d+, guid: ([a-f0-9]{32}), type: 3\}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
                if (string.IsNullOrEmpty(path)) { lines.Add("ERROR: missing GUID " + match.Groups[1].Value); errors++; }
                else paths.Add(path);
            }
            foreach (string path in paths)
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Type before = script == null ? null : script.GetClass();
                if (before == null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                }
                Type after = script == null ? null : script.GetClass();
                bool valid = after != null && typeof(MonoBehaviour).IsAssignableFrom(after);
                if (!valid) errors++;
                if (before == null && valid) repaired++;
                lines.Add((valid ? "OK: " : "ERROR: ") + path + " | before=" + (before == null ? "NULL" : before.FullName) +
                          " | after=" + (after == null ? "NULL" : after.FullName));
            }
            if (errors == 0)
            {
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) { errors++; lines.Add("ERROR: cannot load WinDialog"); }
                else errors += Inspect(prefab, "Prefab asset", lines);
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == PrefabPath)
                {
                    errors += Inspect(stage.prefabContentsRoot, "Open stage", lines);
                    lines.Add("Open prefab stage unsaved changes=" + stage.scene.isDirty);
                }
            }
        }
        catch (Exception error) { errors++; lines.Add(error.ToString()); }
        lines.Insert(0, (errors == 0 ? "PASS" : "FAIL") + ": script paths=" + paths.Count + ", repaired imports=" + repaired + ", errors=" + errors);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllLines(ReportPath, lines);
        Debug.Log("WinDialog script imports: " + lines[0] + "; " + ReportPath);
    }

    private static int Inspect(GameObject root, string label, List<string> lines)
    {
        int missing = 0;
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
            missing += count;
            if (count > 0) lines.Add("ERROR: " + label + "/" + node.name + " missing=" + count);
        }
        lines.Add(label + ": missing scripts=" + missing);
        return missing;
    }
}
#endif
