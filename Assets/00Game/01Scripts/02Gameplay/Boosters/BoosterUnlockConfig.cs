using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoosterUnlockConfig", menuName = "LoopMaster/Boosters/Unlock Config")]
public sealed class BoosterUnlockConfig : ScriptableObject
{
    [SerializeField] private List<BoosterDefinition> _boosters = new List<BoosterDefinition>();
    private static BoosterUnlockConfig _current;
    private BoosterUnlockInfo[] _definitions = Array.Empty<BoosterUnlockInfo>();

    private void OnEnable() { RebuildDefinitions(); }
    private void OnValidate() { RebuildDefinitions(); }

    private void RebuildDefinitions()
    {
        var definitions = new List<BoosterUnlockInfo>();
        var keys = new HashSet<string>();
        var levels = new HashSet<int>();
        foreach (BoosterDefinition booster in _boosters)
        {
            if (booster == null || string.IsNullOrWhiteSpace(booster.Key) || booster.UnlockLevel < 1)
            {
                Debug.LogError("BoosterUnlockConfig entries require a key and unlock level greater than zero.", this);
                continue;
            }
            if (!keys.Add(booster.Key) || !levels.Add(booster.UnlockLevel))
            {
                Debug.LogError("BoosterUnlockConfig contains a duplicate key or unlock level: " + booster.Key, this);
                continue;
            }
            definitions.Add(booster.CreateInfo());
        }
        _definitions = definitions.ToArray();
    }

    private static BoosterUnlockConfig LoadConfig()
    {
        if (_current == null) _current = Resources.Load<BoosterUnlockConfig>("BoosterUnlockConfig");
        if (_current == null)
            throw new InvalidOperationException("Missing Resources/BoosterUnlockConfig.asset. Create a booster unlock config asset.");
        return _current;
    }

    public static bool GetInfo(string key, out BoosterUnlockInfo info)
    {
        BoosterUnlockInfo[] definitions = LoadConfig()._definitions;
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].Key != key) continue;
            info = definitions[i];
            return true;
        }
        info = default;
        return false;
    }

    public static BoosterUnlockInfo[] AllDefinitions
    {
        get
        {
            try
            {
                BoosterUnlockConfig config = LoadConfig();
                if (config != null && config._definitions != null)
                {
                    return config._definitions;
                }
                return Array.Empty<BoosterUnlockInfo>();
            }
            catch
            {
                return Array.Empty<BoosterUnlockInfo>();
            }
        }
    }

    public static string GetBoosterForLevel(int levelNumber)
    {
        BoosterUnlockInfo[] definitions = LoadConfig()._definitions;
        for (int i = 0; i < definitions.Length; i++)
            if (definitions[i].UnlockLevel == levelNumber) return definitions[i].Key;
        return null;
    }
}
