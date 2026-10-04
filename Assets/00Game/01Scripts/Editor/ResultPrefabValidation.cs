using System;
using System.Collections.Generic;
using System.IO;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class ResultPrefabValidation
{
    private const string ScenePath = "Assets/00Game/00Scene/GameScene.unity";
    private const string RequestPath = "ReverseEngineering/Verification/result-prefab-validation.request";
    private const string ReportPath = "ReverseEngineering/Verification/result-prefab-validation.txt";

    [InitializeOnLoadMethod]
    private static void ScheduleRequestedValidation()
    {
        EditorApplication.delayCall += RunIfRequested;
    }

    private static void RunIfRequested()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += RunIfRequested;
            return;
        }
        File.Delete(RequestPath);
        Validate();
    }

    [MenuItem("Tools/LoopMaster/Validate GameScene and Result Prefabs")]
    public static void Validate()
    {
        var lines = new List<string>();
        int missing = 0;
        try
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    missing += CheckHierarchy(root, ScenePath, lines);
                    foreach (GameController controller in root.GetComponentsInChildren<GameController>(true))
                    {
                        if (controller.WinDialogPrefab == null || controller.FailDialogPrefab == null || controller.FailDialogContentPrefab == null)
                            throw new InvalidOperationException("GameController is missing a result prefab.");
                        lines.Add("GameController: Win/Fail/FailContent prefab references OK");
                    }
                    foreach (EventSystem events in root.GetComponentsInChildren<EventSystem>(true))
                    {
                        if (events.GetComponent<StandaloneInputModule>() == null)
                            throw new InvalidOperationException("EventSystem has no StandaloneInputModule.");
                        lines.Add("EventSystem: StandaloneInputModule OK");
                    }
                }
            }
            else lines.Add("GameScene is not open; checked its prefab and material dependencies.");

            var dependencies = new HashSet<string>(AssetDatabase.GetDependencies(ScenePath, true));
            foreach (string dependency in AssetDatabase.GetDependencies("Assets/00Game/Resources/RecoveryPrefabLibrary.asset", true)) dependencies.Add(dependency);
            foreach (string path in dependencies)
            {
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) throw new InvalidOperationException("Cannot load prefab: " + path);
                    missing += CheckHierarchy(prefab, path, lines);
                }
                else if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                        throw new InvalidOperationException("Missing shader: " + path);
                }
                else if (path.EndsWith("_SkeletonData.asset", StringComparison.OrdinalIgnoreCase) &&
                         (path.Contains("/Spine/Win/") || path.Contains("/Spine/Lose/")))
                {
                    SkeletonDataAsset asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
                    var data = asset == null ? null : asset.GetSkeletonData(false);
                    if (data == null || data.Animations.Count == 0)
                        throw new InvalidOperationException("Cannot load result animation: " + path);
                    lines.Add(path + ": first animation = " + data.Animations.Items[0].Name);
                }
            }
            lines.Insert(0, missing == 0 ? "PASS: imported GameScene/result dependencies have no missing scripts or shaders." : "FAIL: missing scripts = " + missing);
        }
        catch (Exception error) { lines.Insert(0, "FAIL: " + error); }
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
    }

    private static int CheckHierarchy(GameObject root, string path, List<string> lines)
    {
        int missing = 0;
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
            missing += count;
            if (count > 0) lines.Add(path + " / " + node.name + ": missing scripts = " + count);
        }
        return missing;
    }
}
