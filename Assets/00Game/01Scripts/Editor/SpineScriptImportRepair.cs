#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Package MonoScripts can retain a null class binding after a package migration.
// Repair the imports without replacing components or losing their serialized data.
[InitializeOnLoad]
public static class SpineScriptImportRepair
{
    private const string PackageRoot = "Packages/com.esotericsoftware.spine.spine-unity/Runtime/spine-unity/";
    private const string ReportPath = "ReverseEngineering/SpineScriptImportRepair/unity-validation.txt";
    private const string RequestPath = "ReverseEngineering/SpineScriptImportRepair/repair.request";
    private static readonly Dictionary<string, Type> Bindings = new Dictionary<string, Type>
    {
        { "Components/SkeletonRenderer.cs", typeof(SkeletonRenderer) },
        { "Components/SkeletonAnimation.cs", typeof(SkeletonAnimation) },
        { "Asset Types/SpineAtlasAsset.cs", typeof(SpineAtlasAsset) },
        { "Asset Types/SkeletonDataAsset.cs", typeof(SkeletonDataAsset) }
    };
    private static bool pending = true;

    static SpineScriptImportRepair() { EditorApplication.update += CheckImports; }

    private static void CheckImports()
    {
        if (!pending || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        pending = false;
        EditorApplication.update -= CheckImports;
        if (File.Exists(RequestPath))
        {
            File.Delete(RequestPath);
            Repair();
            return;
        }
        foreach (var binding in Bindings)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(PackageRoot + binding.Key);
            if (script != null && script.GetClass() == binding.Value) continue;
            Repair();
            return;
        }
    }

    [MenuItem("Tools/LoopMaster/Repair Spine Script Imports")]
    public static void Repair()
    {
        var lines = new List<string> { "Project: " + Application.dataPath, "UTC: " + DateTime.UtcNow.ToString("o") };
        int errors = 0;
        try
        {
            foreach (var binding in Bindings) errors += RepairBinding(binding.Key, binding.Value, lines);

            const string comboPath = "Assets/00Game/Resources/CoreGamePrefabs/BoxComboEffect.prefab";
            string[] dependencies = AssetDatabase.GetDependencies(comboPath, true);
            foreach (string dependency in dependencies)
                if (dependency.EndsWith("_Atlas.asset", StringComparison.Ordinal))
                    AssetDatabase.ImportAsset(dependency, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            foreach (string dependency in dependencies)
                if (dependency.EndsWith("_SkeletonData.asset", StringComparison.Ordinal))
                    AssetDatabase.ImportAsset(dependency, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(comboPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            GameObject combo = Resources.Load<GameObject>("CoreGamePrefabs/BoxComboEffect");
            if (combo == null) { errors++; lines.Add("ERROR cannot load " + comboPath); }
            else
            {
                SkeletonAnimation[] animations = combo.GetComponentsInChildren<SkeletonAnimation>(true);
                if (animations.Length != 3) { errors++; lines.Add("ERROR expected three combo animations, got " + animations.Length); }
                foreach (SkeletonAnimation animation in animations)
                {
                    bool valid = animation.skeletonDataAsset != null && animation.skeletonDataAsset.GetSkeletonData(true) != null;
                    if (!valid) errors++;
                    lines.Add((valid ? "OK " : "ERROR ") + animation.name + ": SkeletonAnimation / SkeletonDataAsset=" +
                        (animation.skeletonDataAsset == null ? "NULL" : AssetDatabase.GetAssetPath(animation.skeletonDataAsset)));
                }
            }

            foreach (string path in Directory.GetFiles("Assets/00Game/Resources/CoreGamePrefabs", "*.prefab"))
                errors += Inspect(AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/')), path, lines);

            // Inspect loaded scenes directly; close only scenes opened for this audit.
            foreach (string path in Directory.GetFiles("Assets/00Game/00Scene", "*.unity"))
            {
                string scenePath = path.Replace('\\', '/');
                var scene = SceneManager.GetSceneByPath(scenePath);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects()) errors += Inspect(root, path, lines);
                }
                finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        catch (Exception error) { errors++; lines.Add(error.ToString()); }
        lines.Insert(0, (errors == 0 ? "PASS" : "FAIL") + ": missing/invalid scripts or combo data=" + errors);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllLines(ReportPath, lines);
        if (errors == 0) Debug.Log("Spine script import repair: " + lines[0]);
        else Debug.LogWarning("Spine script import repair: " + lines[0] + "; see " + ReportPath);
    }

    private static int RepairBinding(string relativePath, Type expected, List<string> lines)
    {
        string path = PackageRoot + relativePath;
        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        Type before = script == null ? null : script.GetClass();
        if (before != expected)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        }
        Type after = script == null ? null : script.GetClass();
        lines.Add((after == expected ? "OK " : "ERROR ") + path + " | before=" + (before?.FullName ?? "NULL") + " | after=" + (after?.FullName ?? "NULL"));
        return after == expected ? 0 : 1;
    }

    private static int Inspect(GameObject root, string path, List<string> lines)
    {
        if (root == null) { lines.Add("ERROR cannot load " + path); return 1; }
        int missing = 0;
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
            missing += count;
            if (count > 0) lines.Add("ERROR " + path + "/" + node.name + ": missing scripts=" + count);
        }
        lines.Add(path + "/" + root.name + ": missing scripts=" + missing);
        return missing;
    }
}
#endif
