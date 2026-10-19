using System;
using System.Collections.Generic;

public enum LinkedCubePairState { Pending, Launching, Consumed }
public enum LinkedCubeLaunchFailure { None, Busy, BlockedPartner, Capacity, InvalidState }

public sealed class LinkedCubePair
{
	public int Id { get; }
	public BoxInfo FirstBox { get; }
	public BoxInfo SecondBox { get; }
	public CubeInfo FirstCube { get; private set; }
	public CubeInfo SecondCube { get; private set; }
	public int FirstBoxIndex { get; }
	public int SecondBoxIndex { get; }
	public LinkedCubePairState State { get; internal set; }

	internal LinkedCubePair(int id, BoxInfo first, int firstIndex, BoxInfo second, int secondIndex,
		LinkedCubePairState state = LinkedCubePairState.Pending)
	{
		Id = id;
		State = state;
		FirstBox = first;
		SecondBox = second;
		FirstBoxIndex = firstIndex;
		SecondBoxIndex = secondIndex;
		RebindMembers();
		first.LinkedPair = this;
		second.LinkedPair = this;
	}

	internal void RebindMembers()
	{
		FirstCube = Member(FirstBox);
		SecondCube = Member(SecondBox);
		if (State != LinkedCubePairState.Consumed && (FirstCube == null || SecondCube == null))
			throw new InvalidOperationException("Linked cube pair " + Id + " lost a member.");
	}

	private CubeInfo Member(BoxInfo box)
	{
		for (int i = 0; i < box.CubeInfos.Count; i++)
			if (box.CubeInfos[i].LinkedPairId == Id) return box.CubeInfos[i];
		return null;
	}

	public bool Contains(BoxInfo box) => box == FirstBox || box == SecondBox;

	public static void Validate(Puzzle puzzle)
	{
		if (puzzle == null || puzzle.Boxes == null) return;
		int pairId = 0, firstBox = -1, memberCount = 0;
		for (int b = 0; b < puzzle.Boxes.Count; b++)
		{
			BoxData box = puzzle.Boxes[b];
			if (box == null || box.Cubes == null) continue;
			foreach (CubeData cube in box.Cubes)
			{
				if (cube == null) continue;
				if (cube.LinkedPairId < 0) throw Invalid("negative pair ID", b);
				if (cube.LinkedPairId == 0) continue;
				if (pairId != 0 && pairId != cube.LinkedPairId) throw Invalid("prototype supports one pair", b);
				if (memberCount > 0 && b == firstBox) throw Invalid("members must be in different boxes", b);
				if (cube.IsSecret != 0 || cube.GroupKeyId != 0 || cube.NumLock != 0 || cube.IsFixed != 0)
					throw Invalid("unsupported linked member", b);
				pairId = cube.LinkedPairId;
				if (memberCount == 0) firstBox = b;
				memberCount++;
			}
		}
		if (memberCount == 0) return;
		if (memberCount != 2) throw Invalid("each pair needs exactly two members", firstBox);
		if (puzzle.Groups != null && puzzle.Groups.Count > 0 || puzzle.Covers != null && puzzle.Covers.Count > 0 ||
			puzzle.SurpriseDatas != null && puzzle.SurpriseDatas.Count > 0)
			throw Invalid("prototype requires an ordinary uncovered board", firstBox);
		for (int b = 0; b < puzzle.Boxes.Count; b++)
		{
			BoxData box = puzzle.Boxes[b];
			if (box == null || box.Cubes == null || box.BoxType != BoxType.Normal ||
				box.LockColor != 0 || box.NumLock != 0 || box.IsAdjacentLocked != 0 || box.IsEnterOnly != 0 ||
				box.LimitedColor != 0 || box.MultiColorLock != null && box.MultiColorLock.Count > 0)
				throw Invalid("prototype requires normal unlocked boxes", b);
			foreach (CubeData cube in box.Cubes)
				if (cube == null || cube.Type <= 0 || cube.Type > 14 || cube.IsSecret != 0 ||
					cube.GroupKeyId != 0 || cube.NumLock != 0 || cube.IsFixed != 0)
					throw Invalid("prototype requires ordinary cubes", b);
		}
	}

	private static InvalidOperationException Invalid(string reason, int boxIndex)
	{
		return new InvalidOperationException("Linked cubes: " + reason + " (box " + boxIndex + ").");
	}
}