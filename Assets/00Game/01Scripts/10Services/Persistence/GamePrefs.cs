using UnityEngine;

public static class GamePrefs
{
#if UNITY_EDITOR
    // Single-level playtests overlay real prefs; isolated campaign profiles use fresh defaults.
    // Migration must also be read-only: even a GetInt can otherwise delete a legacy key.
    public static bool EditorSandbox { get; set; }
    public static bool EditorIsolatedProfile { get; set; }
    public static int EditorCampaignLastLevel { get; set; }
    public static System.Func<int, GameData> EditorCampaignLoader { get; set; }
    public static System.Func<int, bool> EditorCampaignAvailability { get; set; }
    private static readonly System.Collections.Generic.Dictionary<string, object> TestValues = new System.Collections.Generic.Dictionary<string, object>();
    public static void ResetEditorSandbox()
    {
        TestValues.Clear();
        _isDirty = false;
    }
#endif
    public static bool HasKey(string key)
    {
#if UNITY_EDITOR
        if (EditorSandbox && TestValues.TryGetValue(key, out object test)) return test != null;
        if (EditorSandbox && EditorIsolatedProfile) return false;
#endif
        string legacy = GamePrefKeys.Legacy(key);
        return PlayerPrefs.HasKey(key) || (legacy != null && PlayerPrefs.HasKey(legacy));
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
#if UNITY_EDITOR
        if (EditorSandbox)
        {
            if (TestValues.TryGetValue(key, out object test)) return test is int value ? value : defaultValue;
            if (EditorIsolatedProfile) return defaultValue;
            string oldKey = GamePrefKeys.Legacy(key);
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key, defaultValue) : oldKey != null ? PlayerPrefs.GetInt(oldKey, defaultValue) : defaultValue;
        }
#endif
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
#if UNITY_EDITOR
        if (EditorSandbox)
        {
            if (TestValues.TryGetValue(key, out object test)) return test is string value ? value : defaultValue;
            if (EditorIsolatedProfile) return defaultValue;
            string oldKey = GamePrefKeys.Legacy(key);
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, defaultValue) : oldKey != null ? PlayerPrefs.GetString(oldKey, defaultValue) : defaultValue;
        }
#endif
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
#if UNITY_EDITOR
        if (EditorSandbox) { TestValues[key] = value; return; }
#endif
        PlayerPrefs.SetInt(key, value);
        DeleteLegacy(key);
    }

    public static void SetString(string key, string value)
    {
#if UNITY_EDITOR
        if (EditorSandbox) { TestValues[key] = value; return; }
#endif
        PlayerPrefs.SetString(key, value);
        DeleteLegacy(key);
    }

    public static void DeleteKey(string key)
    {
#if UNITY_EDITOR
        if (EditorSandbox) { TestValues[key] = null; return; }
#endif
        PlayerPrefs.DeleteKey(key);
        DeleteLegacy(key);
    }

    private static void DeleteLegacy(string key)
    {
        string legacy = GamePrefKeys.Legacy(key);
        if (legacy != null) PlayerPrefs.DeleteKey(legacy);
    }

    private static bool _isDirty;
    public static bool IsDirty => _isDirty;

    public static void Save()
    {
#if UNITY_EDITOR
        if (EditorSandbox) return;
        if (!Application.isPlaying)
        {
            PlayerPrefs.Save();
            return;
        }
#endif
        _isDirty = true;
        GamePrefsLifecycleFlusher.Ensure();
    }

    public static void SaveImmediate()
    {
#if UNITY_EDITOR
        if (EditorSandbox) return;
#endif
        PlayerPrefs.Save();
        _isDirty = false;
    }
}

