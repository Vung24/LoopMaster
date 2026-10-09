using System;
using System.IO;
using UnityEngine;

public class BuiltinPuzzleLoader : MonoBehaviour
{
	[SerializeField] private string _bundleName = "puzzle";
	[SerializeField] private int _defaultLevel = 1;
	[SerializeField] private string _levelDataFolder = "00Game/05LevelData/LevelData";
	private AssetBundle _bundle;
	private bool _loading;
	private string _lastJsonFolder;
	private BuiltinLevelCatalog _catalog;
	private BuiltinLevelCatalog Catalog => _catalog != null ? _catalog :
		(_catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName));

	public string LastJsonFolder => _lastJsonFolder;

	public bool LoadDefault(GameController controller)
	{
		return !GameProgressStore.IsCampaignComplete && LoadExactLevel(GameProgressStore.NextPlayableLevel, controller);
	}

	/// <summary>Loads an exact unfinished level from the exported catalog.</summary>
	public bool LoadLevel(int levelNumber, GameController controller = null)
	{
		return LoadExactLevel(levelNumber, controller);
	}

	/// <summary>Loads an exact exported level without falling back to another puzzle.</summary>
	public bool LoadExactLevel(int levelNumber, GameController controller = null)
	{
		if (levelNumber < 1 || GameProgressStore.IsLevelCompleted(levelNumber)) return false;
		if (!string.IsNullOrEmpty(_lastJsonFolder) &&
			LoadJsonLevel(_lastJsonFolder, levelNumber, controller)) return true;
		return LoadProjectLevel(levelNumber, controller) || LoadPackagedLevel(levelNumber, controller);
	}

	public bool HasExactLevel(int levelNumber)
	{
		if (levelNumber < 1) return false;
		if (!string.IsNullOrEmpty(_lastJsonFolder) &&
			File.Exists(Path.Combine(_lastJsonFolder, "Level " + levelNumber + ".json"))) return true;
		return File.Exists(Path.Combine(GetProjectLevelFolder(), "Level " + levelNumber + ".json")) ||
			(Catalog != null && Catalog.HasLevel(levelNumber));
	}

	public bool LoadPuzzle(int puzzleId, GameController controller = null)
	{
		if (GameProgressStore.IsLevelCompleted(Mathf.Max(1, _defaultLevel))) return false;
		if (_loading) return false;
		_loading = true;
		try
		{
			if (!EnsureBundle()) return false;
			string suffix = "/p" + puzzleId + ".bytes";
			string assetName = null;
			foreach (string name in _bundle.GetAllAssetNames())
			{
				if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
				assetName = name;
				break;
			}
			return LoadAsset(assetName, puzzleId, controller);
		}
		finally { _loading = false; }
	}

	public bool LoadFirstPuzzle(GameController controller = null)
	{
		if (GameProgressStore.IsLevelCompleted(Mathf.Max(1, _defaultLevel))) return false;
		if (_loading) return false;
		_loading = true;
		try
		{
			if (!EnsureBundle()) return false;
			string[] names = _bundle.GetAllAssetNames();
			Array.Sort(names, StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < names.Length; i++)
			{
				string file = Path.GetFileNameWithoutExtension(names[i]);
				int id;
				if (file.Length > 1 && file[0] == 'p' && int.TryParse(file.Substring(1), out id) && LoadAsset(names[i], id, controller)) return true;
			}
			return false;
		}
		finally { _loading = false; }
	}

	/// <summary>Loads a "Level N.json" file from an exported level-pack folder.</summary>
	public bool LoadJsonLevel(string folderPath, int levelNumber, GameController controller = null)
	{
		if (string.IsNullOrEmpty(folderPath) || levelNumber < 1 || GameProgressStore.IsLevelCompleted(levelNumber)) return false;
		return LoadJsonFile(Path.Combine(folderPath, "Level " + levelNumber + ".json"), controller);
	}

	public bool LoadJsonFile(string filePath, GameController controller = null)
	{
		if (_loading || string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
		_loading = true;
		try
		{
			string json = File.ReadAllText(filePath);
			int levelNumber = ParseLevelNumber(filePath);
			if (!InitializeJson(json, levelNumber, controller)) return false;
			_lastJsonFolder = Path.GetDirectoryName(Path.GetFullPath(filePath));
			return true;
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			return false;
		}
		finally { _loading = false; }
	}

	private bool LoadPackagedLevel(int levelNumber, GameController controller)
	{
		if (_loading || Catalog == null || !Catalog.HasLevel(levelNumber)) return false;
		TextAsset asset = null;
		_loading = true;
		try
		{
			asset = Resources.Load<TextAsset>($"{BuiltinLevelCatalog.ResourceFolder}/Level {levelNumber}");
			if (asset == null) return false;
			if (!InitializeJson(asset.text, levelNumber, controller)) return false;
			_lastJsonFolder = null;
			return true;
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			return false;
		}
		finally
		{
			if (asset != null) Resources.UnloadAsset(asset);
			_loading = false;
		}
	}

	private static bool InitializeJson(string json, int levelNumber, GameController controller)
	{
		if (GameProgressStore.IsLevelCompleted(levelNumber)) return false;
		GameData previous = GameData.Current;
		GameData data = GameLevelDataConverter.FromJson(json, levelNumber);
		if (GameProgressStore.IsLevelCompleted(data.GetPuzzleActualLevel())) { GameData.SetCurrent(previous); return false; }
		GameController target = controller != null ? controller : GameController.Current;
		if (target == null)
		{
			GameData.SetCurrent(previous);
			Debug.LogError("No GameController is available to initialize level " + levelNumber + ".");
			return false;
		}
		target.Initialize(data);
		return target.Data == data;
	}

	private bool LoadProjectLevel(int levelNumber, GameController controller)
	{
		if (levelNumber < 1 || string.IsNullOrEmpty(_levelDataFolder)) return false;
		return LoadJsonLevel(GetProjectLevelFolder(), levelNumber, controller);
	}

	private string GetProjectLevelFolder()
	{
		string relative = (_levelDataFolder ?? string.Empty)
			.Replace('/', Path.DirectorySeparatorChar)
			.Replace('\\', Path.DirectorySeparatorChar);
		return Path.IsPathRooted(relative) ? relative : Path.Combine(Application.dataPath, relative);
	}

	private bool EnsureBundle()
	{
		if (_bundle != null) return true;
		string path = Path.Combine(Application.streamingAssetsPath, "Puzzles", _bundleName);
		if (!File.Exists(path))
		{
			Debug.LogError("Built-in puzzle bundle not found: " + path);
			return false;
		}
		_bundle = AssetBundle.LoadFromFile(path);
		if (_bundle == null) Debug.LogError("Could not load puzzle AssetBundle: " + path);
		return _bundle != null;
	}

	private bool LoadAsset(string assetName, int id, GameController controller)
	{
		if (string.IsNullOrEmpty(assetName) || GameProgressStore.IsLevelCompleted(Mathf.Max(1, _defaultLevel))) return false;
		TextAsset asset = _bundle.LoadAsset<TextAsset>(assetName);
		if (asset == null) return false;
		try
		{
			GameController target = controller != null ? controller : GameController.Current;
			if (target == null) return false;
			Puzzle puzzle = PuzzleMessagePackDecoder.Decode(asset.bytes);
			GameData data = GameLevelDataConverter.FromPuzzle(puzzle, id, Mathf.Max(1, _defaultLevel));
			_lastJsonFolder = null;
			target.Initialize(data);
			return target.Data == data;
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			return false;
		}
	}

	private static int ParseLevelNumber(string filePath)
	{
		string name = Path.GetFileNameWithoutExtension(filePath);
		if (name.StartsWith("Level ", StringComparison.OrdinalIgnoreCase) &&
			int.TryParse(name.Substring(6), out int levelNumber)) return levelNumber;
		return -1;
	}

	private void OnDestroy()
	{
		if (_bundle != null) _bundle.Unload(false);
		_bundle = null;
	}
}
