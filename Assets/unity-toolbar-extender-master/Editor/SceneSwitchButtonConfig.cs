using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;
using UnityEditor.SceneManagement;

internal static class ToolbarStyles
{
    public static readonly GUIStyle commandButtonStyle;

    static ToolbarStyles()
    {
        commandButtonStyle = new GUIStyle("Command")
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            imagePosition = ImagePosition.ImageAbove,
            fontStyle = FontStyle.Normal,
            fixedWidth = 120f
        };
    }
}

[InitializeOnLoad]
public class SceneSwitchButtonConfig
{
    static SceneSwitchButtonConfig()
    {
        ToolbarExtender.LeftToolbarGUI.Add(OnLeftToolbarGUI);
        ToolbarExtender.RightToolbarGUI.Add(OnRightToolbarGUI);
    }

    private static void OnLeftToolbarGUI()
    {
        GUILayout.FlexibleSpace();

        //if (GUILayout.Button(new GUIContent(UISupport.Decor, GetToolTipText(UISupport.Decor)), ToolbarStyles.commandButtonStyle))
        //{
        //    EditorUtility.FocusProjectWindow();
        //    var dataObject = AssetDatabase.LoadAssetAtPath(UISupport.PathDecorData, typeof(ScriptableObject));
        //    Selection.activeObject = dataObject;
        //}

        // if (GUILayout.Button(new GUIContent("Level Data", GetToolTipText(UISupport.SceneGame)), ToolbarStyles.commandButtonStyle))
        // {
        //     Selection.activeObject = Resources.Load("Data/LevelCollections/LevelCollection");
        // }

        // if (GUILayout.Button(new GUIContent("Theme Data", GetToolTipText(UISupport.SceneGame)), ToolbarStyles.commandButtonStyle))
        // {
        //     Selection.activeObject = Resources.Load("Data/ThemeManagerData");
        // }

        // if (GUILayout.Button(new GUIContent("Tutorial Data", GetToolTipText(UISupport.SceneGame)), ToolbarStyles.commandButtonStyle))
        // {
        //     Selection.activeObject = Resources.Load("Data/Tutorials/TutorialDataCollection");
        // }
    }

    private static void OnRightToolbarGUI()
    {
        if (GUILayout.Button(new GUIContent("Game Scene", GetToolTipText(UISupport.SceneGame)), ToolbarStyles.commandButtonStyle))
        {
            SceneHelper.OpenScene(UISupport.SceneGame);
        }
        if (GUILayout.Button(new GUIContent("Splash Scene", GetToolTipText(UISupport.SplashScene)), ToolbarStyles.commandButtonStyle))
        {
            SceneHelper.OpenScene(UISupport.SplashScene);
        }
        if (GUILayout.Button(new GUIContent("Home Scene", GetToolTipText(UISupport.HomeScene)), ToolbarStyles.commandButtonStyle))
        {
            SceneHelper.OpenScene(UISupport.HomeScene);
        }
    }

    private static string GetToolTipText(string sceneName)
    {
        return "Open " + sceneName + " scene";
    }
}

internal static class SceneHelper
{
    private static string sceneToOpen;

    public static void OpenScene(string sceneName)
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        sceneToOpen = sceneName;
        EditorApplication.update += OnUpdate;
    }

    private static void OnUpdate()
    {
        if (sceneToOpen == null ||
            EditorApplication.isPlaying || EditorApplication.isPaused ||
            EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.update -= OnUpdate;

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            // need to get scene via search because the path to the scene
            // file contains the package version so it'll change over time
            string[] guids = AssetDatabase.FindAssets("t:scene " + sceneToOpen, null);
            if (guids.Length == 0)
            {
                Debug.LogWarning("Couldn't find scene file");
            }
            else
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                EditorSceneManager.OpenScene(scenePath);
            }
        }

        sceneToOpen = null;
    }
}