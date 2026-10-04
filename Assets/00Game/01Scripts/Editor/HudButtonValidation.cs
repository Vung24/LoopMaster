#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
[InitializeOnLoad]
public static class HudButtonValidation
{
    private const string Request = "ReverseEngineering/Verification/hud-buttons.request";
    private const string Report = "ReverseEngineering/Verification/hud-buttons.txt";
    static HudButtonValidation() { EditorApplication.delayCall += Requested; }
    private static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Requested; return; }
        File.Delete(Request);
        Validate();
    }
    [MenuItem("Tools/LoopMaster/Validate HUD Buttons")]
    public static void Validate()
    {
        var lines = new List<string>();
        lines.Add("Play mode: " + Application.isPlaying);
        EventSystem es = EventSystem.current;
        lines.Add("EventSystem: " + (es == null ? "NONE" : es.name + " enabled=" + es.isActiveAndEnabled + " module=" + es.currentInputModule));
        GameController gc = GameController.Current;
        lines.Add("Controller: " + (gc == null ? "NONE" : gc.name + " active=" + gc.IsGameplayActive + " replay=" + gc.CanReplayLevel));
        var live = new List<UICanvas>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
                live.AddRange(root.GetComponentsInChildren<UICanvas>(true));
        }
        if (Application.isPlaying && UICanvas.Current != null && !live.Contains(UICanvas.Current))
            live.Add(UICanvas.Current);
        lines.Add("Live HUDs: " + live.Count);
        foreach (UICanvas hud in live) Inspect(hud, es, lines);
        foreach (string name in new[] { "UICanvas", "UICanvasBlue" })
        {
            string path = "Assets/00Game/AAResources/Game/Prefab/UI/" + name + ".prefab";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { lines.Add("MISSING " + path); continue; }
            UICanvas hud = asset.GetComponent<UICanvas>();
            if (hud == null) { lines.Add("MISSING SCRIPT " + path); continue; }
            Inspect(hud, null, lines);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllLines(Report, lines);
        Debug.Log("HUD button validation: " + Report + "\n" + string.Join("\n", lines));
    }
    private static void Inspect(UICanvas hud, EventSystem es, List<string> lines)
    {
        lines.Add("HUD " + hud.name + " active=" + hud.gameObject.activeInHierarchy);
        foreach (string field in new[] { "_pauseButton", "_reloadButton", "_undoButton", "_shuffleButton", "_pathButton", "_speedButton" })
        {
            Button b = typeof(UICanvas).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud) as Button;
            if (b == null) { lines.Add(field + ": MISSING reference"); continue; }
            string state = field + ": " + b.name + " active=" + b.gameObject.activeInHierarchy + " enabled=" + b.enabled + " interactable=" + b.IsInteractable();
            if (es != null)
            {
                Canvas.ForceUpdateCanvases();
                Canvas c = b.GetComponentInParent<Canvas>();
                Vector2 point = RectTransformUtility.WorldToScreenPoint(c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera, b.transform.TransformPoint(((RectTransform)b.transform).rect.center));
                var hits = new List<RaycastResult>();
                es.RaycastAll(new PointerEventData(es) { position = point }, hits);
                state += " point=" + point + " hit=" + (hits.Count == 0 ? "NONE" : PathOf(hits[0].gameObject.transform));
            }
            lines.Add(state);
        }
    }
    private static string PathOf(Transform t) { return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name; }
}
#endif
