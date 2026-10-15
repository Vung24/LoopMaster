using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class LoseItemRegistry
{
    private static readonly Dictionary<string, ILoseItemProvider> _providers = new Dictionary<string, ILoseItemProvider>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        _providers.Clear();
    }

    public static void Register(ILoseItemProvider provider)
    {
        if (provider == null || string.IsNullOrEmpty(provider.Id)) return;
        _providers[provider.Id] = provider;
    }

    public static void Unregister(string id)
    {
        if (!string.IsNullOrEmpty(id)) _providers.Remove(id);
    }

    public static bool HasProvider(string id) => !string.IsNullOrEmpty(id) && _providers.ContainsKey(id);

    public static IReadOnlyList<ILoseItemProvider> GetOrderedProviders()
    {
        return _providers.Values
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .ToList();
    }
}
