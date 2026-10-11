using UnityEngine;

public static class GamePrefs
{
    public static bool HasKey(string key)
    {
        string legacy = GamePrefKeys.Legacy(key);
        return PlayerPrefs.HasKey(key) || (legacy != null && PlayerPrefs.HasKey(legacy));
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        string legacy = GamePrefKeys.Legacy(key);
        if (legacy != null && PlayerPrefs.HasKey(legacy))
        {
            if (!PlayerPrefs.HasKey(key))
                PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(legacy, defaultValue));
            FinishMigration(legacy);
        }
        return PlayerPrefs.GetInt(key, defaultValue);
    }

    public static string GetString(string key, string defaultValue = "")
    {
        string legacy = GamePrefKeys.Legacy(key);
        if (legacy != null && PlayerPrefs.HasKey(legacy))
        {
            if (!PlayerPrefs.HasKey(key))
                PlayerPrefs.SetString(key, PlayerPrefs.GetString(legacy, defaultValue));
            FinishMigration(legacy);
        }
        return PlayerPrefs.GetString(key, defaultValue);
    }

    private static void FinishMigration(string legacy)
    {
        // Persist the destination before removing the source.
        PlayerPrefs.Save();
        PlayerPrefs.DeleteKey(legacy);
        PlayerPrefs.Save();
    }

    public static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        DeleteLegacy(key);
    }

    public static void SetString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        DeleteLegacy(key);
    }

    public static void DeleteKey(string key)
    {
        PlayerPrefs.DeleteKey(key);
        DeleteLegacy(key);
    }

    private static void DeleteLegacy(string key)
    {
        string legacy = GamePrefKeys.Legacy(key);
        if (legacy != null) PlayerPrefs.DeleteKey(legacy);
    }

    public static void Save() => PlayerPrefs.Save();
}

