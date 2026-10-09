#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class UnityIdeSolutionSync : AssetPostprocessor
{
    private static string generatedSolution;

    static UnityIdeSolutionSync() { EditorApplication.delayCall += Sync; }

    private static void OnGeneratedCSProjectFiles() { Sync(); }

    private static string OnGeneratedSlnSolution(string path, string content)
    {
        generatedSolution = Path.GetFullPath(path);
        EditorApplication.delayCall -= Sync;
        EditorApplication.delayCall += Sync;
        return content;
    }

    [MenuItem("Tools/LoopMaster/Sync IDE Solution")]
    public static void Sync()
    {
        EditorApplication.delayCall -= Sync;
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string solution = generatedSolution;
            generatedSolution = null;
            SyncSettings(projectRoot, solution);
        }
        catch (Exception error)
        {
            Debug.LogWarning("Could not sync the IDE solution: " + error.Message);
        }
    }

    public static bool SyncSettings(string projectRoot, string generatedPath = null)
    {
        string projectName = new DirectoryInfo(projectRoot).Name;
        string solution = Path.Combine(projectRoot, projectName + ".sln");
        string solutionXml = solution + "x";
        if (File.Exists(solutionXml) && (!File.Exists(solution) ||
            File.GetLastWriteTimeUtc(solutionXml) > File.GetLastWriteTimeUtc(solution)))
            solution = solutionXml;
        if (!string.IsNullOrEmpty(generatedPath) && File.Exists(generatedPath) &&
            string.Equals(Path.GetDirectoryName(generatedPath), projectRoot, StringComparison.OrdinalIgnoreCase))
            solution = generatedPath;
        if (!File.Exists(solution)) return false;

        string settingsPath = Path.Combine(projectRoot, ".vscode", "settings.json");
        var settings = File.Exists(settingsPath)
            ? JObject.Parse(File.ReadAllText(settingsPath)) : new JObject();
        string solutionName = Path.GetFileName(solution);
        if ((string)settings["dotnet.defaultSolution"] == solutionName) return false;
        settings["dotnet.defaultSolution"] = solutionName;
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
        File.WriteAllText(settingsPath, settings.ToString(Newtonsoft.Json.Formatting.Indented) + Environment.NewLine);
        return true;
    }
}
#endif
