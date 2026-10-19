using System;
using System.Collections.Generic;

public static class LevelDataConverter
{
	private const int BottleCapacity = 4;

	public static SolverLevelData FromPuzzle(Puzzle puzzle, bool includeLocks = true)
	{
		if (puzzle == null) throw new ArgumentNullException(nameof(puzzle));
		return FromBoxData(puzzle.Boxes, puzzle.Groups, puzzle.Covers,
			puzzle.SurpriseDatas, puzzle.PathLength, includeLocks);
	}

	public static SolverLevelData FromBoxData(IEnumerable<BoxData> boxes,
		int conveyorSlots, bool includeLocks = true) =>
		FromBoxData(boxes, null, null, null, conveyorSlots, includeLocks);

	public static SolverLevelData FromBoxData(IEnumerable<BoxData> boxes,
		IEnumerable<GroupData> groups, int conveyorSlots, bool includeLocks = true) =>
		FromBoxData(boxes, groups, null, null, conveyorSlots, includeLocks);

	public static SolverLevelData FromBoxData(IEnumerable<BoxData> boxes,
		IEnumerable<GroupData> groups, IEnumerable<RollingCoverData> covers,
		int conveyorSlots, bool includeLocks = true) =>
		FromBoxData(boxes, groups, covers, null, conveyorSlots, includeLocks);

	public static SolverLevelData FromBoxData(IEnumerable<BoxData> boxes,
		IEnumerable<GroupData> groups, IEnumerable<RollingCoverData> covers,
		IEnumerable<SurpriseData> surpriseDatas, int conveyorSlots,
		bool includeLocks = true)
	{
		var result = new SolverLevelData
		{
			ConveyorSlots = conveyorSlots,
			SurpriseNormalCubesByType = SolverSurpriseRules.BuildNormalCubesByType(surpriseDatas)
		};
		if (boxes == null) return result;

		IList<BoxData> boxList = boxes as IList<BoxData> ?? new List<BoxData>(boxes);
		Dictionary<int, ValueTuple<int, int>> groupMeta = includeLocks
			? BuildGroupMetaByBoxIndex(groups, boxList.Count)
			: new Dictionary<int, ValueTuple<int, int>>();
		Dictionary<int, List<int>> adjacentSources = includeLocks
			? BuildAdjacentLockSourcesByBoxIndex(boxList)
			: new Dictionary<int, List<int>>();

		for (int index = 0; index < boxList.Count; index++)
		{
			BoxData box = boxList[index];
			groupMeta.TryGetValue(index, out ValueTuple<int, int> group);
			adjacentSources.TryGetValue(index, out List<int> sources);
			result.Bottles.Add(new SolverBottle
			{
				BoxType = box?.BoxType ?? BoxType.Normal,
				Blocks = BuildBlocks(box?.Cubes,
					cube => cube.Type, cube => cube.IsSecret > 0,
					cube => includeLocks ? cube.GroupKeyId : 0,
					cube => includeLocks ? cube.NumLock : 0,
					cube => cube.LinkedPairId),
				LockedByColor = includeLocks && box != null ? box.LockColor : 0,
				MultiColorLock = includeLocks && box != null
					? SolverMultiColorLockRules.ClonePendingColors(box.MultiColorLock)
					: new List<int>(),
				NumLock = includeLocks && box != null ? box.NumLock : 0,
				GroupId = includeLocks ? group.Item1 : 0,
				GroupLockedByColor = includeLocks ? group.Item2 : 0,
				IsAdjacentLocked = includeLocks && box != null && box.IsAdjacentLocked > 0,
				AdjacentLockSourceIndexes = sources == null ? new List<int>() : new List<int>(sources),
				IsEnterOnly = box != null && box.IsEnterOnly > 0,
				EnterOnlyCubeCount = box != null && box.IsEnterOnly > 0
					? ClampCubeCount(box.Cubes?.Count ?? 0) : 0,
				LimitedColor = Math.Max(0, box?.LimitedColor ?? 0)
			});
		}

		result.Covers = BuildSolverCovers(covers, boxList);
		return result;
	}

	public static List<SolverCover> BuildSolverCovers(IEnumerable<RollingCoverData> covers,
		IList<BoxData> boxes)
	{
		var result = new List<SolverCover>();
		if (covers == null || boxes == null) return result;
		foreach (RollingCoverData cover in covers)
		{
			if (cover?.BoxIndexes == null) continue;
			var entries = new List<ValueTuple<int, BoxData>>();
			foreach (int index in cover.BoxIndexes)
			{
				if (index >= 0 && index < boxes.Count && boxes[index] != null)
					entries.Add((index, boxes[index]));
			}
			if (entries.Count == 0) continue;
			bool horizontal = IsCoverHorizontal(entries);
			entries.Sort((left, right) => horizontal
				? left.Item2.Position.x.CompareTo(right.Item2.Position.x)
				: left.Item2.Position.z.CompareTo(right.Item2.Position.z));
			var indexes = new List<int>(entries.Count);
			foreach (ValueTuple<int, BoxData> entry in entries) indexes.Add(entry.Item1);
			result.Add(new SolverCover(indexes));
		}
		return result;
	}

	public static BoxData ToBoxData(SolverBottle bottle, BoxData templateBox)
	{
		var result = new BoxData();
		if (templateBox != null)
		{
			result.PathIndex = templateBox.PathIndex;
			result.PathIndexV2 = templateBox.PathIndexV2;
			result.Position = templateBox.Position;
			result.Rotation = templateBox.Rotation;
		}
		if (bottle == null) return result;

		result.BoxType = bottle.BoxType;
		result.LockColor = bottle.LockedByColor;
		result.MultiColorLock = SolverMultiColorLockRules.ClonePendingColors(bottle.MultiColorLock);
		if (result.MultiColorLock.Count == 0) result.MultiColorLock = null;
		result.NumLock = bottle.NumLock;
		result.IsEnterOnly = bottle.IsEnterOnly ? 1 : 0;
		result.LimitedColor = Math.Max(0, bottle.LimitedColor);
		result.IsAdjacentLocked = bottle.IsAdjacentLocked ? 1 : 0;
		foreach (SolverBlock block in ToBottomToTopArray(bottle))
		{
			result.Cubes.Add(new CubeData(block.Color, block.IsSecret ? 1 : 0,
				block.GroupKeyId, block.NumLock) { LinkedPairId = block.LinkedPairId });
		}
		return result;
	}

	public static SolverBlock[] ToBottomToTopArray(SolverBottle bottle)
	{
		if (bottle?.Blocks == null) return Array.Empty<SolverBlock>();
		SolverBlock[] blocks = bottle.Blocks.ToArray();
		Array.Reverse(blocks);
		return blocks;
	}

	private static Stack<SolverBlock> BuildBlocks<T>(IEnumerable<T> cubes,
		Func<T, int> getColor, Func<T, bool> getIsSecret,
		Func<T, int> getGroupKeyId, Func<T, int> getNumLock, Func<T, int> getLinkedPairId = null)
	{
		var result = new Stack<SolverBlock>();
		if (cubes == null) return result;
		foreach (T cube in cubes)
		{
			if (ReferenceEquals(cube, null)) continue;
			result.Push(new SolverBlock(getColor(cube), getIsSecret(cube),
				getGroupKeyId?.Invoke(cube) ?? 0, getNumLock?.Invoke(cube) ?? 0, getLinkedPairId?.Invoke(cube) ?? 0));
		}
		return result;
	}

	private static Dictionary<int, ValueTuple<int, int>> BuildGroupMetaByBoxIndex(
		IEnumerable<GroupData> groups, int boxCount)
	{
		var result = new Dictionary<int, ValueTuple<int, int>>();
		if (groups == null) return result;
		foreach (GroupData group in groups)
		{
			if (group?.BoxIndexes == null) continue;
			foreach (int boxIndex in group.BoxIndexes)
			{
				if (boxIndex >= 0 && boxIndex < boxCount)
					result[boxIndex] = (group.Id, group.LockedByColor);
			}
		}
		return result;
	}

	private static Dictionary<int, List<int>> BuildAdjacentLockSourcesByBoxIndex(
		IList<BoxData> boxes)
	{
		var result = new Dictionary<int, List<int>>();
		if (boxes == null) return result;
		for (int index = 0; index < boxes.Count; index++)
		{
			if (boxes[index] != null && boxes[index].IsAdjacentLocked > 0)
				result[index] = AdjacentLockRules.GetSourceIndexes(boxes, index);
		}
		return result;
	}

	private static int ClampCubeCount(int cubeCount) =>
		Math.Max(0, Math.Min(BottleCapacity, cubeCount));

	private static bool IsCoverHorizontal(IReadOnlyList<ValueTuple<int, BoxData>> entries)
	{
		if (entries == null || entries.Count < 2) return true;
		float minX = float.MaxValue;
		float maxX = float.MinValue;
		float minZ = float.MaxValue;
		float maxZ = float.MinValue;
		for (int i = 0; i < entries.Count; i++)
		{
			var position = entries[i].Item2.Position;
			minX = Math.Min(minX, position.x);
			maxX = Math.Max(maxX, position.x);
			minZ = Math.Min(minZ, position.z);
			maxZ = Math.Max(maxZ, position.z);
		}
		return maxX - minX >= maxZ - minZ;
	}
}
