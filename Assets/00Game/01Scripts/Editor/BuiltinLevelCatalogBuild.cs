using System;
using System.Collections.Generic;
using System.Globalization;
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
	private const string PackagedFolder = "Assets/00Game/Resources/" + BuiltinLevelCatalog.ResourceFolder;

	public void OnPreprocessBuild(BuildReport report) => RefreshCatalog();

	[MenuItem("Tools/Levels/Refresh Built-in Level Catalog")]
	public static void RefreshCatalog()
	{
		string folder = "Assets/" + BuiltinLevelCatalog.SourceFolder;
		if (!Directory.Exists(folder)) throw new BuildFailedException("Level data folder not found: " + folder);
		string[] paths = Directory.GetFiles(folder, "*.json");
		if (paths.Length == 0) throw new BuildFailedException("No exported levels found in " + folder);
		var levels = new SortedDictionary<int, string>();
		// Validate the entire source set before changing any packaged files.
		foreach (string path in paths)
		{
			string name = Path.GetFileNameWithoutExtension(path);
			if (!name.StartsWith("Level ", StringComparison.Ordinal) ||
				!int.TryParse(name.Substring(6), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id < 1)
				throw new BuildFailedException("Invalid level file name: " + path);
			if (levels.ContainsKey(id)) throw new BuildFailedException("Duplicate level ID " + id + ": " + path);
			if (name != "Level " + id.ToString(CultureInfo.InvariantCulture))
				throw new BuildFailedException("Expected canonical file name Level " + id + ".json: " + path);
			levels.Add(id, path);
		}

		Directory.CreateDirectory(PackagedFolder);
		AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
		var packagedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string source in levels.Values)
		{
			string destination = PackagedFolder + "/" + Path.GetFileName(source);
			packagedPaths.Add(destination);
			byte[] bytes = File.ReadAllBytes(source);
			if (!File.Exists(destination) || !bytes.SequenceEqual(File.ReadAllBytes(destination)))
				File.WriteAllBytes(destination, bytes);
			AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
		}
		foreach (string path in Directory.GetFiles(PackagedFolder, "*.json"))
		{
			string assetPath = path.Replace('\\', '/');
			if (!packagedPaths.Contains(assetPath) && !AssetDatabase.DeleteAsset(assetPath))
				throw new BuildFailedException("Cannot remove stale packaged level: " + assetPath);
		}
		BuiltinLevelCatalog catalog = AssetDatabase.LoadAssetAtPath<BuiltinLevelCatalog>(CatalogPath);
		if (catalog == null)
		{
			catalog = ScriptableObject.CreateInstance<BuiltinLevelCatalog>();
			AssetDatabase.CreateAsset(catalog, CatalogPath);
		}
		catalog.LevelIds = levels.Keys.ToArray();
		EditorUtility.SetDirty(catalog);
		AssetDatabase.SaveAssets();
		Debug.Log("Packaged " + levels.Count + " built-in JSON levels for player builds.");
	}
}
