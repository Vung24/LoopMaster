#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using A = LoopLevelAuthoring;

[Serializable]
public sealed class LoopLevelGeneratorSettings
{
    public int Difficulty = 1, Seed = 12345, BoxCount = 10, ColorCount = 4, EmptyBoxes = 2;
    public int SingleBoxes = 1, LongBoxes, SecretPercent = 20, ColorLocks, NumberLocks, Groups;
    public int EnterOnlyBoxes, LimitedColorBoxes, AdjacentLocks;
    public int ScrambleMoves = 32, RoadCapacity = 6, RoadShape, MaxStates = 4000, Attempts = 8;
    public float BoxSpacing = 3f, RoadMargin = 2.5f;
    public float BoxGap = .6f, RoadClearance = .75f, BoardAspect = .75f;
    public int BoardColumns, BoardLayout;
    public bool VerifySolution = true;
    public void ApplyDifficulty(int difficulty)
    {
        Difficulty = Mathf.Clamp(difficulty, 0, 3);
        BoxCount = new[] { 7, 10, 14, 18 }[Difficulty];
        ColorCount = new[] { 3, 4, 5, 7 }[Difficulty];
        EmptyBoxes = 2; SingleBoxes = Difficulty < 1 ? 0 : Difficulty < 3 ? 1 : 2;
        LongBoxes = Difficulty >= 2 ? 3 : 0;
        SecretPercent = new[] { 0, 20, 35, 50 }[Difficulty];
        ColorLocks = Difficulty >= 2 ? 1 : 0;
        NumberLocks = Difficulty >= 2 ? 1 : 0; Groups = Difficulty >= 2 ? 1 : 0;
        EnterOnlyBoxes = Difficulty >= 2 ? 1 : 0; LimitedColorBoxes = Difficulty >= 2 ? 1 : 0;
        AdjacentLocks = Difficulty >= 3 ? 1 : 0;
        ScrambleMoves = new[] { 16, 32, 50, 80 }[Difficulty];
        RoadCapacity = new[] { 8, 6, 5, 4 }[Difficulty];
    }
    public LoopLevelGeneratorSettings Copy() => (LoopLevelGeneratorSettings)MemberwiseClone();
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Difficulty < 0 || Difficulty > 3 || RoadShape < 0 || RoadShape > 2) errors.Add("Chọn độ khó và hình đường hợp lệ.");
        if (BoxCount < 5 || BoxCount > 24) errors.Add("Tổng số hộp phải từ 5–24.");
        if (EmptyBoxes < 1 || EmptyBoxes > 4 || SingleBoxes < 0 || SingleBoxes > 3 || LongBoxes < 0 || LongBoxes > 6 || LongBoxes == 1)
            errors.Add("Dùng 1–4 hộp dự phòng thường, 0–3 hộp đơn và 0 hoặc 2–6 hộp dài (dành một hộp dài làm dự phòng).");
        int normalFilled = BoxCount - EmptyBoxes - SingleBoxes - LongBoxes;
        if (ColorCount < 2 || ColorCount > 14 || normalFilled < ColorCount) errors.Add("Mỗi màu thường cần ít nhất một hộp thường đầy, cộng các hộp dự phòng / đơn / dài đã chọn.");
        if (ColorLocks < 0 || ColorLocks > 3 || NumberLocks < 0 || NumberLocks > 3 || Groups < 0 || Groups > 3 || ColorLocks + NumberLocks + Groups > normalFilled - 1)
            errors.Add("Mỗi loại khóa tối đa 3; giữ ít nhất một hộp đầy nằm ngoài mọi khóa.");
        if (EnterOnlyBoxes < 0 || EnterOnlyBoxes > 3 || LimitedColorBoxes < 0 || LimitedColorBoxes > 3 || AdjacentLocks < 0 || AdjacentLocks > 3)
            errors.Add("Hộp chỉ nhận vào / nhận màu / khóa liền kề tối đa 3.");
        if (SecretPercent < 0 || SecretPercent > 70 || ScrambleMoves < 4 || ScrambleMoves > 160 || RoadCapacity < 2 || RoadCapacity > 16) errors.Add("Tỉ lệ khối ẩn, số bước trộn hoặc sức chứa đường không hợp lệ.");
        if (MaxStates < 100 || MaxStates > 20000 || Attempts < 1 || Attempts > 16) errors.Add("Số trạng thái tìm phải từ 100–20000, số lần thử từ 1–16.");
        if (!FiniteRange(BoxGap, .35f, 2) || !FiniteRange(RoadClearance, .4f, 3) || !FiniteRange(BoardAspect, .5f, 1.5f) || BoardColumns < 0 || BoardColumns > 24 || BoardLayout < 0 || BoardLayout > 1)
            errors.Add("Khoảng cách hộp: 0,35–2; khoảng hở đường: 0,4–3; tỉ lệ rộng/sâu: 0,5–1,5; số cột: 0 (tự động) hoặc 1–24.");
        return errors;
    }
    private static bool FiniteRange(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
}

public sealed class LoopGeneratedLevel
{
    public JObject Json;
    public int Seed, MixedBoxes, HiddenCubes;
    public readonly List<int> ReverseClicks = new List<int>();
    public List<int> Solution;
    public bool Verified => Solution != null;
}

public static class LoopLevelGenerator
{
    private static readonly MethodInfo SetCurrent = typeof(GameData).GetProperty(nameof(GameData.Current)).GetSetMethod(true);
    public static LoopGeneratedLevel Generate(LoopLevelGeneratorSettings settings, int levelNumber, int seed)
    {
        List<string> errors = settings.Validate();
        if (errors.Count > 0) throw new FormatException(string.Join("\n", errors));
        if (levelNumber < 1) throw new FormatException("Số màn chơi phải lớn hơn 0.");
        var random = new System.Random(seed); var result = new LoopGeneratedLevel { Seed = seed, Json = A.NewLevel(levelNumber) };
        JObject layout = A.Layout(result.Json);
        layout["Version"] = 7; layout["HardLevel"] = Mathf.Max(0, settings.Difficulty - 1);
        layout["PathLength"] = settings.RoadCapacity;
        JArray boxes = A.Array(layout, "Boxes");
        int normalBoxes = settings.BoxCount - settings.LongBoxes - settings.SingleBoxes;
        var palette = Enumerable.Range(1, 14).ToList(); Shuffle(palette, random);
        for (int i = 0; i < normalBoxes; i++)
        {
            JObject box = A.NewBox(Vector3.zero);
            if (i < normalBoxes - settings.EmptyBoxes) Fill(box, palette[i % settings.ColorCount], 4);
            boxes.Add(box);
        }
        var longPalette = Enumerable.Range(101, 5).ToList(); Shuffle(longPalette, random);
        for (int i = 0; i < settings.LongBoxes; i++)
        {
            JObject box = A.NewBox(Vector3.zero); box["BoxType"] = 2;
            if (i < settings.LongBoxes - 1) Fill(box, longPalette[i % 5], 6);
            boxes.Add(box);
        }
        for (int i = 0; i < settings.SingleBoxes; i++) { JObject box = A.NewBox(Vector3.zero); box["BoxType"] = 1; boxes.Add(box); }
        Mix(boxes, settings, random, result.ReverseClicks);
        foreach (JObject single in boxes.Cast<JObject>().Where(b => A.Int(b, "BoxType") == 1 && A.Array(b, "Cubes").Count == 0))
        {
            JObject source = boxes.Cast<JObject>().FirstOrDefault(b => A.Int(b, "BoxType") == 0 && A.Array(b, "Cubes").Count > 1);
            if (source == null) throw new FormatException("Không thể lấp hàng đợi hộp đơn đã chọn; thử seed khác.");
            JArray cubes = A.Array(source, "Cubes"); JToken cube = cubes[cubes.Count - 1]; cubes.RemoveAt(cubes.Count - 1);
            A.Array(single, "Cubes").Add(cube); result.ReverseClicks.Add(boxes.IndexOf(single));
        }
        result.ReverseClicks.Reverse();
        result.MixedBoxes = boxes.Cast<JObject>().Count(IsMixed);
        if (result.MixedBoxes < 2) throw new FormatException("Màu chưa được trộn đủ; thử seed khác hoặc tăng số bước trộn.");
        AddMechanics(boxes, layout, settings, random);
        foreach (JObject box in boxes)
        {
            JArray cubes = A.Array(box, "Cubes");
            int firstVisible = cubes.Count - 1;
            while (firstVisible > 0 && A.Int((JObject)cubes[firstVisible - 1], "Type") == A.Int((JObject)cubes[cubes.Count - 1], "Type")) firstVisible--;
            for (int c = 0; c < firstVisible; c++)
                if (A.Int((JObject)cubes[c], "GroupKeyId") == 0 && random.Next(100) < settings.SecretPercent)
                { cubes[c]["IsSecret"] = 1; result.HiddenCubes++; }
        }
        LoopLevelBoardLayout.Arrange(layout, settings, seed);
        var usedSettings = settings.Copy(); usedSettings.Seed = seed;
        result.Json["editor_generator"] = new JObject { ["version"] = 2, ["seed"] = seed, ["difficulty"] = settings.Difficulty,
            ["settings"] = JObject.FromObject(usedSettings), ["verified"] = false };
        errors = A.Validate(result.Json, out _);
        if (errors.Count > 0) throw new FormatException(string.Join("\n", errors));
        return result;
    }

    private static void Fill(JObject box, int color, int count)
    { for (int i = 0; i < count; i++) A.Array(box, "Cubes").Add(A.NewCube(color)); }
    private static bool IsMixed(JObject box) => A.Array(box, "Cubes").Select(c => c["Type"].Value<int>()).Distinct().Count() > 1;
    private static bool IsFinished(JObject box) => A.Int(box, "BoxType") != 1 && A.Array(box, "Cubes").Count == (A.Int(box, "BoxType") == 2 ? 6 : 4) && !IsMixed(box);
    private static void Shuffle<T>(IList<T> list, System.Random random)
    { for (int i = list.Count - 1; i > 0; i--) { int j = random.Next(i + 1); T old = list[i]; list[i] = list[j]; list[j] = old; } }

    private static void Mix(JArray boxes, LoopLevelGeneratorSettings settings, System.Random random, List<int> reverseClicks)
    {
        int moved = 0;
        for (int trial = 0; trial < settings.ScrambleMoves * 80 && moved < settings.ScrambleMoves; trial++)
        {
            int sourceIndex = random.Next(boxes.Count), targetIndex = random.Next(boxes.Count);
            if (sourceIndex == targetIndex) continue;
            JObject source = (JObject)boxes[sourceIndex], target = (JObject)boxes[targetIndex];
            if (A.Int(source, "BoxType") == 1) continue;
            JArray from = A.Array(source, "Cubes"), to = A.Array(target, "Cubes");
            if (from.Count == 0) continue;
            int type = A.Int((JObject)from[from.Count - 1], "Type"), targetType = A.Int(target, "BoxType");
            if ((type >= 101 && type <= 105) != (targetType == 2)) continue;
            int room = (targetType == 2 ? 6 : targetType == 1 ? 8 : 4) - to.Count;
            if (room <= 0 || (to.Count > 0 && A.Int((JObject)to[to.Count - 1], "Type") == type)) continue;
            int run = 1;
            while (run < from.Count && A.Int((JObject)from[from.Count - run - 1], "Type") == type) run++;
            int removable = run == from.Count ? run : run - 1;
            if (removable <= 0) continue;
            int amount = targetType == 1 ? 1 : random.Next(1, Math.Min(room, removable) + 1);
            for (int i = 0; i < amount; i++) { JToken cube = from[from.Count - 1]; from.RemoveAt(from.Count - 1); to.Add(cube); }
            reverseClicks.Add(targetIndex); moved++;
        }
    }

    private static void AddMechanics(JArray boxes, JObject layout, LoopLevelGeneratorSettings settings, System.Random random)
    {
        var eligible = Enumerable.Range(0, boxes.Count).Where(i => A.Array((JObject)boxes[i], "Cubes").Count > 0 && !IsFinished((JObject)boxes[i])).ToList();
        Shuffle(eligible, random);
        int keySource = eligible.Where(i => A.Int((JObject)boxes[i], "BoxType") != 2 && A.Array((JObject)boxes[i], "Cubes").Count >= Math.Max(1, settings.Groups)).DefaultIfEmpty(-1).First();
        if (eligible.Count == 0) throw new FormatException("Không có hộp nguồn có thể chơi.");
        if (settings.Groups > 0 && keySource < 0) throw new FormatException("Không có hộp nguồn thường / đơn có thể dùng để đặt chìa khóa nhóm.");
        eligible.Remove(keySource);
        for (int n = 0; n < settings.Groups; n++)
        {
            if (eligible.Count == 0) throw new FormatException("Không đủ hộp cho số nhóm đã chọn.");
            int target = eligible[eligible.Count - 1]; eligible.RemoveAt(eligible.Count - 1);
            JArray keyCubes = A.Array((JObject)boxes[keySource], "Cubes");
            int keyIndex = keyCubes.Count - 1 - n;
            if (keyIndex < 0) throw new FormatException("Không đủ khối có thể lấy để đặt chìa khóa nhóm; thử seed khác.");
            int id = n + 1; keyCubes[keyIndex]["GroupKeyId"] = id;
            A.Array(layout, "Groups").Add(new JObject { ["Id"] = id, ["BoxIndexes"] = new JArray(target), ["LockedByColor"] = keyCubes[keyIndex]["Type"].DeepClone() });
        }
        for (int n = 0; n < settings.ColorLocks; n++)
        {
            int target = -1, keyColor = 0;
            foreach (int i in eligible)
            {
                var inside = new HashSet<int>(A.Array((JObject)boxes[i], "Cubes").Select(c => c["Type"].Value<int>()));
                var outside = boxes.Cast<JObject>().Where(b => b != boxes[i] && A.Int(b, "LockColor") == 0 && !IsFinished(b))
                    .SelectMany(b => A.Array(b, "Cubes")).Select(c => c["Type"].Value<int>()).Distinct().Where(c => c < 100 && !inside.Contains(c)).ToList();
                if (outside.Count == 0) continue;
                target = i; keyColor = outside[random.Next(outside.Count)]; break;
            }
            if (target < 0) throw new FormatException("Không thể đặt khóa màu mà vẫn để màu mở khóa có thể lấy được. Thử seed khác hoặc giảm khóa.");
            boxes[target]["LockColor"] = keyColor; eligible.Remove(target);
        }
        for (int n = 0; n < settings.NumberLocks; n++)
        {
            if (eligible.Count == 0) throw new FormatException("Không đủ hộp cho số khóa số đã chọn.");
            int target = eligible[eligible.Count - 1]; eligible.RemoveAt(eligible.Count - 1); boxes[target]["NumLock"] = 1;
        }
        for (int n = 0; n < settings.EnterOnlyBoxes; n++)
        {
            if (eligible.Count == 0) break;
            int target = eligible[eligible.Count - 1]; eligible.RemoveAt(eligible.Count - 1);
            boxes[target]["IsEnterOnly"] = true;
        }
        for (int n = 0; n < settings.LimitedColorBoxes; n++)
        {
            if (eligible.Count == 0) break;
            int target = eligible[eligible.Count - 1]; eligible.RemoveAt(eligible.Count - 1);
            var insideColors = A.Array((JObject)boxes[target], "Cubes").Select(c => c["Type"].Value<int>()).Distinct().ToList();
            if (insideColors.Count > 0) boxes[target]["LimitedColor"] = insideColors[random.Next(insideColors.Count)];
        }
    }

    public static bool AutoFillCubesAndMechanics(JObject root, int colorCount, int secretPercent, int colorLocks, int numberLocks, int groups, int enterOnly, int limitedColor, int seed, out string error)
    {
        error = null;
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");
        int count = boxes.Count;
        if (count < 3)
        {
            error = "Cần ít nhất 3 hộp trên bàn chơi để tự động điền màu.";
            return false;
        }

        var random = new System.Random(seed);
        int emptyCount = Math.Max(1, Math.Min(2, count / 5));
        int filledCount = count - emptyCount;
        colorCount = Mathf.Clamp(colorCount, 2, Math.Min(14, filledCount));

        A.Array(layout, "Groups").Clear();
        foreach (JObject b in boxes)
        {
            A.Array(b, "Cubes").Clear();
            b.Remove("LockColor");
            b.Remove("NumLock");
            b.Remove("LimitedColor");
            b.Remove("IsEnterOnly");
            b.Remove("IsAdjacentLocked");
        }

        var palette = Enumerable.Range(1, 14).ToList();
        Shuffle(palette, random);

        for (int i = 0; i < filledCount; i++)
        {
            JObject box = (JObject)boxes[i];
            int boxType = A.Int(box, "BoxType");
            int cubeCapacity = boxType == 2 ? 6 : 4;
            int color = palette[i % colorCount];
            Fill(box, color, cubeCapacity);
        }

        var settings = new LoopLevelGeneratorSettings
        {
            ScrambleMoves = Math.Max(20, count * 4),
            ColorLocks = colorLocks,
            NumberLocks = numberLocks,
            Groups = groups,
            EnterOnlyBoxes = enterOnly,
            LimitedColorBoxes = limitedColor,
            SecretPercent = secretPercent
        };

        var reverseClicks = new List<int>();
        Mix(boxes, settings, random, reverseClicks);

        try
        {
            AddMechanics(boxes, layout, settings, random);
        }
        catch (Exception ex)
        {
            error = "Lưu ý đặt khóa: " + ex.Message;
        }

        foreach (JObject box in boxes)
        {
            JArray cubes = A.Array(box, "Cubes");
            int firstVisible = cubes.Count - 1;
            while (firstVisible > 0 && A.Int((JObject)cubes[firstVisible - 1], "Type") == A.Int((JObject)cubes[cubes.Count - 1], "Type")) firstVisible--;
            for (int c = 0; c < firstVisible; c++)
                if (A.Int((JObject)cubes[c], "GroupKeyId") == 0 && random.Next(100) < secretPercent)
                    cubes[c]["IsSecret"] = 1;
        }

        var errors = A.Validate(root, out _);
        if (errors.Count > 0)
        {
            error = string.Join("\n", errors);
            return false;
        }
        return true;
    }

    public static GameData DecodeDetached(JObject level)
    {
        GameData previous = GameData.Current;
        try { return GameLevelDataConverter.FromJson(level.ToString(Formatting.None)); }
        finally { SetCurrent.Invoke(null, new object[] { previous }); }
    }
    public static T InSimulation<T>(GameData data, Func<T> action)
    {
        GameData previous = GameData.Current;
        try { SetCurrent.Invoke(null, new object[] { data }); return action(); }
        finally { SetCurrent.Invoke(null, new object[] { previous }); }
    }
}

public sealed class LoopLevelGenerationJob : IDisposable
{
    private readonly LoopLevelGeneratorSettings _settings;
    private readonly int _level;
    private IEnumerator _runner;
    public LoopGeneratedLevel Candidate { get; private set; }
    public LoopGeneratedLevel Result { get; private set; }
    public int Attempt { get; private set; }
    public int Explored { get; private set; }
    public bool Done { get; private set; }
    public string Status { get; private set; }
    public LoopLevelGenerationJob(LoopLevelGeneratorSettings settings, int level)
    { _settings = settings.Copy(); _level = level; _runner = Run(); }
    public LoopLevelGenerationJob(LoopGeneratedLevel completed)
    {
        Candidate = Result = completed; Done = true;
        Status = "Đã khôi phục bản xem trước, seed " + completed.Seed + (completed.Verified ? "; đã tìm thấy lời giải." : "; chưa kiểm tra lời giải.");
    }
    public void Tick(double milliseconds = 10)
    {
        if (Done) return;
        var timer = Stopwatch.StartNew();
        try
        {
            do { if (!_runner.MoveNext()) { Done = true; DisposeRunner(); break; } }
            while (timer.Elapsed.TotalMilliseconds < milliseconds);
        }
        catch (Exception exception) { Status = "Tạo tự động thất bại: " + exception.Message; Done = true; DisposeRunner(); }
    }
    private IEnumerator Run()
    {
        for (Attempt = 1; Attempt <= _settings.Attempts; Attempt++)
        {
            int seed = unchecked(_settings.Seed + (Attempt - 1) * 7919);
            Candidate = null; Explored = 0;
            try { Candidate = LoopLevelGenerator.Generate(_settings, _level, seed); }
            catch (FormatException exception) { Status = exception.Message; }
            if (Candidate == null) { yield return null; continue; }
            if (!_settings.VerifySolution) { Result = Candidate; Status = "Đã tạo; chưa kiểm tra lời giải."; yield break; }
            GameData data = LoopLevelGenerator.DecodeDetached(Candidate.Json);
            var plan = new List<int>(); var witness = data;
            foreach (int index in Candidate.ReverseClicks)
            {
                Status = "Đang kiểm tra phương án tạo (lần thử " + Attempt + ").";
                if (!witness.Boxes[index].IsFinished() && !witness.Boxes[index].IsEmpty() && witness.Boxes[index].CanPopup())
                {
                    GameData next = LoopLevelGenerator.InSimulation(witness, () => DebugLevelSolver.SimulateClick(witness, index));
                    if (next == null) break;
                    witness = next; plan.Add(index);
                }
                yield return null;
            }
            if (witness.IsGameOver()) { Accept(plan); yield break; }
            var solver = new DebugLevelSolver(data, _settings.MaxStates);
            var timer = Stopwatch.StartNew();
            IEnumerator search = solver.Search();
            try
            {
                while (timer.Elapsed.TotalSeconds < 8 && LoopLevelGenerator.InSimulation(data, search.MoveNext))
                {
                    Explored = solver.Explored; Status = "Đang tìm lời giải: lần thử " + Attempt + "/" + _settings.Attempts + ", " + Explored + " trạng thái.";
                    yield return null;
                }
            }
            finally { (search as IDisposable)?.Dispose(); }
            Explored = solver.Explored;
            if (solver.Solution != null) { Accept(solver.Solution.Select(s => s.BoxIndex).ToList()); yield break; }
            Status = "Chưa tìm thấy lời giải trong giới hạn tìm kiếm; đang thử seed khác.";
            yield return null;
        }
        Attempt = _settings.Attempts;
        Status = "Chưa tìm thấy màn có lời giải. Thử seed khác, thêm hộp dự phòng / sức chứa đường, giảm khóa hoặc tăng giới hạn tìm kiếm.";
    }
    private void Accept(List<int> plan)
    {
        Candidate.Solution = plan; Result = Candidate;
        var metadata = (JObject)Candidate.Json["editor_generator"];
        metadata["verified"] = true; metadata["solution_clicks"] = new JArray(plan);
        Status = "Đã tìm thấy lời giải: " + plan.Count + " lần bấm, seed " + Candidate.Seed + ".";
    }
    private void DisposeRunner() { (_runner as IDisposable)?.Dispose(); _runner = null; }
    public void Dispose() { DisposeRunner(); Done = true; }
}
#endif
