public static class BoosterUnlockProgress
{
    public static bool HasSeenIntro(string key)
    {
        return BoosterUnlockConfig.TryGetInfo(key, out BoosterUnlockInfo info) &&
            GamePrefs.GetInt(info.IntroPreferenceKey) == 1;
    }

    public static void MarkIntroSeen(string key)
    {
        if (!BoosterUnlockConfig.TryGetInfo(key, out BoosterUnlockInfo info) || HasSeenIntro(key)) return;
        // Persist both together; replaying the intro can never replenish a spent use.
        GamePrefs.SetInt(info.IntroPreferenceKey, 1);
        GamePrefs.SetInt(info.FreeUsePreferenceKey, 1);
        GamePrefs.Save();
    }

    public static string GetPendingUnlockKey(int levelNumber)
    {
        string key = BoosterUnlockConfig.GetBoosterForLevel(levelNumber);
        if (key == null || HasSeenIntro(key)) return null;
        return key;
    }

    public static bool IsFirstUseFree(string key)
    {
        if (!BoosterUnlockConfig.TryGetInfo(key, out BoosterUnlockInfo info)) return false;
        return GamePrefs.GetInt(info.IntroPreferenceKey) == 1 && GamePrefs.GetInt(info.FreeUsePreferenceKey) == 1;
    }

    public static void ConsumeFirstUseFree(string key)
    {
        if (!BoosterUnlockConfig.TryGetInfo(key, out BoosterUnlockInfo info)) return;
        GamePrefs.SetInt(info.FreeUsePreferenceKey, 0);
        GamePrefs.Save();
    }

    public static void ResetBooster(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (BoosterUnlockConfig.TryGetInfo(key, out BoosterUnlockInfo info))
        {
            GamePrefs.DeleteKey(info.IntroPreferenceKey);
            GamePrefs.DeleteKey(info.FreeUsePreferenceKey);
        }
        else
        {
            GamePrefs.DeleteKey("BoosterIntro_" + key);
            GamePrefs.DeleteKey("BoosterFirstUseFree_" + key);
        }
    }

    public static void ResetAll()
    {
        string[] defaultKeys = { InventoryKeys.TOOL_UNDO, InventoryKeys.TOOL_SHUFFLE, InventoryKeys.TOOL_PATH };
        for (int i = 0; i < defaultKeys.Length; i++)
        {
            ResetBooster(defaultKeys[i]);
        }
        BoosterUnlockInfo[] definitions = BoosterUnlockConfig.AllDefinitions;
        if (definitions != null)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                ResetBooster(definitions[i].Key);
            }
        }
        GamePrefs.Save();
    }
}
