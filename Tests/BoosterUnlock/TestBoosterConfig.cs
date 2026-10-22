#if !UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

// Read the shipped config asset so the harness exercises the same list as the game.
internal static class TestBoosterConfig
{
    public static BoosterUnlockConfig Load()
    {
        string yaml = File.ReadAllText("Assets/00Game/Resources/BoosterUnlockConfig.asset");
        var definitions = new List<BoosterDefinition>();
        foreach (Match match in Regex.Matches(yaml, @"  - _key: (\w+)\r?\n([\s\S]*?)(?=  - _key:|$)"))
        {
            var definition = new BoosterDefinition();
            SetField(definition, "_key", match.Groups[1].Value);
            SetField(definition, "_unlockLevel", int.Parse(ReadValue(match.Value, "_unlockLevel")));
            SetField(definition, "_title", ReadValue(match.Value, "_title"));
            SetField(definition, "_description", ReadValue(match.Value, "_description"));
            SetField(definition, "_iconAddress", ReadValue(match.Value, "_iconAddress"));
            definitions.Add(definition);
        }
        var config = new BoosterUnlockConfig();
        SetField(config, "_boosters", definitions);
        typeof(BoosterUnlockConfig).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(config, null);
        return config;
    }

    private static string ReadValue(string text, string name)
    {
        return Regex.Match(text, @"(?m)^\s*" + name + @": ([^\r\n]+)").Groups[1].Value;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
#endif
