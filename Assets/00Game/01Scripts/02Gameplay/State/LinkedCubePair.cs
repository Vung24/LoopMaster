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
		var firstMembers = new Dictionary<int, int>();
		var completed = new HashSet<int>();
		for (int b = 0; b < puzzle.Boxes.Count; b++)
		{
			BoxData box = puzzle.Boxes[b];
			if (box == null || box.Cubes == null) continue;
			bool hasMember = false;
			foreach (CubeData cube in box.Cubes)
			{
				if (cube == null) continue;
				if (cube.LinkedPairId < 0) throw Invalid("negative pair ID", b);
				if (cube.LinkedPairId == 0) continue;
				if (hasMember) throw Invalid("a box may belong to only one pair", b);
				if (completed.Contains(cube.LinkedPairId)) throw Invalid("each pair needs exactly two members", b);
				if (box.BoxType != BoxType.Normal) throw Invalid("linked cubes must be in normal boxes", b);
				if (cube.GroupKeyId != 0 || cube.NumLock != 0 || cube.IsFixed != 0)
					throw Invalid("unsupported linked member", b);
				if (cube.Type <= 0 || cube.Type > 14)
					throw Invalid("unsupported linked member color", b);
				hasMember = true;
				if (firstMembers.ContainsKey(cube.LinkedPairId)) completed.Add(cube.LinkedPairId);
				else firstMembers.Add(cube.LinkedPairId, b);
			}
		}
		foreach (KeyValuePair<int, int> member in firstMembers)
			if (!completed.Contains(member.Key)) throw Invalid("each pair needs exactly two members", member.Value);
	}

	private static InvalidOperationException Invalid(string reason, int boxIndex)
	{
		return new InvalidOperationException("Linked cubes: " + reason + " (box " + boxIndex + ").");
	}
}
