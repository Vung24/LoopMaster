#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class AlternativeCampaign70Window : EditorWindow
{
    [SerializeField] private int _level = 1;
    private string _status;

    [MenuItem("Tools/LoopMaster/Alternative Campaign 70/Open campaign tools")]
    public static void Open() { GetWindow<AlternativeCampaign70Window>("Alternative Campaign 70").Show(); }

    private void OnGUI()
    {
        GUILayout.Label("Alternative Campaign · 70 levels", EditorStyles.boldLabel);
        _level = EditorGUILayout.IntSlider("Level", _level, 1, 70);
        string path = Path.Combine(AlternativeCampaign70Builder.LevelFolder, "Level " + _level + ".json");
        using (new EditorGUI.DisabledScope(!File.Exists(path)))
        {
            if (GUILayout.Button("Open in level editor"))
                GetWindow<LoopLevelEditorWindow>("Trình sửa màn chơi").TryOpen(path);
            if (GUILayout.Button("Play this level in sandbox"))
            {
                if (!LoopLevelPlaytest.Play(LoopLevelAuthoring.Parse(File.ReadAllText(path)), out _status))
                    Debug.LogError(_status);
            }
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Build representative samples")) AlternativeCampaign70Builder.BuildSamples();
            if (GUILayout.Button("Build all 70 (requires accepted samples)")) AlternativeCampaign70Builder.BuildAll();
            if (GUILayout.Button("Start campaign from level 1")) AlternativeCampaign70Session.StartCampaign();
            if (GUILayout.Button("Audit current campaign")) AlternativeCampaign70Audit.Run();
        }
        EditorGUILayout.HelpBox("Draft levels require Unity replay, visual review and balance measurements before acceptance. Stop Play Mode to end the isolated campaign session.", MessageType.Info);
        if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status, EditorStyles.wordWrappedLabel);
    }
}
#endif
