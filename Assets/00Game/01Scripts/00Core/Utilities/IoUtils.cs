using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class IoUtils
{
	private static string GetFilePath(string fileName)
	{
		if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
		{
			fileName += ".json";
		}
		return Path.Combine(Application.persistentDataPath, fileName);
	}

	public static T ReadJsonFile<T>(string fileName)
	{
		try
		{
			string path = GetFilePath(fileName);
			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				if (!string.IsNullOrEmpty(json))
				{
					return JsonUtility.FromJson<T>(json);
				}
			}
			else
			{
				string prefJson = GamePrefs.GetString(GamePrefKeys.JsonFile(fileName), string.Empty);
				if (!string.IsNullOrEmpty(prefJson))
				{
					return JsonUtility.FromJson<T>(prefJson);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[IoUtils] Failed to read {fileName}: {ex.Message}");
		}
		return default;
	}

	public static async UniTask WriteJsonFileAsync(string fileName, object data)
	{
		await UniTask.Yield();
		WriteJsonFile(fileName, data);
	}

	public static void WriteJsonFile(string fileName, object data)
	{
		try
		{
			if (data == null) return;
			string json = JsonUtility.ToJson(data);
			string path = GetFilePath(fileName);
			File.WriteAllText(path, json);
			GamePrefs.SetString(GamePrefKeys.JsonFile(fileName), json);
			GamePrefs.Save();
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[IoUtils] Failed to write {fileName}: {ex.Message}");
		}
	}

	public static bool Exists(string fileName)
	{
		string path = GetFilePath(fileName);
		return File.Exists(path) || GamePrefs.HasKey(GamePrefKeys.JsonFile(fileName));
	}

	public static void DeleteJsonFile(string fileName)
	{
		try
		{
			string path = GetFilePath(fileName);
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			GamePrefs.DeleteKey(GamePrefKeys.JsonFile(fileName));
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[IoUtils] Failed to delete {fileName}: {ex.Message}");
		}
	}
}