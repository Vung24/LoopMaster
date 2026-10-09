#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public sealed class DebugLevelSolver
{
    public sealed class Step
    {
        public int BoxIndex;
        public string Before;
    }

    private sealed class Node
    {
        public GameData Data;
        public Node Parent;
        public int BoxIndex, Depth, Score, Order;
        public string Key;
    }

    private readonly GameData _root;
    private readonly int _maxStates;
    public List<Step> Solution { get; private set; }
    public int Explored { get; private set; }
    public string Failure { get; private set; }

    public DebugLevelSolver(GameData data, int maxStates = 10000)
    {
        _root = data.CreateDebugSimulationCopy();
        _maxStates = maxStates;
    }

    public IEnumerator Search()
    {
        var open = new SortedSet<Node>(Comparer<Node>.Create((a, b) =>
        {
            int score = a.Score.CompareTo(b.Score);
            return score != 0 ? score : a.Order.CompareTo(b.Order);
        }));
        var visited = new HashSet<string>();
        int order = 0;
        string rootKey = Signature(_root);
        open.Add(new Node { Data = _root, Key = rootKey });
        visited.Add(rootKey);
        while (open.Count > 0 && Explored < _maxStates)
        {
            Node node = open.Min;
            open.Remove(node);
            Explored++;
            if (node.Data.IsGameOver())
            {
                Solution = new List<Step>();
                for (Node item = node; item.Parent != null; item = item.Parent)
                    Solution.Add(new Step { BoxIndex = item.BoxIndex, Before = item.Parent.Key });
                Solution.Reverse();
                yield break;
            }
            if (node.Depth >= 200) continue;
            for (int i = 0; i < node.Data.Boxes.Count; i++)
            {
                BoxInfo source = node.Data.Boxes[i];
                if (!source.CanPopup() || source.IsFinished() || source.GetTopCubes(false).Count == 0 ||
                    !node.Data.CanCubeJumpToTransfer()) continue;
                GameData next = SimulateClick(node.Data, i);
                if (next != null)
                {
                    string key = Signature(next);
                    if (visited.Add(key))
                        open.Add(new Node
                        {
                            Data = next, Key = key, Parent = node, BoxIndex = i,
                            Depth = node.Depth + 1, Score = Score(next) + (node.Depth + 1) * 2,
                            Order = ++order
                        });
                }
                yield return null;
            }
            if (visited.Count >= _maxStates) break;
        }
        Failure = open.Count == 0 ? "No solution found from this board. Replay to try from the start." :
            "Search limit reached. Increase Max search states or replay the level.";
    }

    public static string Signature(GameData data)
    {
        var key = new StringBuilder();
        key.Append(data.ExtraPathLength).Append('|');
        foreach (GroupInfo group in data.Groups) key.Append(group.IsLocked ? '1' : '0');
        foreach (BoxInfo box in data.Boxes)
        {
            key.Append('|').Append(box.LockColor).Append(',').Append(box.NumLock)
                .Append(',').Append(box.IsAdjacentLocked ? 1 : 0);
            foreach (CubeInfo cube in box.CubeInfos)
                key.Append(';').Append(cube.Type).Append(',').Append(cube.Size)
                    .Append(',').Append(cube.IsSecret ? 1 : 0).Append(',').Append(cube.NumLock)
                    .Append(',').Append(cube.GroupKeyId);
        }
        foreach (var group in data.TransferSlice.GroupBy(slice => new
            { slice.Type, Source = data.Boxes.IndexOf(slice.SourceBox) })
            .OrderBy(group => group.Key.Type).ThenBy(group => group.Key.Source))
            key.Append('/').Append(group.Key.Type).Append(',').Append(group.Key.Source)
                .Append(',').Append(group.Count());
        return key.ToString();
    }

    private static int Score(GameData data)
    {
        int score = data.GetTransferGroup() * 35 + data.Groups.Count(group => group.IsLocked) * 80;
        foreach (BoxInfo box in data.Boxes)
        {
            score += (box.LockColor > 0 ? 200 : 0) + box.NumLock * 15;
            if (box.IsFinished()) continue;
            score += 150;
            for (int i = 1; i < box.CubeInfos.Count; i++)
                if (box.CubeInfos[i].Type != box.CubeInfos[i - 1].Type) score += 20;
        }
        return score;
    }

    public static GameData SimulateClick(GameData original, int boxIndex)
    {
        GameData data = original.CreateDebugSimulationCopy();
        BoxInfo source = data.Boxes[boxIndex];
        List<CubeInfo> top = source.GetTopCubes(false);
        if (data.PathPoints.Count < 2 || top.Count == 0 || !source.CanPopup() ||
            !data.CanCubeJumpToTransfer()) return null;
        var engine = new CoreGameplayEngine(data);
        var finished = new HashSet<BoxInfo>(data.Boxes.Where(box => box.IsFinished()));
        var landing = new Queue<KeyValuePair<SliceInfo, int>>();
        source.State = BoxState.OnPopup;
        data.OnJumpingCube = top.Count;
        int cubeIndex = 0, remaining = 0, lastLaunch = 0, quiet = 0;
        bool launching = true, released = false;
        const int jumpTicks = 16;
        int quietLimit = data.PathPoints.Count * 2 + jumpTicks;
        int limit = (data.TransferSlice.Count + top.Sum(cube => cube.Size) + 2) * quietLimit;
        for (int tick = 0; tick < limit; tick++)
        {
            int countBefore = data.TransferSlice.Count;
            AcceptSlices(data, engine, finished);
            int moved = MoveRoad(data, engine);
            while (landing.Count > 0 && landing.Peek().Value <= tick)
            {
                SliceInfo slice = landing.Dequeue().Key;
                slice.PathIndex = source.PathIndex;
            }

            if (launching)
            {
                if (remaining == 0)
                {
                    if (cubeIndex >= top.Count || !data.IsTransferHaveSpace()) launching = false;
                    else remaining = top[cubeIndex++].Size;
                }
                if (launching && remaining > 0)
                {
                    CubeInfo cube = source.PopupSlice();
                    if (cube == null) return null;
                    var slice = new SliceInfo(cube.Type, source);
                    data.AddTransferSlice(slice);
                    landing.Enqueue(new KeyValuePair<SliceInfo, int>(slice, tick + jumpTicks));
                    remaining--;
                    lastLaunch = tick;
                }
            }
            if (!launching && !released && landing.Count == 0 && tick >= lastLaunch + jumpTicks + 15)
            {
                source.ClearCubes();
                engine.RevealTopSecretRun(source);
                source.State = BoxState.Free;
                data.OnJumpingCube = 0;
                UnlockAvailableKeys(data);
                foreach (BoxInfo box in data.Boxes.Where(box => box.IsAdjacentLocked))
                    if (data.IsNoSolution() || data.ShouldForceUnlockAdjacentBox(box)) box.IsAdjacentLocked = false;
                released = true;
            }
            if (!released) continue;
            if (data.IsGameOver()) return data;
            if (data.IsGameFailed()) return null;
            quiet = countBefore == data.TransferSlice.Count ? quiet + 1 : 0;
            if (data.TransferSlice.Count == 0 || quiet >= quietLimit) return data;
            if (quiet > 0 && moved == data.TransferSlice.Count)
            {
                int distance = DistanceToNextAcceptance(data);
                if (distance == int.MaxValue) return data;
                int skip = Math.Min(distance, quietLimit - quiet - 1);
                foreach (SliceInfo slice in data.TransferSlice)
                    slice.PathIndex = (slice.PathIndex + skip) % data.PathPoints.Count;
                tick += skip;
                quiet += skip;
            }
        }
        return null;
    }

    private static int MoveRoad(GameData data, CoreGameplayEngine engine)
    {
        List<SliceInfo> road = data.TransferSlice.Where(slice => slice.PathIndex >= 0)
            .OrderByDescending(slice => slice.PathIndex).ToList();
        int begin = -1;
        for (int i = 0; i < road.Count; i++)
        {
            if (!engine.CanMove(road[i])) continue;
            begin = i;
            break;
        }
        bool overlap = begin < 0 && !data.Boxes.Any(box => box.State == BoxState.OnPopup);
        if (begin < 0) begin = 0;
        int moved = 0;
        for (int i = 0; i < road.Count; i++)
        {
            SliceInfo slice = road[(begin + i) % road.Count];
            if (!engine.CanMove(slice, overlap)) continue;
            slice.PathIndex = (slice.PathIndex + 1) % data.PathPoints.Count;
            slice.IsMoved = true;
            moved++;
        }
        return moved;
    }

    private static int DistanceToNextAcceptance(GameData data)
    {
        int distance = int.MaxValue;
        foreach (SliceInfo slice in data.TransferSlice)
            foreach (BoxInfo box in data.Boxes)
            {
                if (box.State != BoxState.Free || !box.CanJumpSlice(slice.Type) ||
                    (data.LimitedColor.Contains(slice.Type) && box.LimitedColor != slice.Type) ||
                    data.IsIgnoreBox(slice.Type, box)) continue;
                distance = Math.Min(distance, (box.PathIndex - slice.PathIndex +
                    data.PathPoints.Count) % data.PathPoints.Count);
            }
        return distance;
    }

    private static void AcceptSlices(GameData data, CoreGameplayEngine engine, HashSet<BoxInfo> finished)
    {
        foreach (SliceInfo slice in data.TransferSlice.Where(item => item.IsMoved && item.PathIndex >= 0)
            .OrderBy(item => item.PathIndex).ToList())
        {
            List<BoxInfo> targets = data.Boxes.Where(box => box.State == BoxState.Free &&
                box.PathIndex == slice.PathIndex && box.CanJumpSlice(slice.Type) &&
                (!data.LimitedColor.Contains(slice.Type) || box.LimitedColor == slice.Type) &&
                !data.IsIgnoreBox(slice.Type, box)).ToList();
            if (targets.Count > 1) targets.Remove(slice.SourceBox);
            if (targets.Count == 0) continue;
            BoxInfo target = targets[0];
            CubeInfo pushed = target.PushSlice(slice.Type) ?? target.AddCube(slice.Type);
            if (pushed == null) continue;
            data.RemoveTransferSlice(slice);
            bool surprise = engine.ResolveSurpriseBox(target);
            if (surprise || (target.IsFinished() && finished.Add(target)))
            {
                foreach (BoxInfo box in data.Boxes)
                {
                    if (box.LockColor == slice.Type) box.LockColor = 0;
                    if (box.NumLock > 0) box.NumLock--;
                    foreach (CubeInfo cube in box.CubeInfos) if (cube.NumLock > 0) cube.NumLock--;
                    if (box.IsAdjacentLocked && box.CanUnlockAdjacentBox(target)) box.IsAdjacentLocked = false;
                }
                foreach (CubeInfo key in target.CubeInfos.Where(cube => cube.GroupKeyId > 0))
                {
                    data.TryUnlockGroupColorLock(key.GroupKeyId);
                    key.ClearGroupColorLockKey();
                }
                UnlockAvailableKeys(data);
            }
        }
    }

    private static void UnlockAvailableKeys(GameData data)
    {
        foreach (BoxInfo box in data.Boxes.Where(box => !box.HasBlockingLock()))
            foreach (CubeInfo cube in box.GetTopCubes(false).Where(cube => cube.GroupKeyId > 0))
                if (data.TryUnlockGroupColorLock(cube.GroupKeyId)) cube.ClearGroupColorLockKey();
    }
}
#endif
