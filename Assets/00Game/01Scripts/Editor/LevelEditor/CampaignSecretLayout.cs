#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using A = LoopLevelAuthoring;

/// <summary>Authors readable reveal layers and progressively buried linked secrets.</summary>
public static class CampaignSecretLayout
{
    /// <summary>Places buried mysteries while preserving visible hints and cube mechanics.</summary>
    public static void Apply(JArray boxes, int level, int percent, int seed)
    {
        var candidates = new List<JObject>();
        foreach (JObject box in boxes)
        {
            JArray cubes = A.Array(box, "Cubes");
            foreach (JObject cube in cubes) cube["IsSecret"] = 0;
        }

        ArrangeLinkedSecrets(boxes, level);
        // Round-robin depths spread mysteries across boxes instead of hiding one whole stack.
        int offset = (int)((uint)seed % (uint)Math.Max(1, boxes.Count));
        for (int depth = 1; depth < 4; depth++)
        for (int step = 0; step < boxes.Count; step++)
        {
            JArray cubes = A.Array((JObject)boxes[(step + offset) % boxes.Count], "Cubes");
            int index = cubes.Count - 1 - depth;
            if (index < 0) continue;
            JObject cube = (JObject)cubes[index];
            if (A.Int(cube, "GroupKeyId") > 0 || A.Int(cube, "LinkedPairId") > 0 ||
                A.Int(cube, "NumLock") > 0 || A.Int(cube, "Type") > 14) continue;
            // A same-color run is one readable action; hiding its middle adds noise.
            if (A.Int(cube, "Type") == A.Int((JObject)cubes[index + 1], "Type")) continue;
            candidates.Add(cube);
        }

        int hidden = (int)Math.Round(candidates.Count * percent / 100.0, MidpointRounding.AwayFromZero);
        if (percent > 0 && candidates.Count > 0) hidden = Math.Max(1, hidden);
        for (int i = 0; i < hidden; i++) candidates[i]["IsSecret"] = 1;
    }

    private static void ArrangeLinkedSecrets(JArray boxes, int level)
    {
        // Hidden and ordinary links are taught separately before Hard 20.
        // The small recovery board at 21 introduces their combination.
        if (!UsesLinkedSecrets(level)) return;
        bool hideBoth = level == 22;
        int member = 0;
        foreach (JObject box in boxes)
        {
            JArray cubes = A.Array(box, "Cubes");
            int linkedIndex = -1;
            for (int i = 0; i < cubes.Count; i++)
                if (A.Int((JObject)cubes[i], "LinkedPairId") > 0) linkedIndex = i;
            if (linkedIndex < 0) continue;
            member++;
            if (!hideBoth && member == 1) continue;

            int target = cubes.Count - 2;
            if (target < 0) throw new FormatException("Linked secret needs a covering cube.");
            JObject cube = (JObject)cubes[target];
            if (A.Int(cube, "NumLock") > 0 || A.Int(cube, "GroupKeyId") > 0 ||
                A.Int(cube, "IsFixed") > 0 || A.Int(cube, "Type") < 1 || A.Int(cube, "Type") > 14)
                throw new FormatException("Linked secret would overlap another cube mechanic.");
            int pairId = A.Int((JObject)cubes[linkedIndex], "LinkedPairId");
            cubes[linkedIndex]["LinkedPairId"] = 0;
            cube["LinkedPairId"] = pairId;
            cube["IsSecret"] = 1;
        }
    }

    public static bool UsesLinkedSecrets(int level)
    {
        return level == 21 || level == 22 || level == 33 || level == 46 || level == 52 || level == 59;
    }
}
#endif
