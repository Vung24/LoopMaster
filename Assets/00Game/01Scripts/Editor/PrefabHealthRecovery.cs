#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PrefabHealthRecovery
{
    private const string Folder = "ReverseEngineering/PrefabHealthRecovery";
    private const string Request = Folder + "/audit.request";
    private static readonly Regex ScriptReference = new Regex(@"m_Script: \{fileID: (-?\d+), guid: ([a-f0-9]{32}), type: \d+\}");

    [Serializable] public class Issue
    {
        public string prefab;
        public string kind;
        public string location;
        public string detail;
    }
    [Serializable] public class Report
    {
        public string unityVersion;
        public string checkedUtc;
        public int prefabs;
        public int scriptReferences;
        public string[] repairedScriptImports;
        public Issue[] issues;
        public string openStage;
        public bool openStageDirty;
    }

    static PrefabHealthRecovery() { EditorApplication.update += RunRequested; }
    private static void RunRequested()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        AuditAndRepairImports();
    }

    [MenuItem("Tools/LoopMaster/Audit Result Prefabs and Repair Script Imports")]
    public static void AuditAndRepairImports()
    {
        var report = new Report { unityVersion = Application.unityVersion, checkedUtc = DateTime.UtcNow.ToString("o") };
        var issues = new List<Issue>();
        var repaired = new HashSet<string>();
        var bindings = new Dictionary<string, string>();
        var paths = AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith("Assets/00Game/AAResources/Game/Prefab/Done/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Assets/00Game/AAResources/Game/Prefab/Fail/", StringComparison.OrdinalIgnoreCase))
            .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).OrderBy(path => path).ToArray();
        foreach (string path in paths)
        {
            report.prefabs++;
            try
            {
                bool reimportPrefab = false;
                foreach (Match match in ScriptReference.Matches(File.ReadAllText(path)))
                {
                    report.scriptReferences++;
                    string guid = match.Groups[2].Value;
                    string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                    string bindingKey = guid + ":" + match.Groups[1].Value;
                    if (!bindings.TryGetValue(bindingKey, out string problem))
                    {
                        problem = null;
                        if (string.IsNullOrEmpty(scriptPath)) problem = "GUID " + guid + " does not resolve.";
                        else if (scriptPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        {
                            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                            if (script == null || script.GetClass() == null)
                            {
                                AssetDatabase.ImportAsset(scriptPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                                script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                                if (script != null && script.GetClass() != null) repaired.Add(scriptPath);
                            }
                            Type type = script == null ? null : script.GetClass();
                            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
                                problem = scriptPath + " resolves to " + (type == null ? "NULL" : type.FullName) + ", not a MonoBehaviour.";
                        }
                        bindings[bindingKey] = problem;
                    }
                    if (problem != null) issues.Add(new Issue { prefab = path, kind = "script-binding", location = guid, detail = problem });
                    if (repaired.Contains(scriptPath)) reimportPrefab = true;
                }
                if (reimportPrefab) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) issues.Add(new Issue { prefab = path, kind = "load", detail = "Prefab cannot be loaded." });
                else Inspect(prefab, path, issues);
            }
            catch (Exception error) { issues.Add(new Issue { prefab = path, kind = "exception", detail = error.ToString() }); }
        }
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            report.openStage = stage.assetPath;
            report.openStageDirty = stage.scene.isDirty;
            Inspect(stage.prefabContentsRoot, "OPEN STAGE " + stage.assetPath, issues);
        }
        report.repairedScriptImports = repaired.OrderBy(path => path).ToArray();
        report.issues = issues.ToArray();
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/imported-audit.json", JsonUtility.ToJson(report, true));
        File.WriteAllLines(Folder + "/imported-audit.txt", new[] {
            "Prefabs=" + report.prefabs + " script references=" + report.scriptReferences + " repaired imports=" + repaired.Count + " issues=" + issues.Count,
            "Checked UTC=" + report.checkedUtc,
            "Open stage=" + report.openStage + " dirty=" + report.openStageDirty
        }.Concat(issues.Select(issue => issue.kind + " | " + issue.prefab + " | " + issue.location + " | " + issue.detail)));
        Debug.Log("Prefab health audit: prefabs=" + report.prefabs + ", repaired imports=" + repaired.Count + ", issues=" + issues.Count + ". See " + Folder + "/imported-audit.txt");
    }

    private static void Inspect(GameObject root, string path, List<Issue> issues)
    {
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
        {
            string location = AnimationUtility.CalculateTransformPath(node, root.transform);
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
            if (missing > 0) issues.Add(new Issue { prefab = path, kind = "missing-script", location = location, detail = "Count=" + missing });
            foreach (Component component in node.GetComponents<Component>())
            {
                if (component == null) continue;
                using (var serialized = new SerializedObject(component))
                {
                    SerializedProperty property = serialized.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            issues.Add(new Issue { prefab = path, kind = "broken-reference", location = location + " / " + component.GetType().Name + "." + property.propertyPath,
                                detail = "Missing object instanceID=" + property.objectReferenceInstanceIDValue });
                        if (property.objectReferenceValue is Material material &&
                            (material.shader == null || material.shader.name == "Hidden/InternalErrorShader"))
                            issues.Add(new Issue { prefab = path, kind = "material-shader", location = location + " / " + property.propertyPath,
                                detail = AssetDatabase.GetAssetPath(material) });
                    }
                }
            }
        }
    }
}
#endif
