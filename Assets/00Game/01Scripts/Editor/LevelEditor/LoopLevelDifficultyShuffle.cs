#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using A = LoopLevelAuthoring;

/// <summary>Builds a full-board color preview without adding spare boxes or changing mechanics.</summary>
public static class LoopLevelDifficultyShuffle
{
    /// <summary>Fills editable boxes, samples seeded shuffles and keeps the closest structural score.</summary>
    public static void Generate(JObject root, IEnumerable<int> selection, ICollection<int> pinned,
        int seed, int difficulty, int reserveEmptyBoxes, out string report)
    {
        if (difficulty < 0 || difficulty > 3 || reserveEmptyBoxes < 0 || reserveEmptyBoxes > 2)
            throw new FormatException("Chọn độ khó và số hộp trống hợp lệ.");
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        var editable = new List<int>();
        int skipped = 0, reserved = 0;
        foreach (int index in LoopLevelCommands.Selection(root, selection))
        {
            JObject box = (JObject)boxes[index];
            if ((pinned != null && pinned.Contains(index)) || LoopLevelCubeEditing.IsGrouped(A.Layout(root), index) || !CanShuffle(box))
            {
                skipped++;
                continue;
            }
            if (A.Array(box, "Cubes").Count == 0 && reserved < reserveEmptyBoxes) { reserved++; continue; }
            editable.Add(index);
        }
        if (reserved != reserveEmptyBoxes) throw new FormatException("Không đủ hộp trống sẵn có để giữ lại; giảm số hộp dự phòng.");
        if (editable.Count < 2) throw new FormatException("Cần ít nhất hai hộp thường có thể sửa; hộp ghim/khóa/liên kết được giữ nguyên.");
        JObject baseline = (JObject)root.DeepClone();
        JArray baselineBoxes = A.Array(A.Layout(baseline), "Boxes");
        bool needsFill = false;
        foreach (int index in editable) if (A.Array((JObject)baselineBoxes[index], "Cubes").Count < 4) needsFill = true;
        if (needsFill) LoopLevelCubeEditing.FillMissingColors(baseline, editable, 1);
        ValidateSets(baseline);
        var stock = new List<int>();
        foreach (int index in editable)
            foreach (JObject cube in A.Array((JObject)baselineBoxes[index], "Cubes")) stock.Add(A.Int(cube, "Type"));
        stock.Sort();
        float target = new[] { 50f, 60f, 64f, 70f }[difficulty];
        int swaps = new[] { 2, Math.Max(3, stock.Count / 3), stock.Count, stock.Count * 3 }[difficulty];
        var random = new Random(seed);
        JObject best = null;
        LoopLevelDifficultyAnalysis bestAnalysis = null;
        float bestDistance = float.MaxValue;
        for (int attempt = 0; attempt < 96; attempt++)
        {
            var colors = new List<int>(stock);
            int swapCount = swaps + random.Next(Math.Max(2, swaps));
            for (int step = 0; step < swapCount; step++)
            {
                int first = random.Next(colors.Count), second = random.Next(colors.Count);
                int color = colors[first]; colors[first] = colors[second]; colors[second] = color;
            }
            JObject candidate = (JObject)baseline.DeepClone();
            JArray candidateBoxes = A.Array(A.Layout(candidate), "Boxes");
            int next = 0;
            foreach (int index in editable)
            foreach (JObject cube in A.Array((JObject)candidateBoxes[index], "Cubes"))
            {
                cube["Type"] = colors[next];
                next++;
            }
            LoopLevelDifficultyAnalysis analysis = LoopLevelDifficultyAnalysis.Evaluate(candidate);
            if (analysis.MixedBoxes == 0) continue;
            float distance = Math.Abs(analysis.Score - target);
            if (distance >= bestDistance) continue;
            best = candidate; bestAnalysis = analysis; bestDistance = distance;
        }
        if (best == null) throw new FormatException("Cần ít nhất hai màu khác nhau để tạo màn trộn.");
        // The caller supplies a preview clone; publish only after every candidate has been evaluated.
        root["layout"] = A.Layout(best).DeepClone();
        string[] bands = { "Dễ", "Thường", "Khó", "Rất khó" };
        string targetNote = "";
        if (bestAnalysis.Band != difficulty)
            targetNote = "\nChưa đạt dải yêu cầu với bố cục/cơ chế đang giữ. Thử seed khác hoặc chỉnh cơ chế/sức chứa đường; không tự đổi nhãn HardLevel.";
        report = "Mục tiêu: " + bands[difficulty] + " · Ước lượng đạt: " + bands[bestAnalysis.Band] + " · Seed " + seed +
            "\n" + bestAnalysis.Describe() + "\nĐã fill/trộn " + editable.Count + " hộp; giữ nguyên " + skipped +
            " hộp có cơ chế/ghim và " + reserved + " hộp dự phòng. Không thêm hộp mới.\n" +
            "Điểm là ước lượng cấu trúc, chưa xác nhận lời giải. Dùng Tìm lời giải bản xem trước và chơi thử trước khi chốt độ khó." + targetNote;
    }

    private static bool CanShuffle(JObject box)
    {
        if (A.Int(box, "BoxType") != 0 || A.Array(box, "Cubes").Count > 4 || A.Array(box, "LinkedCubes").Count > 0 ||
            A.Int(box, "LockColor") > 0 || A.Int(box, "NumLock") > 0 || A.Int(box, "LimitedColor") > 0 ||
            A.Int(box, "IsEnterOnly") > 0 || A.Int(box, "IsAdjacentLocked") > 0 || A.Array(box, "MultiColorLock").Count > 0) return false;
        foreach (JObject cube in A.Array(box, "Cubes"))
        {
            int color = A.Int(cube, "Type");
            if (color < 1 || color > 14 || A.Int(cube, "LinkedPairId") > 0 || A.Int(cube, "GroupKeyId") > 0 ||
                A.Int(cube, "IsFixed") > 0 || A.Int(cube, "NumLock") > 0) return false;
        }
        return true;
    }

    private static void ValidateSets(JObject root)
    {
        var counts = new int[15];
        foreach (JObject box in A.Array(A.Layout(root), "Boxes"))
        foreach (JObject cube in A.Array(box, "Cubes"))
        {
            int color = A.Int(cube, "Type");
            if (color >= 1 && color <= 14) counts[color]++;
        }
        for (int color = 1; color <= 14; color++)
            if (counts[color] != 0 && counts[color] != 4)
                throw new FormatException("Màu " + color + " cần đúng 4 cube trước khi trộn.");
    }
}
#endif
