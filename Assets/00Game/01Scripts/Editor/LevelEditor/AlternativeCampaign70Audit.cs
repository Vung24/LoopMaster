#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using A = LoopLevelAuthoring;

public static class AlternativeCampaign70Audit
{
    [MenuItem("Tools/LoopMaster/Alternative Campaign 70/Audit current campaign")]
    public static void Run()
    {
        var levels=new JArray();
        var guids=new HashSet<string>();
        var roadShapes = new HashSet<string>();
        var capacities = new SortedDictionary<int, int>();
        var boxCounts = new SortedDictionary<int, int>();
        var content = new JArray();
        int equalColorBoxCount = 0;
        int combinationCount = 0;
        int threeMechanicCount = 0, fourMechanicCount = 0, hardCount = 0, totalSecrets = 0;
        int nearSolvedTotal = 0;
        int linkedLevels = 0, multipleLinkedLevels = 0, totalLinkedPairs = 0, normalSingleBufferLevels = 0;
        var gentleLevels = new JArray();
        int runtime=0, capacityDisplays=0, capacityRules=0, addSpaceReviews=0;
        for(int level=1;level<=70;level++)
        {
            AlternativeCampaign70Builder.Profile p=AlternativeCampaign70Builder.Profile.Get(level);
            List<string> mechanics = AlternativeCampaign70Builder.GetMechanics(p);
            if (p.Hard > 0)
                Require(mechanics.Count >= (level < 20 ? 1 : 2),level,"Hard/Expert needs learned special mechanics");
            if (p.Pair > 0)
            {
                linkedLevels++;
                totalLinkedPairs += p.Pair;
                if (p.Pair > 1) multipleLinkedLevels++;
            }
            if (level >= 6 && !CampaignLearningCurve.IsRecovery(level))
                Require(mechanics.Count >= 1,level,"practice must reuse a learned mechanic");
            if (level >= 58 && !CampaignLearningCurve.IsRecovery(level))
                Require(mechanics.Count >= 4,level,"advanced level needs at least four distinct mechanic families");
            if (!CampaignLearningCurve.IsGentle(level) &&
                ((level >= 31 && level <= 35) || (level >= 51 && level <= 55 && level != 53) || level == 57))
                Require(mechanics.Count >= 3,level,"combination practice needs at least three mechanic families");
            if (level >= 43 && level <= 48 && level != 46)
                Require(mechanics.Count >= 3,level,"late practice needs at least three mechanic families");
            if (p.Hard > 0 && level >= 20)
                Require(p.Secrets >= (level == 30 ? 30 : 40),level,"challenge needs sufficient hidden information");
            if ((level >= 58 && !CampaignLearningCurve.IsRecovery(level)) || level == 51 || level == 52)
                Require(mechanics.Count >= 2,level,"later practice level needs distinct mechanic families");
            if (mechanics.Count >= 2) combinationCount++;
            if (mechanics.Count >= 3) threeMechanicCount++;
            if (mechanics.Count >= 4) fourMechanicCount++;
            if (p.Hard > 0) hardCount++;
            string path=Path.Combine(AlternativeCampaign70Builder.LevelFolder,"Level "+level+".json");
            string json=File.ReadAllText(path);
            JObject root=A.Parse(json), layout=A.Layout(root);
            int nearSolved = AlternativeCampaign70Builder.CountNearSolvedBoxes(A.Array(layout,"Boxes"));
            if (level >= 5) Require(nearSolved <= 1,level,"too many near-solved normal boxes");
            nearSolvedTotal += nearSolved;
            JObject report=JObject.Parse(File.ReadAllText(AlternativeCampaign70Builder.ReportPath(level)));
            Require(report.Value<int>("builder_version")==AlternativeCampaign70Builder.Version,level,"stale builder revision");
            Require(p.MixingMode==0 || ((string)report["profile"]?["MixingRecipe"] ?? "").StartsWith("layered_cycle_",StringComparison.Ordinal),
                level,"missing layered mixing recipe");
            report["profile"]["MixingMode"]=p.MixingMode;
            report["profile"]["Road"]=p.Road;
            report["mechanics"]=new JArray(mechanics);
            report["learning_objective"]=AlternativeCampaign70Builder.LearningObjective(level);
            Require(A.Int(root,"level_number")==level && A.Int(root,"puzzle_id")==level,level,"identity");
            Require((string)report["sha256"]==AlternativeCampaign70Builder.Hash(json),level,"stale proof hash");
            Require(A.Validate(root,out _).Count==0,level,"structural validation");
            var clicks=new List<int>();
            foreach(JToken click in (JArray)report["solution_clicks"]) clicks.Add(click.Value<int>());
            Require(AlternativeCampaign70Builder.Replay(json,clicks,out _),level,"final JSON replay");
            Puzzle puzzle=ExportedPuzzleJsonDecoder.Decode(json);
            Require(puzzle.Boxes.Count==p.Boxes && puzzle.HardLevel==p.Hard,level,"box count or hard label");
            Require(A.Int(layout,"PathLength")==p.Capacity,level,"road capacity");
            if (level >= 5 && p.Hard == 0)
                Require(p.Capacity >= 5 && p.Capacity <= 8,level,"Normal capacity must stay within the authored 5-8 range");
            Require(p.Capacity <= 8,level,"excess road capacity");
            int indicators = 0;
            foreach (JObject item in A.Array(layout,"MapData"))
                if (item.Value<string>("Name")=="Capacity") indicators++;
            Require(indicators==1,level,"exactly one serialized capacity marker required");
            int singles=0,longs=0,locks=0,adjacent=0,ice=0,links=0,hidden=0,eligible=0;
            var stock=new Dictionary<int,int>();
            foreach(BoxData box in puzzle.Boxes)
            {
                Require(box.IsEnterOnly==0 && box.LimitedColor==0 && box.NumLock==0 &&
                    (box.MultiColorLock==null || box.MultiColorLock.Count==0) && box.LinkedCubes.Count==0,level,"out-of-scope box mechanic");
                if(box.BoxType==BoxType.Single) { singles++; Require(box.Cubes.Count==1,level,"single queue must contain one cube"); }
                if(box.BoxType==BoxType.Long) longs++;
                if(box.LockColor>0) locks++;
                if(box.IsAdjacentLocked>0) adjacent++;
                for(int c=0;c<box.Cubes.Count;c++)
                {
                    CubeData cube=box.Cubes[c];
                    Require(cube.IsFixed==0,level,"out-of-scope fixed cube");
                    if(cube.NumLock>0) ice++;
                    if(cube.LinkedPairId>0) links++;
                    if(cube.IsSecret>0) hidden++;
                    if(c<box.Cubes.Count-1 && cube.GroupKeyId==0 && cube.LinkedPairId==0) eligible++;
                    if(!stock.ContainsKey(cube.Type)) stock[cube.Type]=0;
                    stock[cube.Type]++;
                }
            }
            if (level < 49) Require(singles==0,level,"single boxes must follow their introduction at 49");
            if (level < 15) Require(links==0,level,"linked cubes must follow their introduction at 15");
            Require(singles==p.Singles && longs==p.Longs && locks==p.ColorLocks && adjacent==p.Adjacent && ice==p.Ice && links==p.Pair*2,level,"mechanic counts");
            Require(puzzle.Groups.Count==p.Groups && puzzle.SurpriseDatas.Count==p.Surprise,level,"group or surprise count");
            Require(p.Secrets == 0 || hidden > 0,level,"secret lesson has no hidden cubes");
            int hiddenLinkedMembers = 0;
            foreach (BoxData box in puzzle.Boxes)
            {
                for (int cubeIndex = 0; cubeIndex < box.Cubes.Count; cubeIndex++)
                {
                    CubeData cube = box.Cubes[cubeIndex];
                    if (cube.IsSecret == 0) continue;
                    if (cube.LinkedPairId > 0) hiddenLinkedMembers++;
                    Require(cubeIndex < box.Cubes.Count - 1,level,"secret needs a visible covering cube");
                    Require(cube.GroupKeyId == 0 && cube.NumLock == 0 && cube.Type <= 14,
                        level,"secret must not obscure a key, ice counter or special color");
                }
            }
            if (CampaignSecretLayout.UsesLinkedSecrets(level))
                Require(hiddenLinkedMembers == (level == 22 ? 2 : p.Pair),level,"linked secret lesson has the wrong hidden member count");
            if (level <= 20) Require(hiddenLinkedMembers == 0,level,"linked hidden must follow Hard 20");
            if (level == 7)
            {
                Require(p.Hard == 0 && p.Boxes == 6 && p.Colors == 5 && p.Capacity == p.Boxes,
                    level,"bottom-secret lesson needs five colors and capacity equal to box count");
                int emptyBoxes = 0;
                foreach (BoxData box in puzzle.Boxes)
                {
                    if (box.Cubes.Count == 0) { emptyBoxes++; continue; }
                    Require(box.Cubes.Count == 4 && box.Cubes[0].IsSecret == 1,
                        level,"occupied boxes need four cubes and a hidden bottom");
                    for (int index = 1; index < box.Cubes.Count; index++)
                        Require(box.Cubes[index].IsSecret == 0,level,"only the bottom cube should be hidden");
                }
                Require(emptyBoxes == 1,level,"easy bottom-secret lesson needs exactly one empty box");
            }
            totalSecrets += hidden;
            foreach(KeyValuePair<int,int> entry in stock)
                Require(entry.Value%(CubeData.IsLongBoxColor(entry.Key)?6:4)==0,level,"color stock must fill complete target boxes");
            var normalColors=new HashSet<int>();
            foreach(int type in stock.Keys) if(type>=1 && type<=14) normalColors.Add(type);
            foreach(SurpriseData surprise in puzzle.SurpriseDatas)
            foreach(CubeData replacement in surprise.NormalCubes) normalColors.Add(replacement.Type);
            Require(normalColors.Count==p.Colors,level,"normal color count including surprise replacements");
            for (int type = 1; type <= p.Colors; type++)
                Require(normalColors.Contains(type),level,"normal types must be consecutive starting at 1");
            var longColors = new SortedSet<int>();
            foreach (int type in stock.Keys)
                if (CubeData.IsLongBoxColor(type)) longColors.Add(type);
            int nextLongType = 101;
            foreach (int type in longColors)
                Require(type==nextLongType++,level,"long types must be consecutive starting at 101");
            int mixed = 0, normalMixed = 0;
            foreach (BoxData box in puzzle.Boxes)
            {
                var colors = new HashSet<int>();
                foreach (CubeData cube in box.Cubes) colors.Add(cube.Type);
                if (colors.Count > 1)
                {
                    mixed++;
                    if (box.BoxType == BoxType.Normal) normalMixed++;
                }
            }
            if (level >= 5)
                Require(normalMixed >= Math.Max(2, (p.Boxes-p.Singles-p.Longs)/2) && clicks.Count >= p.Boxes,
                    level,"shallow puzzle content");
            // Paired releases can shorten a solution without reducing its dependencies.
            // Click counts are content filters, not measured human difficulty.
            report["reference_length_review"] = clicks.Count < AlternativeCampaign70Builder.MinimumReferenceClicks(p)
                ? "below_depth_floor" : "within_reference_target";
            report["minimum_reference_clicks"] = AlternativeCampaign70Builder.MinimumReferenceClicks(p);
            report["mechanic_introduction"] = CampaignLearningCurve.IsIntroduction(level);
            report["post_challenge_recovery"] = CampaignLearningCurve.IsRecovery(level);
            if (p.Hard == 0 && level >= 3)
            {
                int emptyBoxes = 0;
                foreach (BoxData box in puzzle.Boxes)
                    if (box.Cubes.Count == 0) emptyBoxes++;
                Require(emptyBoxes == 1 && p.Colors == p.Boxes - p.Singles - p.Longs - 1,
                    level,"easy level needs one empty box and one normal-color buffer");
                normalSingleBufferLevels++;
            }
            if (CampaignLearningCurve.IsGentle(level))
            {
                Require(p.Hard == 0 && p.Capacity >= p.Boxes && p.Capacity <= p.Boxes + 1 && clicks.Count <= p.Boxes * 2,
                    level,"teaching/recovery level must be forgiving and short");
                gentleLevels.Add(level);
            }
            if (CampaignLearningCurve.IsIntroduction(level))
                Require(p.Boxes <= 7 && p.Colors <= 5,level,"mechanic introduction needs a small board");
            if (level > 1 && AlternativeCampaign70Builder.Profile.Get(level - 1).Hard > 0)
                Require(CampaignLearningCurve.IsRecovery(level) && p.Hard == 0 && p.Boxes <= 8 && p.Colors <= 6,
                    level,"Hard/Expert must be followed by a small recovery level");
            if (level == 10)
                Require(clicks.Count >= 12 && clicks.Count <= 16,level,"first Hard reference solution must have 12-16 clicks");
            if (p.Hard > 0) Require(p.Capacity==4,level,"Hard/Expert capacity");
            if (p.Colors==p.Boxes) equalColorBoxCount++;
            string roadHash = AlternativeCampaign70Builder.Hash(layout["Paths"].ToString(Formatting.None));
            roadShapes.Add(roadHash);
            if (!capacities.ContainsKey(p.Capacity)) capacities[p.Capacity]=0;
            capacities[p.Capacity]++;
            if (!boxCounts.ContainsKey(p.Boxes)) boxCounts[p.Boxes]=0;
            boxCounts[p.Boxes]++;
            report["mixed_boxes"] = mixed;
            report["near_solved_normal_boxes"] = nearSolved;
            report["normal_mixed_boxes"] = normalMixed;
            report["road_geometry_sha256"] = roadHash;
            content.Add(new JObject { ["level"]=level, ["boxes"]=p.Boxes, ["normal_colors"]=p.Colors,
                ["capacity"]=p.Capacity, ["hard"]=p.Hard, ["mixed_boxes"]=mixed,
                ["near_solved_normal_boxes"]=nearSolved,
                ["geometry_reference"]=report["profile"]?["GeometryReference"]?.DeepClone(),
                ["mixing_recipe"]=report["profile"]?["MixingRecipe"]?.DeepClone(),
                ["mechanics"]=new JArray(mechanics),
                ["road_sha256"]=roadHash, ["reference_clicks"]=clicks.Count });
            CheckGeometry(root,puzzle,p,level);
            string[] meta=File.ReadAllLines(path+".meta"); string guid=null;
            foreach(string line in meta) if(line.StartsWith("guid: ",StringComparison.Ordinal)) guid=line.Substring(6).Trim();
            Require(!string.IsNullOrEmpty(guid) && guids.Add(guid),level,"missing or duplicate asset GUID");
            bool unity=(string)report["unity_replay"]=="passed";
            if(unity) runtime++;
            if((string)report["capacity_display_review"]=="passed") capacityDisplays++;
            else report["capacity_display_review"]="pending";
            if((string)report["capacity_rules_review"]=="passed") capacityRules++;
            if((string)report["add_space_runtime_review"]=="passed_initial_capacity_and_inventory")
            {
                Require(p.Hard>0 && report.Value<int>("add_space_capacity_before")==p.Capacity &&
                    report.Value<int>("add_space_capacity_after")==p.Capacity+GameData.PathToolExtraGroupCount,
                    level,"stale Add Space capacity evidence");
                addSpaceReviews++;
            }
            File.WriteAllText(AlternativeCampaign70Builder.ReportPath(level),report.ToString(Formatting.Indented));
            levels.Add(new JObject { ["level"]=level,["sha256"]=report["sha256"].DeepClone(),
                ["objective"]=report["learning_objective"].DeepClone(),["simulation_replay"]="passed",
                ["unity_replay"]=unity?"passed":"pending",["reference_clicks"]=clicks.Count,
                ["secret_count"]=hidden,["secret_eligible"]=eligible,["color_locks"]=locks,
                ["adjacent_locks"]=adjacent,["ice_cubes"]=ice,["linked_pairs"]=links/2,
                ["groups"]=p.Groups,["surprise_colors"]=p.Surprise });
        }
        var summary=new JObject { ["levels"]=levels,["structural_and_simulation_passed"]=70,
            ["mechanic_contracts_passed"]=70,["runtime_replay_passed"]=runtime,
            ["capacity_display_passed"]=capacityDisplays,
            ["capacity_rules_passed"]=capacityRules,
            ["hard_add_space_initialization_passed"]=addSpaceReviews,
            ["geometry_scope"]="Box footprint separation, contiguous same-facing door groups, adjacent lock sources and linked member distance. Does not replace Game View review.",
            ["final_acceptance"]="pending_visual_lifecycle_and_human_balance" };
        File.WriteAllText(Path.Combine(AlternativeCampaign70Builder.ReportFolder,"campaign-audit.json"),summary.ToString(Formatting.Indented));
        Require(roadShapes.Count>=24,0,"insufficient distinct road contours");
        Require(linkedLevels>=30 && multipleLinkedLevels>=20,0,"linked practice needs broader reuse and multiple pairs");
        File.WriteAllText(Path.Combine(AlternativeCampaign70Builder.ReportFolder,"content-distribution.json"),
            new JObject { ["builder_version"]=AlternativeCampaign70Builder.Version,
                ["distinct_road_geometries"]=roadShapes.Count, ["colors_equal_boxes_levels"]=equalColorBoxCount,
                ["levels_with_multiple_mechanics"]=combinationCount,
                ["levels_with_three_or_more_mechanics"]=threeMechanicCount,
                ["levels_with_four_or_more_mechanics"]=fourMechanicCount,
                ["hard_or_expert_levels"]=hardCount, ["total_secret_cubes"]=totalSecrets,
                ["linked_cube_levels"]=linkedLevels,
                ["levels_with_multiple_linked_pairs"]=multipleLinkedLevels,
                ["total_linked_pairs"]=totalLinkedPairs,
                ["normal_levels_with_one_empty_box"]=normalSingleBufferLevels,
                ["gentle_levels"]=gentleLevels,
                ["near_solved_normal_boxes"]=nearSolvedTotal,
                ["capacity_histogram"]=JObject.FromObject(capacities), ["box_count_histogram"]=JObject.FromObject(boxCounts),
                ["levels"]=content }.ToString(Formatting.Indented));
        Debug.Log("Alternative campaign audit: 70 structural/simulation/contracts passed; "+runtime+" current Unity proofs.");
    }

    public static void CheckGeometry(JObject root,Puzzle puzzle,AlternativeCampaign70Builder.Profile p,int level)
    {
        Require(CampaignBoxEntries.GetProblems(root).Count == 0,level,"shared entry slot or long box-to-road flight");
        JArray boxes=A.Array(A.Layout(root),"Boxes");
        foreach(JObject path in A.Array(A.Layout(root),"Paths"))
        {
            Vector3[] samples=LoopLevelBoardLayout.SampleRoad(path);
            for(int a=0;a+1<samples.Length;a++)
            for(int b=a+2;b+1<samples.Length;b++)
                Require(!Crosses(samples[a],samples[a+1],samples[b],samples[b+1]),level,"self-intersecting sampled road");
        }
        for(int a=0;a<boxes.Count;a++)
        for(int b=a+1;b<boxes.Count;b++)
        {
            Bounds first=LoopLevelBoardLayout.BoxBounds((JObject)boxes[a]);
            Bounds second=LoopLevelBoardLayout.BoxBounds((JObject)boxes[b]);
            float x=Mathf.Min(first.max.x,second.max.x)-Mathf.Max(first.min.x,second.min.x);
            float z=Mathf.Min(first.max.z,second.max.z)-Mathf.Max(first.min.z,second.min.z);
            Require(x<.01f || z<.01f,level,"overlapping box footprints");
        }
        var grouped=new HashSet<int>();
        foreach(GroupData group in puzzle.Groups)
        {
            Require(group.BoxIndexes.Count==p.GroupSize && group.LockedByColor>0,level,"door group size/color");
            BoxData first=puzzle.Boxes[group.BoxIndexes[0]];
            Vector3 forward=Quaternion.Euler(first.Rotation)*Vector3.forward;
            Vector3 right=Quaternion.Euler(first.Rotation)*Vector3.right;
            var positions=new List<float>();
            foreach(int index in group.BoxIndexes)
            {
                Require(grouped.Add(index),level,"shared group member");
                BoxData box=puzzle.Boxes[index];
                Require(Quaternion.Angle(Quaternion.Euler(first.Rotation),Quaternion.Euler(box.Rotation))<1,level,"group facing");
                Vector3 delta=box.Position-first.Position;
                Require(Mathf.Abs(Vector3.Dot(delta,forward))<.05f,level,"group row alignment");
                positions.Add(Vector3.Dot(delta,right));
            }
            positions.Sort();
            for(int i=1;i<positions.Count;i++) Require(positions[i]-positions[i-1]<=2.15f,level,"non-contiguous door group");
        }
        for(int b=0;b<puzzle.Boxes.Count;b++)
            if(puzzle.Boxes[b].IsAdjacentLocked>0)
                Require(AdjacentLockRules.GetSourceIndexes(puzzle.Boxes,b).Count>0,level,"adjacent lock without a geometric source");
        for (int pairId = 1; pairId <= p.Pair; pairId++)
        {
            int first=-1,second=-1,firstCube=-1,secondCube=-1;
            for(int b=0;b<puzzle.Boxes.Count;b++)
            for(int c=0;c<puzzle.Boxes[b].Cubes.Count;c++)
            {
                if(puzzle.Boxes[b].Cubes[c].LinkedPairId!=pairId) continue;
                if(first<0) { first=b; firstCube=c; } else { second=b; secondCube=c; }
            }
            Require(first>=0 && second>=0 && first!=second && Vector3.Distance(puzzle.Boxes[first].Position,puzzle.Boxes[second].Position)<=2.2f,level,"linked member distance");
            Require(puzzle.Boxes[first].LockColor==0 && puzzle.Boxes[second].LockColor==0 &&
                puzzle.Boxes[first].IsAdjacentLocked==0 && puzzle.Boxes[second].IsAdjacentLocked==0,
                level,"linked pair must be independent of color/adjacent admission locks");
            foreach (CubeData cube in puzzle.Boxes[first].Cubes)
                Require(cube.GroupKeyId == 0,level,"group key needs a box independent of linked admission");
            foreach (CubeData cube in puzzle.Boxes[second].Cubes)
                Require(cube.GroupKeyId == 0,level,"group key needs a box independent of linked admission");
            bool same=puzzle.Boxes[first].Cubes[firstCube].Type==puzzle.Boxes[second].Cubes[secondCube].Type;
            if(level==15)
            {
                Require(same,level,"same-color linked lesson");
                Require(firstCube==puzzle.Boxes[first].Cubes.Count-1 && secondCube==puzzle.Boxes[second].Cubes.Count-1,
                    level,"introductory linked heads must both be visible on top");
            }
            if(level==17) Require(!same,level,"different-color linked lesson");
            if(level==16) Require((firstCube==puzzle.Boxes[first].Cubes.Count-1)!=(secondCube==puzzle.Boxes[second].Cubes.Count-1),level,"one buried linked partner");
            if(level==59 && pairId==1) Require(grouped.Contains(first)!=grouped.Contains(second),level,"first pair must cross door boundary");
        }
    }
    private static void Require(bool value,int level,string message)
    { if(!value) throw new InvalidOperationException("Campaign level "+level+": "+message); }
    private static bool Crosses(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        float first=Side(a,b,c), second=Side(a,b,d), third=Side(c,d,a), fourth=Side(c,d,b);
        return first*second<-.000001f && third*fourth<-.000001f;
    }
    private static float Side(Vector3 a,Vector3 b,Vector3 point)
    { return (b.x-a.x)*(point.z-a.z)-(b.z-a.z)*(point.x-a.x); }
}
#endif
