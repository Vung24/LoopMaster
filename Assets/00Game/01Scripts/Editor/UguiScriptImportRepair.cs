#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class UguiScriptImportRepair
{
    private const string Folder = "ReverseEngineering/UguiRepair";
    private const string Request = Folder + "/repair.request";
    private const string Package = "Packages/com.unity.ugui/Runtime";
    private static readonly Regex ScriptGuid = new Regex(@"m_Script: \{fileID: 11500000, guid: ([a-f0-9]{32}), type: 3\}");
    private static bool autoCheckPending = true;

    static UguiScriptImportRepair() { EditorApplication.update += RunRequested; }

    private static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Request))
        {
            autoCheckPending = false;
            File.Delete(Request);
            Repair();
        }
        else if (autoCheckPending)
        {
            autoCheckPending = false;
            var lines = new List<string>();
            var repairedGuids = new HashSet<string>();
            try
            {
                int errors = RepairScriptBindings(lines, repairedGuids, out int checkedScripts);
                RefreshPrefabs(repairedGuids, lines);
                if (errors > 0 || repairedGuids.Count > 0)
                {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllLines(Folder + "/automatic-validation.txt", lines);
                    string summary = "uGUI automatic import check: checked=" + checkedScripts +
                        ", reimported=" + repairedGuids.Count + ", errors=" + errors;
                    if (errors > 0) Debug.LogWarning(summary); else Debug.Log(summary);
                }
            }
            catch (Exception error) { Debug.LogWarning("uGUI automatic import check failed: " + error); }
        }
    }

    [MenuItem("Tools/LoopMaster/Repair uGUI Script Imports")]
    public static void Repair()
    {
        var lines = new List<string> { "Unity " + Application.unityVersion, "Checked UTC: " + DateTime.UtcNow.ToString("o") };
        var repairedGuids = new HashSet<string>();
        int errors = 0, checkedScripts = 0;
        try
        {
            errors += RepairScriptBindings(lines, repairedGuids, out checkedScripts);
            RefreshPrefabs(repairedGuids, lines);

            foreach (string name in new[] { "UICanvas", "UICanvasBlue" })
            {
                string path = "Assets/00Game/AAResources/Game/Prefab/UI/" + name + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                errors += Inspect(prefab, path, lines);
                if (errors == 0) errors += VerifySave(prefab, name, lines);
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath.StartsWith("Assets/00Game/AAResources/Game/Prefab/UI/", StringComparison.Ordinal))
            {
                lines.Add("Open stage: " + stage.assetPath + " | unsaved edits=" + stage.scene.isDirty);
                int stageErrors = Inspect(stage.prefabContentsRoot, "Open stage", lines);
                errors += stageErrors;
                if (stageErrors == 0 && errors == 0) errors += VerifySave(stage.prefabContentsRoot, "OpenStage", lines);
            }
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        catch (Exception error) { errors++; lines.Add(error.ToString()); }
        lines.Insert(0, (errors == 0 ? "PASS" : "FAIL") + ": checked uGUI components=" + checkedScripts + ", reimported scripts=" + repairedGuids.Count + ", errors=" + errors);
        Directory.CreateDirectory(Folder);
        File.WriteAllLines(Folder + "/unity-validation.txt", lines);
        Debug.Log("uGUI script import repair: " + lines[0] + "; " + Folder + "/unity-validation.txt");
    }

    private static int RepairScriptBindings(List<string> lines, HashSet<string> repairedGuids, out int checkedScripts)
    {
        int errors = 0;
        checkedScripts = 0;
        Type[] packageTypes = typeof(CanvasScaler).Assembly.GetTypes();
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(Package + "/", StringComparison.Ordinal) && p.EndsWith(".cs", StringComparison.Ordinal)).OrderBy(p => p))
        {
            Type expected = packageTypes.FirstOrDefault(t => t.Name == Path.GetFileNameWithoutExtension(path) && typeof(MonoBehaviour).IsAssignableFrom(t));
            if (expected == null || expected.IsAbstract) continue;
            checkedScripts++;
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Type before = script == null ? null : script.GetClass();
            if (before != expected)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                repairedGuids.Add(AssetDatabase.AssetPathToGUID(path));
            }
            Type after = script == null ? null : script.GetClass();
            bool valid = after == expected;
            if (!valid) errors++;
            lines.Add((valid ? "OK " : "ERROR ") + path + " | before=" + TypeName(before) + " | after=" + TypeName(after));
        }
        if (checkedScripts == 0) { errors++; lines.Add("ERROR no uGUI component scripts found in " + Package); }
        return errors;
    }

    private static void RefreshPrefabs(HashSet<string> repairedGuids, List<string> lines)
    {
        if (repairedGuids.Count == 0) return;
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p.EndsWith(".prefab", StringComparison.Ordinal)))
        {
            if (!ScriptGuid.Matches(File.ReadAllText(path)).Cast<Match>().Any(m => repairedGuids.Contains(m.Groups[1].Value))) continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            lines.Add("Refreshed prefab: " + path);
        }
    }

    private static string TypeName(Type type) { return type == null ? "NULL" : type.FullName; }

    private static int Inspect(GameObject root, string label, List<string> lines)
    {
        if (root == null) { lines.Add("ERROR cannot load " + label); return 1; }
        int errors = 0;
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
        {
            if ((node.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0) continue;
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
            errors += missing;
            if (missing > 0) lines.Add("ERROR " + label + "/" + node.name + " missing scripts=" + missing);
            foreach (MonoBehaviour component in node.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                MonoScript script = MonoScript.FromMonoBehaviour(component);
                if (script != null && script.GetClass() == component.GetType()) continue;
                errors++;
                lines.Add("ERROR " + label + "/" + node.name + " invalid script binding for " + component.GetType().FullName);
            }
        }
        if (root.GetComponent<Canvas>() != null && root.GetComponent<CanvasScaler>() == null)
        {
            errors++;
            lines.Add("ERROR " + label + ": root CanvasScaler has no live component binding; reload the prefab stage after repairing imports.");
        }
        lines.Add(label + ": invalid/missing scripts=" + errors + ", CanvasScalers=" + root.GetComponentsInChildren<CanvasScaler>(true).Length +
                  ", ContentSizeFitters=" + root.GetComponentsInChildren<ContentSizeFitter>(true).Length);
        return errors;
    }

    private static int VerifySave(GameObject root, string name, List<string> lines)
    {
        string temporaryPath = "Assets/__UguiSaveCheck_" + Guid.NewGuid().ToString("N") + ".prefab";
        try
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, temporaryPath, out bool success);
            if (!success || saved == null) { lines.Add("ERROR " + name + " save check failed"); return 1; }
            int errors = Inspect(saved, name + " saved copy", lines);
            lines.Add(name + ": save check " + (errors == 0 ? "PASS" : "FAIL"));
            return errors;
        }
        finally { if (File.Exists(temporaryPath)) AssetDatabase.DeleteAsset(temporaryPath); }
    }
}
#endif
