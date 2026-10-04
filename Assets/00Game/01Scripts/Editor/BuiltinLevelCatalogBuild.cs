using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Refresh before each build so added/edited levels require no manual packaging step.
public sealed class BuiltinLevelCatalogBuild : IPreprocessBuildWithReport
{
	public int callbackOrder => 0;
	private const string CatalogPath = "Assets/00Game/Resources/BuiltinLevelCatalog.asset";

	public void OnPreprocessBuild(BuildReport report) => RefreshCatalog();

	[MenuItem("Tools/Levels/Refresh Built-in Level Catalog")]
	public static void RefreshCatalog()
	{
		string folder = "Assets/" + BuiltinLevelCatalog.SourceFolder;
		if (!Directory.Exists(folder)) throw new BuildFailedException("Level data folder not found: " + folder);
		string[] paths = Directory.GetFiles(folder, "Level *.json");
		Array.Sort(paths, StringComparer.Ordinal);
		TextAsset[] levels = paths.Select(path =>
		{
			path = path.Replace('\\', '/');
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
			TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
			if (asset == null) throw new BuildFailedException("Cannot import level JSON: " + path);
			return asset;
		}).ToArray();
		if (levels.Length == 0) throw new BuildFailedException("No exported levels found in " + folder);
		BuiltinLevelCatalog catalog = AssetDatabase.LoadAssetAtPath<BuiltinLevelCatalog>(CatalogPath);
		if (catalog == null)
		{
			catalog = ScriptableObject.CreateInstance<BuiltinLevelCatalog>();
			AssetDatabase.CreateAsset(catalog, CatalogPath);
		}
		catalog.Levels = levels;
		EditorUtility.SetDirty(catalog);
		AssetDatabase.SaveAssets();
		Debug.Log("Packaged " + levels.Length + " built-in JSON levels for player builds.");
	}
}
