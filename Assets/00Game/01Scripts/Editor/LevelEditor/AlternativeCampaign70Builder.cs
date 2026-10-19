#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using A = LoopLevelAuthoring;

public static class AlternativeCampaign70Builder
{
    public static string ProjectRoot = ".";
    public static string LevelFolder => Path.Combine(ProjectRoot, "Assets/00Game/05LevelData/AlternativeCampaign_70");
    public static string ReportFolder => Path.Combine(ProjectRoot, "docs/superpowers/verification/alternative-campaign-70");
    public const int Version = 2;
    private static IEnumerator _job;
    private static string _status;
    private static readonly int[] Samples = { 1, 2, 3, 4, 7, 9, 11, 15, 23, 29, 37, 41, 49, 53, 56, 59, 70 };

    // Number, boxes (including special boxes), normal colors, road, singles, longs,
    // secret %, color locks, adjacent locks, ice cubes, ice count, groups, group size,
    // linked pair, surprise colors, hard label, road capacity, mixing mode (0=generator, 1=layered).
    // Existing layout API roads:
    // rounded rectangle=0, oval=1, circle=2, straight=3, U=4, S=5, winding=6.
    private const string Curriculum = @"1,2,1,0,0,0,0,0,0,0,0,0,0,0,0,0,4,0
2,3,2,0,0,0,0,0,0,0,0,0,0,0,0,0,6,0
3,4,3,1,0,0,0,0,0,0,0,0,0,0,0,0,7,0
4,5,4,0,0,0,0,0,0,0,0,0,0,0,0,0,6,0
5,6,5,1,0,0,0,0,0,0,0,0,0,0,0,0,5,0
6,7,6,0,0,0,15,0,0,0,0,0,0,0,0,0,7,0
7,8,8,0,0,0,20,0,0,0,0,0,0,0,0,0,6,0
8,7,7,0,0,0,20,0,0,0,0,0,0,0,0,0,5,0
9,9,8,0,0,0,20,0,0,0,0,0,0,0,0,0,4,0
10,9,9,1,0,0,25,0,0,0,0,0,0,0,0,1,4,0
11,8,8,3,0,0,0,0,0,0,0,0,0,0,0,0,7,0
12,8,7,3,0,0,0,0,0,0,0,0,0,0,0,0,5,0
13,10,10,5,0,0,0,0,0,0,0,0,0,0,0,0,6,1
14,9,8,5,0,0,20,0,0,0,0,0,0,0,0,0,7,0
15,8,6,0,1,0,0,0,0,0,0,0,0,0,0,0,6,0
16,9,8,0,1,0,0,0,0,0,0,0,0,0,0,0,5,0
17,8,6,5,1,0,0,0,0,0,0,0,0,0,0,0,7,0
18,10,8,4,1,0,0,0,0,0,0,0,0,0,0,0,5,0
19,9,9,6,0,0,0,0,0,0,0,0,0,0,0,0,6,0
20,12,10,4,1,0,20,0,0,0,0,0,0,0,0,1,4,0
21,8,8,5,0,0,0,0,0,0,0,0,0,0,0,0,8,0
22,10,9,6,0,0,0,0,0,0,0,0,0,0,0,0,5,0
23,8,7,0,0,0,0,1,0,0,0,0,0,0,0,0,6,0
24,9,9,5,0,0,0,2,0,0,0,0,0,0,0,0,7,0
25,10,10,1,0,0,20,2,0,0,0,0,0,0,0,0,5,0
26,8,8,0,0,0,0,2,0,0,0,0,0,0,0,0,6,1
27,10,9,4,0,0,0,2,0,0,0,0,0,0,0,0,5,0
28,9,8,0,0,0,0,0,0,0,0,0,0,0,0,0,8,0
29,8,7,1,0,0,0,0,1,0,0,0,0,0,0,0,6,0
30,10,10,1,0,0,0,0,2,0,0,0,0,0,0,1,4,1
31,9,9,5,0,0,0,1,1,0,0,0,0,0,0,0,5,1
32,10,9,3,0,0,0,0,2,0,0,0,0,0,0,0,6,0
33,9,8,0,0,0,0,0,2,0,0,0,0,0,0,0,5,0
34,10,10,0,0,0,0,2,1,0,0,0,0,0,0,0,7,1
35,12,12,2,0,0,15,2,1,0,0,0,0,0,0,1,4,1
36,8,6,0,0,0,0,0,0,0,0,0,0,0,0,0,8,0
37,9,8,0,0,0,0,0,0,2,2,0,0,0,0,0,7,0
38,10,10,1,0,0,0,0,0,2,4,0,0,0,0,0,5,1
39,9,7,5,1,0,0,0,0,2,2,0,0,0,0,0,6,0
40,10,9,0,0,0,20,0,0,2,2,0,0,0,0,0,5,0
41,10,7,0,0,2,0,0,0,0,0,0,0,0,0,0,6,0
42,10,5,0,0,4,0,0,0,0,0,0,0,0,0,0,5,0
43,12,10,4,0,2,0,1,0,0,0,0,0,0,0,0,7,1
44,10,7,0,0,2,0,0,0,2,2,0,0,0,0,0,5,0
45,12,9,6,0,2,0,0,0,2,2,0,0,0,0,1,4,0
46,9,7,0,0,0,0,0,0,0,0,0,0,0,0,0,8,0
47,10,10,0,0,0,0,0,1,2,2,0,0,0,0,0,6,1
48,12,8,0,1,2,20,0,0,0,0,0,0,0,0,0,5,0
49,9,8,1,0,0,0,0,0,0,0,0,0,1,0,0,6,0
50,10,9,1,0,0,0,0,0,0,0,0,0,1,0,0,5,0
51,9,9,1,0,0,20,0,0,0,0,0,0,1,0,0,7,0
52,10,10,5,0,0,0,1,0,0,0,0,0,1,0,0,5,1
53,10,9,0,0,0,0,0,0,0,0,1,2,0,0,0,6,0
54,12,12,0,0,0,0,0,0,0,0,1,3,0,0,0,5,1
55,10,10,4,0,0,20,0,0,0,0,1,2,0,0,0,7,1
56,9,9,0,0,0,0,0,0,0,0,0,0,0,1,0,6,0
57,10,10,0,0,0,0,0,0,0,0,0,0,0,2,0,5,1
58,12,10,5,1,0,0,0,0,0,0,0,0,0,1,0,7,0
59,12,11,0,0,0,0,0,0,0,0,1,2,1,0,0,5,0
60,12,12,2,0,0,0,0,0,0,0,1,2,1,1,1,4,1
61,12,11,4,1,0,0,0,0,0,0,0,0,1,0,1,4,0
62,12,12,0,0,0,20,0,0,0,0,2,2,0,0,1,4,1
63,12,10,0,0,2,30,2,0,0,0,0,0,0,0,1,4,1
64,12,12,1,0,0,0,0,0,2,2,0,0,1,0,1,4,1
65,12,11,0,1,0,25,2,0,0,0,0,0,0,0,1,4,0
66,12,12,6,0,0,0,0,0,0,0,1,2,1,0,1,4,1
67,12,12,0,0,0,0,0,0,3,2,0,0,0,2,1,4,1
68,13,12,1,0,0,0,1,1,0,0,0,0,1,0,2,4,0
69,12,10,5,2,0,0,0,0,0,0,1,2,0,0,1,4,0
70,13,10,2,1,2,20,1,1,2,2,1,2,1,1,2,4,0";

    public sealed class Profile
    {
        public int Level, Boxes, Colors, Road, Singles, Longs, Secrets, ColorLocks,
            Adjacent, Ice, IceCount, Groups, GroupSize, Pair, Surprise, Hard;
        public int Capacity, GeometryReference, MixingMode;
        public string MixingRecipe = "generator_scramble";
        public static Profile Get(int level)
        {
            string[] rows = Curriculum.Split('\n');
            if (level < 1 || level > rows.Length) throw new ArgumentOutOfRangeException(nameof(level));
            string[] fields = rows[level - 1].Trim().Split(',');
            int[] v = System.Array.ConvertAll(fields, int.Parse);
            var profile = new Profile { Level=v[0], Boxes=v[1], Colors=v[2], Road=v[3], Singles=v[4], Longs=v[5], Secrets=v[6],
                ColorLocks=v[7], Adjacent=v[8], Ice=v[9], IceCount=v[10], Groups=v[11], GroupSize=v[12], Pair=v[13], Surprise=v[14], Hard=v[15] };
            profile.Capacity = v[16];
            profile.MixingMode = v[17];
            return profile;
        }
    }

    [MenuItem("Tools/LoopMaster/Alternative Campaign 70/Build representative samples")]
    public static void BuildSamples() { Start(Samples); }

    [MenuItem("Tools/LoopMaster/Tạo 70 Màn Chơi Mới (Alternative Campaign)")]
    public static void BuildAll()
    {
        RequireSampleAcceptance();
        int[] levels = new int[70];
        for (int i = 0; i < levels.Length; i++) levels[i] = i + 1;
        Start(levels);
    }

    public static IEnumerator CreateCampaignJob()
    {
        RequireSampleAcceptance();
        int[] levels=new int[70];
        for(int i=0;i<levels.Length;i++) levels[i]=i+1;
        return Build(levels);
    }

    [MenuItem("Tools/LoopMaster/Alternative Campaign 70/Record automated sample baselines")]
    public static void RecordSampleBaselines()
    {
        var stages=new JArray();
        for(int stage=0;stage<6;stage++)
        {
            int min=int.MaxValue, max=0, peak=0;
            var samples=new JArray();
            foreach(int level in Samples)
            {
                if(Stage(level)!=stage) continue;
                JObject report=JObject.Parse(File.ReadAllText(ReportPath(level)));
                string json=File.ReadAllText(Path.Combine(LevelFolder,"Level "+level+".json"));
                if((string)report["unity_replay"]!="passed" || (string)report["sha256"]!=Hash(json))
                    throw new InvalidOperationException("Sample "+level+" requires a current Unity replay.");
                int clicks=report.Value<int>("reference_click_count");
                min=Math.Min(min,clicks); max=Math.Max(max,clicks);
                peak=Math.Max(peak,report.Value<int>("runtime_peak_groups"));
                samples.Add(new JObject { ["level"]=level, ["sha256"]=report["sha256"].DeepClone() });
            }
            stages.Add(new JObject { ["stage"]=stage+1, ["samples"]=samples,
                ["observed_reference_clicks_min"]=min, ["observed_reference_clicks_max"]=max,
                ["reference_clicks_target_min"]=Math.Max(1,min/2), ["reference_clicks_target_max"]=max*2,
                ["observed_peak_groups"]=peak, ["human_completion_seconds_target"]=JValue.CreateNull(),
                ["human_restart_target"]=JValue.CreateNull() });
        }
        var root=new JObject { ["status"]="provisional_automated_baseline", ["stages"]=stages,
            ["limitations"]="Click bands are provisional production targets based on replayed samples. Human difficulty, completion time and restart rates remain pending and block final acceptance." };
        File.WriteAllText(Path.Combine(ReportFolder,"balance-targets.json"),root.ToString(Formatting.Indented));
    }

    private static int Stage(int level)
    {
        if(level<=10) return 0;
        if(level<=22) return 1;
        if(level<=36) return 2;
        if(level<=48) return 3;
        if(level<=60) return 4;
        return 5;
    }

    public static List<string> GetMechanics(Profile profile)
    {
        var mechanics = new List<string>();
        if (profile.Singles > 0) mechanics.Add("single");
        if (profile.Longs > 0) mechanics.Add("long");
        if (profile.Secrets > 0) mechanics.Add("secret");
        if (profile.ColorLocks > 0) mechanics.Add("color_lock");
        if (profile.Adjacent > 0) mechanics.Add("adjacent_lock");
        if (profile.Ice > 0) mechanics.Add("ice");
        if (profile.Groups > 0) mechanics.Add("group_door");
        if (profile.Pair > 0) mechanics.Add("linked_pair");
        if (profile.Surprise > 0) mechanics.Add("surprise");
        return mechanics;
    }

    private static void RequireSampleAcceptance()
    {
        foreach (int level in Samples)
        {
            string path = ReportPath(level);
            if (!File.Exists(path)) throw new InvalidOperationException("Build and replay representative samples first.");
            JObject report = JObject.Parse(File.ReadAllText(path));
            string jsonPath = Path.Combine(LevelFolder, "Level " + level + ".json");
            if (report.Value<int>("builder_version") != Version || (string)report["unity_replay"] != "passed" || !File.Exists(jsonPath) ||
                (string)report["sha256"] != Hash(File.ReadAllText(jsonPath)))
                throw new InvalidOperationException("Sample " + level + " needs a Unity replay matching its current JSON.");
        }
        if (!File.Exists(Path.Combine(ReportFolder, "balance-targets.json")))
            throw new InvalidOperationException("Record measured sample balance targets before building all stages.");
        JObject targets=JObject.Parse(File.ReadAllText(Path.Combine(ReportFolder,"balance-targets.json")));
        if((string)targets["status"]!="provisional_automated_baseline" || !(targets["stages"] is JArray stages) || stages.Count!=6)
            throw new InvalidOperationException("Sample baseline is missing or invalid.");
        foreach(JObject stage in stages)
        foreach(JObject sample in (JArray)stage["samples"])
            if((string)sample["sha256"]!=Hash(File.ReadAllText(Path.Combine(LevelFolder,"Level "+sample.Value<int>("level")+".json"))))
                throw new InvalidOperationException("Sample baselines are stale; measure the updated samples again.");
    }

    private static void Start(int[] levels)
    {
        if (_job != null || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop the active build or Play Mode first.");
        _job = Build(levels);
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Cancel;
        EditorApplication.playModeStateChanged += CancelForPlayMode;
    }

    private static void Tick()
    {
        try
        {
            if (EditorUtility.DisplayCancelableProgressBar("Alternative Campaign 70", _status ?? "Preparing", 0)) { Cancel(); return; }
            if (!_job.MoveNext()) Cancel();
        }
        catch (Exception error) { Cancel(); UnityEngine.Debug.LogException(error); }
    }

    private static void Cancel()
    {
        (_job as IDisposable)?.Dispose(); _job = null;
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
        EditorApplication.playModeStateChanged -= CancelForPlayMode;
        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();
    }

    private static void CancelForPlayMode(PlayModeStateChange state)
    { if(state==PlayModeStateChange.ExitingEditMode) Cancel(); }

    public static IEnumerator Build(int[] levels)
    {
        Directory.CreateDirectory(LevelFolder);
        Directory.CreateDirectory(ReportFolder);
        EnsureMeta(LevelFolder, true);
        foreach (int level in levels)
        {
            Profile p = Profile.Get(level);
            string lastError = "No candidate";
            bool accepted = false;
            int baseSeed=701019+level*1009;
            JObject savedReport=null;
            if(File.Exists(ReportPath(level)))
            {
                savedReport=JObject.Parse(File.ReadAllText(ReportPath(level)));
                if((string)savedReport["unity_replay"]=="failed")
                {
                    baseSeed=savedReport.Value<int>("seed")+7919;
                    string failures=Path.Combine(ReportFolder,"failures");
                    Directory.CreateDirectory(failures);
                    string failedJson=File.ReadAllText(Path.Combine(LevelFolder,"Level "+level+".json"));
                    string prefix=Path.Combine(failures,"Level "+level+"-"+Hash(failedJson));
                    File.WriteAllText(prefix+".report.json",savedReport.ToString(Formatting.Indented));
                    File.Copy(Path.Combine(LevelFolder,"Level "+level+".json"),prefix+".gameplay.json",true);
                }
            }
            for (int attempt = 0; attempt < 48; attempt++)
            {
                int seed = baseSeed + attempt * 7919;
                JObject previousReport = null;
                if (attempt == 0 && File.Exists(ReportPath(level)))
                {
                    previousReport = JObject.Parse(File.ReadAllText(ReportPath(level)));
                    if (previousReport["seed"] != null && (string)previousReport["unity_replay"]!="failed") seed = previousReport.Value<int>("seed");
                }
                _status = "Level " + level + ", attempt " + (attempt + 1);
                UnityEngine.Debug.Log("Alternative campaign: " + _status);
                JObject root = null;
                try { root = CreateCandidate(p, seed); }
                catch (Exception error) { lastError = error.Message; }
                if (root == null) { yield return null; continue; }
                string json = root.ToString(Formatting.Indented);
                GameData data = LoopLevelGenerator.DecodeDetached(A.Parse(json));
                data.DifferentColorSliceSpacing = 1;
                if (data.IsGameOver()) { lastError = "Initial board is already won"; continue; }
                var solver = new DebugLevelSolver(data, 20000);
                List<int> clicks = null;
                if (previousReport?["solution_clicks"] is JArray previousClicks)
                {
                    var cached = new List<int>();
                    foreach (JToken value in previousClicks) cached.Add(value.Value<int>());
                    if (Replay(json, cached, out _)) clicks = cached;
                }
                IEnumerator search = solver.Search();
                var watch = Stopwatch.StartNew();
                try
                {
                    while (clicks == null && watch.Elapsed.TotalSeconds < 8 && LoopLevelGenerator.InSimulation(data, search.MoveNext))
                        yield return null;
                }
                finally { (search as IDisposable)?.Dispose(); }
                if (clicks == null && (solver.Solution == null || solver.Solution.Count == 0))
                { lastError = solver.Failure ?? "Search time budget exhausted"; continue; }
                if (clicks == null)
                {
                    clicks = new List<int>();
                    foreach (DebugLevelSolver.Step step in solver.Solution) clicks.Add(step.BoxIndex);
                }
                int peak;
                if (!Replay(json, clicks, out peak)) { lastError = "Final JSON replay failed"; continue; }
                if (p.Level >= 5 && clicks.Count < p.Boxes)
                { lastError = "Reference solution is too short for this chapter"; continue; }
                List<string> warnings;
                List<string> errors = A.Validate(root, out warnings);
                if (errors.Count != 0) { lastError = string.Join("\n", errors); continue; }
                if (savedReport != null && (string)savedReport["unity_replay"] == "failed" &&
                    (string)savedReport["sha256"] == Hash(json))
                { lastError = "Candidate matches a failed Unity replay"; continue; }
                WriteAtomic(Path.Combine(LevelFolder, "Level " + level + ".json"), json);
                EnsureMeta(Path.Combine(LevelFolder, "Level " + level + ".json"), false);
                JObject report = CreateReport(p, root, seed, clicks, peak, warnings);
                if (previousReport != null && (string)previousReport["sha256"] == (string)report["sha256"])
                {
                    foreach (string key in new[] { "unity_replay", "runtime_peak_groups", "automated_replay_wall_seconds",
                        "runtime_time_scale", "runtime_context", "runtime_solution_clicks", "runtime_wait_policy",
                        "transition_timing_differences", "runtime_legal_no_effect_clicks", "capacity_display_review",
                        "capacity_display_maximum", "visual_review", "lifecycle_review", "lifecycle_scope" })
                        if (previousReport[key] != null) report[key] = previousReport[key].DeepClone();
                }
                WriteAtomic(ReportPath(level), report.ToString(Formatting.Indented));
                accepted = true;
                UnityEngine.Debug.Log("Alternative campaign level " + level + ": simulation replay passed, " + clicks.Count + " clicks; Unity replay pending.");
                break;
            }
            if (!accepted)
            {
                File.WriteAllText(ReportPath(level), new JObject { ["level"]=level, ["simulation_replay"]="unverified", ["error"]=lastError }.ToString(Formatting.Indented));
                throw new InvalidOperationException("Level " + level + " not exported: " + lastError);
            }
            yield return null;
        }
    }

    public static JObject CreateCandidate(Profile p, int seed)
    {
        int normal = p.Boxes - p.Singles - p.Longs;
        var settings = new LoopLevelGeneratorSettings { Difficulty=p.Hard+1, Seed=seed, BoxCount=p.Boxes,
            ColorCount=p.Colors, EmptyBoxes=Math.Min(2, normal-p.Colors), SingleBoxes=p.Singles,
            LongBoxes=p.Longs, SecretPercent=0, ColorLocks=0, NumberLocks=0, Groups=0,
            RoadShape=p.Road, RoadCapacity=p.Capacity, ScrambleMoves=p.Level<5 ? 8 : 24+p.Level/5*4,
            CubePairs=0, AddCapacity=true, BoxGap=.35f, RoadClearance=.08f, VerifySolution=false };
        if (p.Level <= 4)
        {
            settings.RoadShape = 0;
            settings.BoardLayout = 1;
        }
        JObject root = LoopLevelGenerator.Generate(settings, p.Level, seed).Json;
        root.Remove("editor_generator");
        JObject layout = A.Layout(root);
        layout["Version"] = 4;
        layout["HardLevel"] = p.Hard;
        AlternativeCampaign70Geometry.Apply(layout, p, seed, ProjectRoot);
        MixFullBoardStacks(layout, p, seed);
        NormalizeSpecialBoxes(A.Array(layout, "Boxes"), p);
        MixFullNormalSection(A.Array(layout, "Boxes"), seed);
        if (p.Level == 1)
        {
            JArray tutorial = A.Array(layout, "Boxes");
            A.Array((JObject)tutorial[0], "Cubes").Clear();
            A.Array((JObject)tutorial[1], "Cubes").Clear();
            for (int i=0; i<3; i++) A.Array((JObject)tutorial[0], "Cubes").Add(A.NewCube(1));
            A.Array((JObject)tutorial[1], "Cubes").Add(A.NewCube(1));
        }
        ApplySpecialMechanics(root, p, seed);
        int mixed = 0;
        foreach (JObject box in A.Array(layout, "Boxes"))
        {
            if (A.Int(box, "BoxType") == 0 && HasMultipleColors(box)) mixed++;
        }
        if (p.Level >= 5 && mixed < Math.Max(2, normal / 2))
            throw new FormatException("Not enough mixed stacks; reject shallow shuffle.");
        List<string> errors = A.Validate(root, out _);
        if (errors.Count != 0) throw new FormatException(string.Join("\n", errors));
        AlternativeCampaign70Audit.CheckGeometry(root, ExportedPuzzleJsonDecoder.Decode(root.ToString()), p, p.Level);
        return root;
    }

    private static void MixFullBoardStacks(JObject layout, Profile profile, int seed)
    {
        if (profile.MixingMode == 0) return;
        if (profile.Boxes-profile.Longs < 8 || profile.Singles > 0 ||
            profile.Colors != profile.Boxes-profile.Longs)
            throw new FormatException("Layered mixing requires at least eight full normal boxes, one color per normal box, and no single queue.");
        var boxes = new List<JObject>();
        var colors = new SortedSet<int>();
        foreach (JObject box in A.Array(layout, "Boxes"))
        {
            if (A.Int(box, "BoxType") != 0) continue;
            boxes.Add(box);
            foreach (JObject cube in A.Array(box, "Cubes")) colors.Add(A.Int(cube, "Type"));
        }
        if (colors.Count != boxes.Count) throw new FormatException("Full-board color stock mismatch.");
        boxes.Sort(CompareBoxPaths);
        var palette = new List<int>(colors);
        var random = new System.Random(seed);
        for (int i = palette.Count - 1; i > 0; i--)
        {
            int target = random.Next(i + 1);
            int color = palette[i]; palette[i] = palette[target]; palette[target] = color;
        }
        int[][] recipes = { new[] { 0,0,1,2 }, new[] { 0,1,2,3 }, new[] { 0,1,2,2 } };
        int recipe = (int)((uint)seed % (uint)recipes.Length);
        // Thirteen-box references are a small set; avoid tying every Expert road
        // to the same recipe through matching modulo counts.
        if (profile.Hard == 2) recipe = random.Next(recipes.Length);
        int[] offsets = recipes[recipe];
        profile.MixingRecipe = "layered_cycle_" + string.Join("", offsets);
        // Every layer is a permutation: retain exactly four cubes of every color.
        // Neighbouring exits share dependencies; locked boxes then constrain the release order.
        for (int b = 0; b < boxes.Count; b++)
        {
            JArray cubes = A.Array(boxes[b], "Cubes");
            cubes.Clear();
            foreach (int offset in offsets) cubes.Add(A.NewCube(palette[(b + offset) % palette.Count]));
        }
    }

    private static int CompareBoxPaths(JObject first, JObject second)
    {
        return A.Int(first, "PathIndex").CompareTo(A.Int(second, "PathIndex"));
    }

    private static bool HasMultipleColors(JObject box)
    {
        JArray cubes = A.Array(box, "Cubes");
        if (cubes.Count < 2) return false;
        int first = A.Int((JObject)cubes[0], "Type");
        for (int c = 1; c < cubes.Count; c++)
            if (A.Int((JObject)cubes[c], "Type") != first) return true;
        return false;
    }

    private static void MixFullNormalSection(JArray allBoxes, int seed)
    {
        var boxes = new List<JObject>();
        int mixed = 0;
        foreach (JObject box in allBoxes)
        {
            if (A.Int(box, "BoxType") != 0) continue;
            if (A.Array(box, "Cubes").Count != 4) return;
            boxes.Add(box);
            if (HasMultipleColors(box)) mixed++;
        }
        if (boxes.Count < 2 || mixed >= Math.Max(2, boxes.Count/2)) return;
        // An empty long box is not a buffer for normal colors. Mix that full section
        // independently; the generic reverse scramble can only move the long stock.
        var random = new System.Random(seed);
        for (int step = 0; step < boxes.Count*10; step++)
        {
            int first = random.Next(boxes.Count), second = random.Next(boxes.Count);
            if (first == second) continue;
            JObject a = (JObject)A.Array(boxes[first], "Cubes")[random.Next(4)];
            JObject b = (JObject)A.Array(boxes[second], "Cubes")[random.Next(4)];
            int color = A.Int(a, "Type"); a["Type"] = b["Type"].DeepClone(); b["Type"] = color;
        }
    }

    private static void ApplySpecialMechanics(JObject root, Profile p, int seed)
    {
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");
        var random = new System.Random(seed);
        AddColorLocks(boxes, p);
        AddGroups(layout, boxes, p);
        AddAdjacentLocks(boxes, p);
        AddPair(root, boxes, p);
        AddSurprise(layout, boxes, p);
        int ice = 0;
        foreach (JObject box in boxes)
        {
            if (A.Int(box,"LockColor")>0 || A.Int(box,"IsAdjacentLocked")>0 || A.Int(box,"BoxType")!=0) continue;
            JArray cubes = A.Array(box,"Cubes");
            for (int c=0; c<cubes.Count-1 && ice<p.Ice; c++)
            {
                JObject cube=(JObject)cubes[c];
                if (A.Int(cube,"LinkedPairId")>0 || A.Int(cube,"GroupKeyId")>0 || A.Int(cube,"Type")>14) continue;
                cube["NumLock"] = p.Level==38 && ice==0 ? 3 : p.IceCount;
                ice++;
            }
        }
        if (ice != p.Ice) throw new FormatException("Not enough eligible ice cubes.");
        var eligible = new List<JObject>();
        foreach (JObject box in boxes)
        {
            JArray cubes = A.Array(box,"Cubes");
            for (int c=0; c<cubes.Count-1; c++)
            {
                JObject cube=(JObject)cubes[c];
                if (A.Int(cube,"GroupKeyId")==0 && A.Int(cube,"LinkedPairId")==0) eligible.Add(cube);
            }
        }
        int hidden = (int)Math.Round(eligible.Count*p.Secrets/100.0, MidpointRounding.AwayFromZero);
        for (int i=eligible.Count-1; i>0; i--) { int j=random.Next(i+1); JObject old=eligible[i]; eligible[i]=eligible[j]; eligible[j]=old; }
        for (int i=0; i<hidden; i++) eligible[i]["IsSecret"]=1;
        if (p.Secrets>0 && hidden==0) throw new FormatException("No hidden cubes generated.");
    }

    private static void NormalizeSpecialBoxes(JArray boxes, Profile profile)
    {
        int longIndex=0;
        foreach(JObject box in boxes)
        {
            if(A.Int(box,"BoxType")==2)
            {
                if (profile.Longs >= 4) continue;
                JArray cubes=A.Array(box,"Cubes"); cubes.Clear();
                int color=101+longIndex/2;
                int count = 3;
                if (profile.Level > 41) count = longIndex == 0 ? 2 : 4;
                for(int c=0;c<count;c++) cubes.Add(A.NewCube(color));
                longIndex++;
            }
            if(A.Int(box,"BoxType")!=1) continue;
            JArray queue=A.Array(box,"Cubes");
            while(queue.Count>1)
            {
                bool moved=false;
                foreach(JObject target in boxes)
                {
                    JArray destination=A.Array(target,"Cubes");
                    if(A.Int(target,"BoxType")!=0 || destination.Count>=4) continue;
                    JToken cube=queue[0]; queue.RemoveAt(0); destination.Insert(0,cube); moved=true; break;
                }
                if(!moved) throw new FormatException("Cannot reduce the introductory single queue to one cube.");
            }
        }
    }

    private static void AddColorLocks(JArray boxes, Profile p)
    {
        var usedColors=new HashSet<int>();
        var lockedContents=new HashSet<int>();
        int added=0;
        for(int b=boxes.Count-1;b>=0 && added<p.ColorLocks;b--)
        {
            JObject target=(JObject)boxes[b];
            if(A.Int(target,"BoxType")!=0 || A.Array(target,"Cubes").Count==0) continue;
            var forbidden=new HashSet<int>();
            foreach(JObject cube in A.Array(target,"Cubes")) forbidden.Add(A.Int(cube,"Type"));
            bool blocksEarlierKey=false;
            foreach(int used in usedColors)
                if(forbidden.Contains(used)) { blocksEarlierKey=true; break; }
            if(blocksEarlierKey) continue;
            int keyColor=0;
            foreach(JObject other in boxes)
            foreach(JObject cube in A.Array(other,"Cubes"))
            {
                int type=A.Int(cube,"Type");
                if(type<1 || type>14 || forbidden.Contains(type) || usedColors.Contains(type)) continue;
                if(p.Level==27 && added>0)
                {
                    if(!lockedContents.Contains(type)) continue;
                }
                else if(lockedContents.Contains(type)) continue;
                keyColor=type; break;
            }
            if(keyColor==0) continue;
            target["LockColor"]=keyColor; usedColors.Add(keyColor); added++;
            foreach(int type in forbidden) lockedContents.Add(type);
        }
        if(added!=p.ColorLocks) throw new FormatException("Cannot place the exact requested color locks.");
    }

    private static void AddAdjacentLocks(JArray boxes, Profile p)
    {
        int added=0;
        var pairMembers=new HashSet<int>();
        Puzzle puzzle=ExportedPuzzleJsonDecoder.Decode(new JObject { ["layout"]=new JObject { ["Boxes"]=boxes.DeepClone() } }.ToString());
        if(p.Level==33)
        {
            for(int a=0;a<boxes.Count;a++)
            for(int b=0;b<boxes.Count;b++)
            for(int c=0;c<boxes.Count;c++)
            {
                if(a==b || b==c || a==c || !CanCompleteAfterStart(puzzle.Boxes[a]) || puzzle.Boxes[b].Cubes.Count==0 || puzzle.Boxes[c].Cubes.Count==0) continue;
                if(!AdjacentLockRules.CanUnlock(puzzle.Boxes[a],puzzle.Boxes[b]) ||
                    !AdjacentLockRules.CanUnlock(puzzle.Boxes[b],puzzle.Boxes[c]) ||
                    AdjacentLockRules.CanUnlock(puzzle.Boxes[a],puzzle.Boxes[c])) continue;
                ((JObject)boxes[b])["IsAdjacentLocked"]=1;
                ((JObject)boxes[c])["IsAdjacentLocked"]=1;
                return;
            }
            throw new FormatException("Cannot place the three-box adjacent chain.");
        }
        for(int b=0; b<boxes.Count && added<p.Adjacent; b++)
        {
            JObject box=(JObject)boxes[b];
            if(A.Int(box,"BoxType")!=0 || A.Int(box,"LockColor")>0 || A.Array(box,"Cubes").Count==0) continue;
            if(p.Level==30 && pairMembers.Contains(b)) continue;
            for(int source=0; source<boxes.Count; source++)
            {
                if(source==b || A.Int((JObject)boxes[source],"IsAdjacentLocked")>0 ||
                    !CanCompleteAfterStart(puzzle.Boxes[source]) || (p.Level==30 && pairMembers.Contains(source)) ||
                    !AdjacentLockRules.CanUnlock(puzzle.Boxes[source],puzzle.Boxes[b])) continue;
                box["IsAdjacentLocked"]=1; pairMembers.Add(b); pairMembers.Add(source); added++; break;
            }
        }
        if(added!=p.Adjacent) throw new FormatException("Layout cannot satisfy the requested adjacent locks.");
    }

    private static bool CanCompleteAfterStart(BoxData box)
    {
        if(box.Cubes.Count==0) return false;
        if(box.Cubes.Count<4) return true;
        int type=box.Cubes[0].Type;
        foreach(CubeData cube in box.Cubes) if(cube.Type!=type) return true;
        return false;
    }

    private static void AddGroups(JObject layout, JArray boxes, Profile p)
    {
        var reserved=new HashSet<int>();
        for(int g=1; g<=p.Groups; g++)
        {
            JArray members=SelectGroupMembers(boxes,reserved,p.GroupSize);
            if(members.Count!=p.GroupSize) throw new FormatException("Not enough group members.");
            foreach(JToken member in members) reserved.Add(member.Value<int>());
            A.Array(layout,"Groups").Add(new JObject { ["Id"]=g, ["BoxIndexes"]=members, ["LockedByColor"]=0 });
        }
        for(int g=1; g<=p.Groups; g++)
        {
            JObject key=null;
            for(int b=0; b<boxes.Count && key==null; b++)
            {
                if(reserved.Contains(b)) continue;
                JArray cubes=A.Array((JObject)boxes[b],"Cubes");
                if(p.Level==55)
                {
                    if(cubes.Count>1 && A.Int((JObject)cubes[0],"GroupKeyId")==0) key=(JObject)cubes[0];
                }
                else
                    for(int c=cubes.Count-1; c>=0; c--)
                        if(A.Int((JObject)cubes[c],"GroupKeyId")==0) { key=(JObject)cubes[c]; break; }
            }
            if(key==null) throw new FormatException("No independent group key.");
            key["GroupKeyId"]=g;
            ((JObject)A.Array(layout,"Groups")[g-1])["LockedByColor"]=A.Int(key,"Type");
        }
    }

    private static JArray SelectGroupMembers(JArray boxes, HashSet<int> reserved, int count)
    {
        for(int anchor=boxes.Count-1;anchor>=0;anchor--)
        {
            JObject reference=(JObject)boxes[anchor];
            Vector3 position=A.Vector(reference,"Position"), rotation=A.Vector(reference,"Rotation");
            Vector3 forward=Quaternion.Euler(rotation)*Vector3.forward;
            Vector3 right=Quaternion.Euler(rotation)*Vector3.right;
            var candidates=new List<KeyValuePair<float,int>>();
            for(int b=0;b<boxes.Count;b++)
            {
                JObject box=(JObject)boxes[b];
                if(reserved.Contains(b) || A.Int(box,"BoxType")!=0 || A.Int(box,"LockColor")>0 || A.Array(box,"Cubes").Count==0) continue;
                if(Quaternion.Angle(Quaternion.Euler(rotation),Quaternion.Euler(A.Vector(box,"Rotation")))>1) continue;
                Vector3 delta=A.Vector(box,"Position")-position;
                if(Mathf.Abs(Vector3.Dot(delta,forward))>.05f) continue;
                candidates.Add(new KeyValuePair<float,int>(Vector3.Dot(delta,right),b));
            }
            candidates.Sort(CompareGroupCandidates);
            for(int start=0;start+count<=candidates.Count;start++)
            {
                bool contiguous=true;
                for(int i=start+1;i<start+count;i++)
                    if(candidates[i].Key-candidates[i-1].Key>2.15f) { contiguous=false; break; }
                if(!contiguous) continue;
                var members=new JArray();
                for(int i=start;i<start+count;i++) members.Add(candidates[i].Value);
                return members;
            }
        }
        return new JArray();
    }

    private static int CompareGroupCandidates(KeyValuePair<float,int> a, KeyValuePair<float,int> b)
    { return a.Key.CompareTo(b.Key); }

    private static void AddPair(JObject root, JArray boxes, Profile p)
    {
        if(p.Pair==0) return;
        for(int a=0; a<boxes.Count; a++)
        for(int b=a+1; b<boxes.Count; b++)
        {
            // Pair admission needs both heads. A wire on an adjacent-locked box can
            // otherwise trap the very neighbour whose completion must unlock it.
            if(A.Int((JObject)boxes[a],"IsAdjacentLocked")>0 || A.Int((JObject)boxes[b],"IsAdjacentLocked")>0 ||
                A.Int((JObject)boxes[a],"LockColor")>0 || A.Int((JObject)boxes[b],"LockColor")>0) continue;
            JArray ca=A.Array((JObject)boxes[a],"Cubes"), cb=A.Array((JObject)boxes[b],"Cubes");
            if(ca.Count==0 || cb.Count==0) continue;
            if(A.Int((JObject)ca[ca.Count-1],"GroupKeyId")>0 || A.Int((JObject)cb[cb.Count-1],"GroupKeyId")>0) continue;
            if(p.Level==59)
            {
                bool aGrouped=false, bGrouped=false;
                foreach(JObject group in A.Array(A.Layout(root),"Groups"))
                foreach(JToken member in A.Array(group,"BoxIndexes"))
                { if(member.Value<int>()==a) aGrouped=true; if(member.Value<int>()==b) bGrouped=true; }
                if(aGrouped==bGrouped) continue;
            }
            if(Vector3.Distance(A.Vector((JObject)boxes[a],"Position"),A.Vector((JObject)boxes[b],"Position"))>2.2f) continue;
            bool same=A.Int((JObject)ca[ca.Count-1],"Type")==A.Int((JObject)cb[cb.Count-1],"Type");
            if(p.Level==49 && !same || p.Level==51 && same) continue;
            if(!LoopLinkedCubeAuthoring.TryLinkTopCubes(root,a,b,out _)) continue;
            if(p.Level==50)
            {
                if(cb.Count<2) throw new FormatException("Buried partner needs two cubes.");
                JObject top=(JObject)cb[cb.Count-1]; top["LinkedPairId"]=0;
                ((JObject)cb[cb.Count-2])["LinkedPairId"]=1;
            }
            return;
        }
        throw new FormatException("No adjacent eligible pair matching the lesson.");
    }

    private static void AddSurprise(JObject layout, JArray boxes, Profile p)
    {
        var colors=new SortedDictionary<int,int>();
        var excluded=new HashSet<int>();
        foreach(JObject box in boxes)
        foreach(JObject cube in A.Array(box,"Cubes"))
        {
            int type=A.Int(cube,"Type");
            if(type<1 || type>14) continue;
            if(!colors.ContainsKey(type)) colors[type]=0;
            colors[type]++;
            if(A.Int(cube,"LinkedPairId")>0 || A.Int(cube,"GroupKeyId")>0) excluded.Add(type);
        }
        int added=0;
        foreach(KeyValuePair<int,int> entry in colors)
        {
            if(added==p.Surprise) break;
            if(excluded.Contains(entry.Key) || entry.Value!=4) continue;
            int type=201+added;
            var replacement=new JArray();
            for(int i=0;i<4;i++) replacement.Add(A.NewCube(entry.Key));
            A.Array(layout,"SurpriseDatas").Add(new JObject { ["SurpriseType"]=type, ["NormalCubes"]=replacement });
            foreach(JObject box in boxes)
            foreach(JObject cube in A.Array(box,"Cubes"))
                if(A.Int(cube,"Type")==entry.Key) cube["Type"]=type;
            added++;
        }
        if(added!=p.Surprise) throw new FormatException("Cannot allocate independent surprise colors.");
    }

    public static bool Replay(string json, IList<int> clicks, out int peak)
    {
        GameData data=LoopLevelGenerator.DecodeDetached(A.Parse(json));
        data.DifferentColorSliceSpacing=1; peak=0;
        if(data.IsGameOver() || clicks.Count==0) return false;
        foreach(int index in clicks)
        {
            if(index<0 || index>=data.Boxes.Count) return false;
            GameData next=LoopLevelGenerator.InSimulation(data, () => DebugLevelSolver.SimulateClick(data,index));
            if(next==null) return false;
            data=next; peak=Math.Max(peak,data.GetTransferGroup());
        }
        return data.IsGameOver() && data.TransferSlice.Count==0;
    }

    private static JObject CreateReport(Profile p, JObject root, int seed, List<int> clicks, int peak, List<string> warnings)
    {
        int eligible=0, hidden=0, empty=0;
        foreach(JObject box in A.Array(A.Layout(root),"Boxes"))
        {
            JArray cubes=A.Array(box,"Cubes"); if(cubes.Count==0) empty++;
            for(int i=0;i<cubes.Count;i++)
            {
                JObject c=(JObject)cubes[i];
                if(A.Int(c,"IsSecret")>0) hidden++;
                if(i<cubes.Count-1 && A.Int(c,"GroupKeyId")==0 && A.Int(c,"LinkedPairId")==0) eligible++;
            }
        }
        return new JObject { ["level"]=p.Level, ["builder_version"]=Version, ["seed"]=seed,
            ["learning_objective"]=LearningObjective(p.Level),
            ["sha256"]=Hash(root.ToString(Formatting.Indented)), ["profile"]=JObject.FromObject(p),
            ["mechanics"]=new JArray(GetMechanics(p)),
            ["empty_boxes"]=empty, ["secret_eligible"]=eligible, ["secret_count"]=hidden,
            ["structural_validation"]="passed", ["warnings"]=new JArray(warnings),
            ["capacity_display_review"]="pending",
            ["simulation_replay"]="passed", ["solution_clicks"]=new JArray(clicks),
            ["wait_policy"]="Wait for launch, landing, lock and surprise animations; allow conveyor circulation before the next click.",
            ["reference_click_count"]=clicks.Count, ["settled_road_peak_groups"]=peak,
            ["peak_limitations"]="Settled-state samples only; transient peak requires Unity measurement.",
            ["unity_replay"]="pending", ["visual_review"]="pending", ["lifecycle_review"]="pending",
            ["completion_seconds"]=JValue.CreateNull(), ["restart_count"]=JValue.CreateNull(),
            ["acceptance"]="draft_pending_unity_and_balance" };
    }

    public static string Hash(string json)
    {
        using(SHA256 hash=SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","").ToLowerInvariant();
    }
    public static string ReportPath(int level) { return Path.Combine(ReportFolder,"Level "+level+".json"); }

    internal static string LearningObjective(int level)
    {
        string spec=Path.Combine(ProjectRoot,"docs/superpowers/specs/2026-10-19-70-level-alternative-campaign-design.md");
        if(!File.Exists(spec)) throw new FileNotFoundException("Campaign design is required for the learning objectives.",spec);
        string prefix="| **"+level+"** |";
        foreach(string line in File.ReadLines(spec))
            if(line.StartsWith(prefix,StringComparison.Ordinal)) return line.Split('|')[2].Replace("**","").Trim();
        throw new FormatException("Missing learning objective for "+level);
    }

    private static void EnsureMeta(string path, bool folder)
    {
        if(File.Exists(path+".meta")) return;
        string text="fileFormatVersion: 2\nguid: "+Guid.NewGuid().ToString("N")+"\n";
        text+=folder ? "folderAsset: yes\nDefaultImporter:\n" : "TextScriptImporter:\n";
        text+="  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n";
        File.WriteAllText(path+".meta",text,new UTF8Encoding(false));
    }

    // Preserve the exact bytes used by the proof hash; the general editor writer appends a newline.
    private static void WriteAtomic(string path,string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        string temporary=path+".campaign.tmp";
        try
        {
            File.WriteAllText(temporary,text,new UTF8Encoding(false));
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if(File.Exists(path)) File.Replace(temporary,path,null);
                    else File.Move(temporary,path);
                    break;
                }
                catch (IOException)
                {
                    if (attempt >= 9) throw;
                    System.Threading.Thread.Sleep(100);
                }
            }
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}
#endif
