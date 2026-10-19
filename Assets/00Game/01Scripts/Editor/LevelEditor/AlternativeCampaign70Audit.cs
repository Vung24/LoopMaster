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
        int runtime=0, capacityDisplays=0;
        for(int level=1;level<=70;level++)
        {
            AlternativeCampaign70Builder.Profile p=AlternativeCampaign70Builder.Profile.Get(level);
            List<string> mechanics = AlternativeCampaign70Builder.GetMechanics(p);
            if (level >= 58 || level == 51 || level == 52)
                Require(mechanics.Count >= 2,level,"later practice level needs distinct mechanic families");
            if (mechanics.Count >= 2) combinationCount++;
            string path=Path.Combine(AlternativeCampaign70Builder.LevelFolder,"Level "+level+".json");
            string json=File.ReadAllText(path);
            JObject root=A.Parse(json), layout=A.Layout(root);
            JObject report=JObject.Parse(File.ReadAllText(AlternativeCampaign70Builder.ReportPath(level)));
            Require(report.Value<int>("builder_version")==AlternativeCampaign70Builder.Version,level,"stale builder revision");
            Require(p.MixingMode==0 || ((string)report["profile"]?["MixingRecipe"] ?? "").StartsWith("layered_cycle_",StringComparison.Ordinal),
                level,"missing layered mixing recipe");
            report["profile"]["MixingMode"]=p.MixingMode;
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
            int indicators = 0;
            foreach (JObject item in A.Array(layout,"MapData"))
                if (item.Value<string>("Name")=="Capacity") indicators++;
            Require(indicators==1,level,"exactly one visible capacity indicator required");
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
            Require(singles==p.Singles && longs==p.Longs && locks==p.ColorLocks && adjacent==p.Adjacent && ice==p.Ice && links==p.Pair*2,level,"mechanic counts");
            Require(puzzle.Groups.Count==p.Groups && puzzle.SurpriseDatas.Count==p.Surprise,level,"group or surprise count");
            Require(hidden==(int)Math.Round(eligible*p.Secrets/100.0,MidpointRounding.AwayFromZero),level,"secret denominator or rounding");
            foreach(KeyValuePair<int,int> entry in stock)
                Require(entry.Value%(CubeData.IsLongBoxColor(entry.Key)?6:4)==0,level,"color stock must fill complete target boxes");
            var normalColors=new HashSet<int>();
            foreach(int type in stock.Keys) if(type>=1 && type<=14) normalColors.Add(type);
            foreach(SurpriseData surprise in puzzle.SurpriseDatas)
            foreach(CubeData replacement in surprise.NormalCubes) normalColors.Add(replacement.Type);
            Require(normalColors.Count==p.Colors,level,"normal color count including surprise replacements");
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
            if (p.Hard > 0) Require(p.Capacity==4,level,"Hard/Expert capacity");
            if (p.Colors==p.Boxes) equalColorBoxCount++;
            string roadHash = AlternativeCampaign70Builder.Hash(layout["Paths"].ToString(Formatting.None));
            roadShapes.Add(roadHash);
            if (!capacities.ContainsKey(p.Capacity)) capacities[p.Capacity]=0;
            capacities[p.Capacity]++;
            if (!boxCounts.ContainsKey(p.Boxes)) boxCounts[p.Boxes]=0;
            boxCounts[p.Boxes]++;
            report["mixed_boxes"] = mixed;
            report["normal_mixed_boxes"] = normalMixed;
            report["road_geometry_sha256"] = roadHash;
            content.Add(new JObject { ["level"]=level, ["boxes"]=p.Boxes, ["normal_colors"]=p.Colors,
                ["capacity"]=p.Capacity, ["hard"]=p.Hard, ["mixed_boxes"]=mixed,
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
            ["geometry_scope"]="Box footprint separation, contiguous same-facing door groups, adjacent lock sources and linked member distance. Does not replace Game View review.",
            ["final_acceptance"]="pending_visual_lifecycle_and_human_balance" };
        File.WriteAllText(Path.Combine(AlternativeCampaign70Builder.ReportFolder,"campaign-audit.json"),summary.ToString(Formatting.Indented));
        Require(roadShapes.Count>=24,0,"insufficient distinct road contours");
        File.WriteAllText(Path.Combine(AlternativeCampaign70Builder.ReportFolder,"content-distribution.json"),
            new JObject { ["builder_version"]=AlternativeCampaign70Builder.Version,
                ["distinct_road_geometries"]=roadShapes.Count, ["colors_equal_boxes_levels"]=equalColorBoxCount,
                ["levels_with_multiple_mechanics"]=combinationCount,
                ["capacity_histogram"]=JObject.FromObject(capacities), ["box_count_histogram"]=JObject.FromObject(boxCounts),
                ["levels"]=content }.ToString(Formatting.Indented));
        Debug.Log("Alternative campaign audit: 70 structural/simulation/contracts passed; "+runtime+" current Unity proofs.");
    }

    public static void CheckGeometry(JObject root,Puzzle puzzle,AlternativeCampaign70Builder.Profile p,int level)
    {
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
        if(p.Pair>0)
        {
            int first=-1,second=-1,firstCube=-1,secondCube=-1;
            for(int b=0;b<puzzle.Boxes.Count;b++)
            for(int c=0;c<puzzle.Boxes[b].Cubes.Count;c++)
            {
                if(puzzle.Boxes[b].Cubes[c].LinkedPairId==0) continue;
                if(first<0) { first=b; firstCube=c; } else { second=b; secondCube=c; }
            }
            Require(first>=0 && second>=0 && Vector3.Distance(puzzle.Boxes[first].Position,puzzle.Boxes[second].Position)<=2.2f,level,"linked member distance");
            Require(puzzle.Boxes[first].LockColor==0 && puzzle.Boxes[second].LockColor==0 &&
                puzzle.Boxes[first].IsAdjacentLocked==0 && puzzle.Boxes[second].IsAdjacentLocked==0,
                level,"linked pair must be independent of color/adjacent admission locks");
            bool same=puzzle.Boxes[first].Cubes[firstCube].Type==puzzle.Boxes[second].Cubes[secondCube].Type;
            if(level==49) Require(same,level,"same-color linked lesson");
            if(level==51) Require(!same,level,"different-color linked lesson");
            if(level==50) Require((firstCube==puzzle.Boxes[first].Cubes.Count-1)!=(secondCube==puzzle.Boxes[second].Cubes.Count-1),level,"one buried linked partner");
            if(level==59) Require(grouped.Contains(first)!=grouped.Contains(second),level,"pair must cross door boundary");
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
