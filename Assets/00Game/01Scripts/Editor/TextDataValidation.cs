#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TextDataValidation
{
    private const string Request = "Rules/TextData/validate.request";
    private const string Report = "Rules/TextData/unity-validation.txt";
    static TextDataValidation() { EditorApplication.update += Requested; }
    private static void Requested()
    {
        if (!File.Exists(Request)) { EditorApplication.update -= Requested; return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Validate();
        EditorApplication.update -= Requested;
    }
    [MenuItem("Tools/LoopMaster/Validate ReferenceData Text")]
    public static void Validate()
    {
        var lines = new List<string>();
        int labels = 0, errors = 0;
        try
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath("71c1514a6bd24e1e882cebbe1904ce04");
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            lines.Add("Font script: " + scriptPath + " class=" + (script == null ? "NULL" : Convert.ToString(script.GetClass())));
            lines.Add("Runtime font type=" + typeof(TMP_FontAsset).AssemblyQualifiedName);
            TMP_FontAsset probe = ScriptableObject.CreateInstance<TMP_FontAsset>();
            MonoScript runtimeScript = MonoScript.FromScriptableObject(probe);
            lines.Add("Runtime font script=" + (runtimeScript == null ? "NULL" : AssetDatabase.GetAssetPath(runtimeScript)));
            UnityEngine.Object.DestroyImmediate(probe);
            if (script != null && script.GetClass() == null && !File.Exists("Rules/TextData/compiler-cache-repaired.txt"))
            {
                File.WriteAllText("Rules/TextData/compiler-cache-repaired.txt", "Requested Unity compilation with CleanBuildCache because TMP_FontAsset's MonoScript had no class.\n");
                AssetDatabase.ImportAsset(scriptPath, ImportAssetOptions.ForceUpdate);
                File.WriteAllText(Request, "Validate after rebuilding TMP script metadata.\n");
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(UnityEditor.Compilation.RequestScriptCompilationOptions.CleanBuildCache);
                lines.Add("Rebuilding Unity script metadata, validation pending.");
                File.WriteAllLines(Report, lines);
                return;
            }
            string fontPath = "Assets/00Game/02Art/Fonts/Snui/Assets/Nunito-Black SDF.asset";
            lines.Add("Before import type=" + Convert.ToString(AssetDatabase.GetMainAssetTypeAtPath(fontPath)));
            foreach (string folder in new[] { "Assets/00Game/02Art/Fonts/Snui", "Assets/00Game/AAResources/Common/Font" })
                foreach (string file in Directory.GetFiles(folder, "*.asset", SearchOption.AllDirectories))
                    AssetDatabase.ImportAsset(file.Replace('\\', '/'), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fontPath))
                if (obj != null) lines.Add("Font object: " + obj.name + " type=" + obj.GetType().FullName);
            TMP_FontAsset primary = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            lines.Add("Primary font=" + (primary == null ? "NULL" : primary.name));
            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            foreach (string path in AssetsInFolders(assetPaths, "Assets/00Game"))
            {
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null) Inspect(asset, path, lines, ref labels, ref errors);
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    Inspect(root, scene.path, lines, ref labels, ref errors);
            }
            foreach (string name in new[] { "LoadingText", "GameLoading" })
            {
                string path = "Assets/00Game/AAResources/Common/Prefab/" + name + ".prefab";
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                LoadingText loading = asset == null ? null : asset.GetComponentInChildren<LoadingText>(true);
                if (!HasLoadingAnimationReference(loading))
                { lines.Add("ERROR: missing loading dots binding " + path); errors++; }
            }
            var uniqueFonts = new HashSet<TMP_FontAsset>();
            foreach (string path in AssetsInFolders(assetPaths, "Assets/00Game/02Art/Fonts/Snui", "Assets/00Game/AAResources/Common/Font"))
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null || !uniqueFonts.Add(font)) continue;
                lines.Add("FONT " + font.name + " material=" + (font.material == null ? "NULL" : font.material.name) +
                    " shader=" + (font.material == null || font.material.shader == null ? "NULL" : font.material.shader.name) +
                    " atlas=" + (font.atlasTexture == null ? "NULL" : font.atlasTexture.name) +
                    " glyphs=" + font.glyphTable.Count + " characters=" + font.characterTable.Count);
            }
            RenderFonts(lines);
        }
        catch (Exception e) { errors++; lines.Add(e.ToString()); }
        lines.Insert(0, (errors == 0 ? "PASS" : "FAIL") + ": labels=" + labels + ", errors=" + errors);
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllLines(Report, lines);
        Debug.Log("Text validation: " + Report + "; " + lines[0]);
    }

    private static IEnumerable<string> AssetsInFolders(string[] paths, params string[] folders)
    {
        foreach (string path in paths)
        {
            foreach (string folder in folders)
            {
                if (!path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)) continue;
                yield return path;
                break;
            }
        }
    }

    private static bool HasLoadingAnimationReference(LoadingText loading)
    {
        if (loading == null) return false;
        using (var serialized = new SerializedObject(loading))
        {
            SerializedProperty property = serialized.GetIterator();
            while (property.NextVisible(true))
                if (property.propertyPath == "_animText") return property.objectReferenceValue != null;
        }
        return false;
    }

    private static void Inspect(GameObject root, string path, List<string> lines, ref int labels, ref int errors)
    {
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            labels++;
            if (label.font == null || label.font.material == null || label.font.atlasTexture == null ||
                label.font.material.shader == null || label.font.material.shader.name == "Hidden/InternalErrorShader")
            { lines.Add("ERROR: font/atlas/shader " + path + "/" + label.name); errors++; }
            // TMP loads the font's default material when no per-label preset is serialized.
            Material material = label.fontSharedMaterial != null ? label.fontSharedMaterial : label.font != null ? label.font.material : null;
            if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
            { lines.Add("ERROR: label material " + path + "/" + label.name); errors++; }
            else if (label.font != null && material.mainTexture != label.font.atlasTexture)
            { lines.Add("ERROR: font/material atlas mismatch " + path + "/" + label.name); errors++; }
            if (label.font != null && label.font.atlasPopulationMode == AtlasPopulationMode.Dynamic && label.font.sourceFontFile == null)
            { lines.Add("ERROR: missing dynamic source font " + path + "/" + label.name); errors++; }
        }
    }

    private static void RenderFonts(List<string> lines)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        RenderTexture render = null;
        Texture2D image = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("FontValidationCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 3.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.18f);
            var canvasObject = new GameObject("FontValidationCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, preview);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1080, 640);
            canvasRect.localScale = Vector3.one * 0.01f;
            string[] samples = { "LEVEL 12", "CONGRATULATIONS!", "LOADING ...", "0/8   1 2 3 4 5 6 7 8 9" };
            for (int i = 0; i < samples.Length; i++)
            {
                var node = new GameObject("Sample", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                node.transform.SetParent(canvasObject.transform, false);
                TMP_Text text = node.GetComponent<TMP_Text>();
                text.font = Snui.ReferenceDataTextFonts.Resolve(i == 1 ? 1 : 0);
                text.fontSize = i == 1 ? 60 : 64;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.rectTransform.sizeDelta = new Vector2(1040, 110);
                text.rectTransform.anchoredPosition = new Vector2(0, 220 - i * 145);
                text.text = samples[i];
                text.ForceMeshUpdate(true, true);
                if (text.textInfo.characterCount < samples[i].Length || text.textInfo.meshInfo[0].vertexCount == 0)
                    throw new InvalidOperationException("No text geometry for " + samples[i]);
                lines.Add("Rendered " + text.font.name + ": " + samples[i]);
            }
            Canvas.ForceUpdateCanvases();
            render = new RenderTexture(1080, 640, 24);
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            image = new Texture2D(1080, 640, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1080, 640), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Rules/TextData");
            File.WriteAllBytes("Rules/TextData/font-preview.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (render != null) UnityEngine.Object.DestroyImmediate(render);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
#endif
