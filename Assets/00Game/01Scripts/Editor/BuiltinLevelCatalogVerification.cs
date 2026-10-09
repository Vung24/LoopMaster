using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class BuiltinLevelCatalogVerification
{
    private const string Source = "Assets/00Game/05LevelData/LevelData";
    private const string Packaged = "Assets/00Game/Resources/LevelData";

    public static void Run()
    {
        string root = Path.GetFullPath(".").Replace('\\', '/');
        Require(root.EndsWith("/Temp/LevelCatalogVerification", StringComparison.OrdinalIgnoreCase) &&
            File.Exists("isolated-verification.txt"), "must run in isolated verification project");

        BuiltinLevelCatalogBuild.RefreshCatalog();
        var catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
        Require(catalog != null && catalog.LevelIds.SequenceEqual(Enumerable.Range(1, 300)), "300 sorted IDs");
        Require(new SerializedObject(catalog).FindProperty("Levels") == null, "no serialized TextAsset references");
        Require(!catalog.HasLevel(0) && !catalog.HasLevel(301) && catalog.HasLevel(1) && catalog.HasLevel(300), "ID boundaries");
        Require(catalog.GetHighestLevelAtOrBelow(0) == 0 && catalog.GetHighestLevelAtOrBelow(150) == 150 &&
            catalog.GetHighestLevelAtOrBelow(int.MaxValue) == 300, "highest level boundaries");
        foreach (string source in Directory.GetFiles(Source, "*.json"))
            Require(File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(Packaged + "/" + Path.GetFileName(source))), "packaged bytes match source");

        foreach (int id in new[] { 1, 300 })
        {
            var asset = Resources.Load<TextAsset>($"LevelData/Level {id}");
            Require(asset != null && asset.text == File.ReadAllText(Source + $"/Level {id}.json"), "on-demand resource contents");
            Resources.UnloadAsset(asset);
            var reloaded = Resources.Load<TextAsset>($"LevelData/Level {id}");
            Require(reloaded != null && reloaded.text == File.ReadAllText(Source + $"/Level {id}.json"), "resource reload after unload");
            Resources.UnloadAsset(reloaded);
        }
        Require(Resources.Load<TextAsset>("LevelData/Level 301") == null, "missing resource");

        DateTime timestamp = File.GetLastWriteTimeUtc(Packaged + "/Level 1.json");
        BuiltinLevelCatalogBuild.RefreshCatalog();
        Require(File.GetLastWriteTimeUtc(Packaged + "/Level 1.json") == timestamp, "unchanged JSON is not rewritten");

        string first = Source + "/Level 1.json";
        byte[] original = File.ReadAllBytes(first);
        try
        {
            File.AppendAllText(first, "\n");
            BuiltinLevelCatalogBuild.RefreshCatalog();
            Require(File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(Packaged + "/Level 1.json")), "source edit synchronized");
        }
        finally { File.WriteAllBytes(first, original); }

        string last = Source + "/Level 300.json";
        byte[] lastBytes = File.ReadAllBytes(last);
        string lastMeta = File.ReadAllText(Packaged + "/Level 300.json.meta");
        try
        {
            File.Delete(last);
            BuiltinLevelCatalogBuild.RefreshCatalog();
            Require(!File.Exists(Packaged + "/Level 300.json") && !File.Exists(Packaged + "/Level 300.json.meta") &&
                !catalog.HasLevel(300) && catalog.GetHighestLevelAtOrBelow(int.MaxValue) == 299, "stale level and metadata removed");
        }
        finally
        {
            File.WriteAllBytes(last, lastBytes);
            File.WriteAllText(Packaged + "/Level 300.json.meta", lastMeta);
        }

        ExpectInvalid("Bad name.json", "Invalid level file name");
        ExpectInvalid("Level 0.json", "Invalid level file name");
        ExpectInvalid("Level 01.json", null);
        ExpectInvalid("Level 0301.json", "Expected canonical file name");
        BuiltinLevelCatalogBuild.RefreshCatalog();
        Require(catalog.LevelIds.SequenceEqual(Enumerable.Range(1, 300)), "final catalog restored");
        Debug.Log("Builtin level catalog verification passed: 300 IDs, resource load/unload, sync, deletion, invalid names.");
    }

    private static void ExpectInvalid(string name, string message)
    {
        string path = Source + "/" + name;
        byte[] before = File.ReadAllBytes(Packaged + "/Level 1.json");
        try
        {
            File.WriteAllText(path, "{}");
            bool failed = false;
            try { BuiltinLevelCatalogBuild.RefreshCatalog(); }
            catch (BuildFailedException exception) { failed = message == null || exception.Message.Contains(message); }
            Require(failed, "reject " + name);
            Require(before.SequenceEqual(File.ReadAllBytes(Packaged + "/Level 1.json")), "invalid source does not modify packaged data");
        }
        finally { AssetDatabase.DeleteAsset(path); if (File.Exists(path)) File.Delete(path); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Catalog verification failed: " + message);
    }
}
