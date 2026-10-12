using System;
using System.IO;
using UnityEngine;

public class BuiltinPuzzleLoader : MonoBehaviour, ILevelDataSource
{
	[SerializeField] private string _bundleName = "puzzle";
	[SerializeField] private int _defaultLevel = 1;
	[SerializeField] private string _levelDataFolder = "00Game/05LevelData/LevelData";
	private AssetBundle _bundle;
	private bool _loading;
	private string _lastJsonFolder;
	private BuiltinLevelCatalog _catalog;
	private string _cachedFolderSetting;
	private string _cachedProjectFolder;
	private int _availabilityFrame = -1;
	private int _availabilityLevel;
	private bool _availabilityResult;
	private string _availabilityJsonFolder;
	private string _availabilityFolderSetting;
	private BuiltinLevelCatalog Catalog => _catalog != null ? _catalog :
		(_catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName));

	public string LastJsonFolder => _lastJsonFolder;

	public bool LoadDefault(GameController controller)
	{
		return !GameProgressStore.IsCampaignComplete && LoadExactLevel(GameProgressStore.NextPlayableLevel, controller);
	}

	public bool LoadLevel(int levelNumber, GameController controller = null)
	{
		return LoadExactLevel(levelNumber, controller);
	}

	public bool LoadExactLevel(int levelNumber, GameController controller = null)
	{
		// Compatibility entry point; navigation uses the data-only factory below.
		return TryCreateExactLevel(levelNumber, out GameData data) && InitializePrepared(data, controller);
	}

	public bool TryCreateExactLevel(int levelNumber, out GameData data)
	{
		data = null;
		if (_loading || levelNumber < 1 || GameProgressStore.IsLevelCompleted(levelNumber)) return false;
		_loading = true;
		GameData previous = GameData.Current;
		try
		{
			if (!string.IsNullOrEmpty(_lastJsonFolder) && ReadJsonLevel(_lastJsonFolder, levelNumber, out data)) return true;
			if (!string.IsNullOrEmpty(_levelDataFolder))
			{
				string projectFolder = GetProjectLevelFolder();
				if (!string.Equals(projectFolder, _lastJsonFolder, StringComparison.Ordinal) &&
					ReadJsonLevel(projectFolder, levelNumber, out data)) return true;
			}
			return ReadPackagedLevel(levelNumber, out data);
		}
		finally
		{
			GameData.SetCurrent(previous);
			_loading = false;
		}
	}

	public bool HasExactLevel(int levelNumber)
	{
		if (levelNumber < 1) return false;
		int frame = Time.frameCount;
		if (Application.isPlaying && _availabilityFrame == frame && _availabilityLevel == levelNumber &&
			_availabilityJsonFolder == _lastJsonFolder && _availabilityFolderSetting == _levelDataFolder)
			return _availabilityResult;
		// Catalog availability needs no file-system probe. Actual loads still honor JSON overrides first.
		bool available = Catalog != null && Catalog.HasLevel(levelNumber);
		if (!available)
		{
			string file = "Level " + levelNumber + ".json";
			available = (!string.IsNullOrEmpty(_lastJsonFolder) && File.Exists(Path.Combine(_lastJsonFolder, file))) ||
				File.Exists(Path.Combine(GetProjectLevelFolder(), file));
		}
		_availabilityFrame = frame;
		_availabilityLevel = levelNumber;
		_availabilityJsonFolder = _lastJsonFolder;
		_availabilityFolderSetting = _levelDataFolder;
		_availabilityResult = available;
		return available;
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

	public bool LoadJsonLevel(string folderPath, int levelNumber, GameController controller = null)
	{
		if (string.IsNullOrEmpty(folderPath) || levelNumber < 1 || GameProgressStore.IsLevelCompleted(levelNumber)) return false;
		return LoadJsonFile(Path.Combine(folderPath, "Level " + levelNumber + ".json"), controller);
	}

	public bool LoadJsonFile(string filePath, GameController controller = null)
	{
		if (_loading || string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
		_loading = true;
		GameData previous = GameData.Current;
		GameData data;
		try
		{
			if (!ReadJsonFile(filePath, out data)) return false;
		}
		finally { GameData.SetCurrent(previous); _loading = false; }
		return InitializePrepared(data, controller);
	}

	private bool ReadJsonLevel(string folder, int level, out GameData data) =>
		ReadJsonFile(Path.Combine(folder, "Level " + level + ".json"), out data);

	private bool ReadJsonFile(string path, out GameData data)
	{
		data = null;
		if (!File.Exists(path)) return false;
		try
		{
			if (!CreateJsonData(File.ReadAllText(path), ParseLevelNumber(path), out data)) return false;
			_lastJsonFolder = Path.GetDirectoryName(Path.GetFullPath(path));
			_availabilityFrame = -1;
			return true;
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			return false;
		}
	}

	private bool ReadPackagedLevel(int levelNumber, out GameData data)
	{
		data = null;
		if (Catalog == null || !Catalog.HasLevel(levelNumber)) return false;
		TextAsset asset = null;
		try
		{
			asset = Resources.Load<TextAsset>($"{BuiltinLevelCatalog.ResourceFolder}/Level {levelNumber}");
			if (asset == null) return false;
			if (!CreateJsonData(asset.text, levelNumber, out data)) return false;
			_lastJsonFolder = null;
			_availabilityFrame = -1;
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
		}
	}

	private static bool CreateJsonData(string json, int levelNumber, out GameData data)
	{
		data = null;
		if (GameProgressStore.IsLevelCompleted(levelNumber)) return false;
		data = GameLevelDataConverter.FromJson(json, levelNumber);
		if (!GameProgressStore.IsLevelCompleted(data.GetPuzzleActualLevel())) return true;
		data = null;
		return false;
	}

	private static bool InitializePrepared(GameData data, GameController controller)
	{
		GameData previous = GameData.Current;
		GameController target = controller != null ? controller : GameController.Current;
		if (target == null)
		{
			GameData.SetCurrent(previous);
			Debug.LogError("No GameController is available to initialize the prepared level.");
			return false;
		}
		try
		{
			target.Initialize(data);
			return target.Data == data;
		}
		catch (Exception exception)
		{
			GameData.SetCurrent(previous);
			Debug.LogException(exception);
			return false;
		}
	}

	private string GetProjectLevelFolder()
	{
		if (_cachedProjectFolder != null && _cachedFolderSetting == _levelDataFolder) return _cachedProjectFolder;
		string relative = (_levelDataFolder ?? string.Empty)
			.Replace('/', Path.DirectorySeparatorChar)
			.Replace('\\', Path.DirectorySeparatorChar);
		_cachedFolderSetting = _levelDataFolder;
		_cachedProjectFolder = Path.GetFullPath(Path.IsPathRooted(relative) ? relative : Path.Combine(Application.dataPath, relative));
		return _cachedProjectFolder;
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

	private void OnValidate() => _availabilityFrame = -1;
}
