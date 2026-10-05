using System.Collections.Generic;

public class SolverLevelData
{
	public List<SolverBottle> Bottles { get; set; }
	public List<SolverCover> Covers { get; set; }
	public Dictionary<int, List<SolverBlock>> SurpriseNormalCubesByType { get; set; }
	public int ConveyorSlots { get; set; }

	public SolverLevelData()
	{
		Bottles = new List<SolverBottle>();
		Covers = new List<SolverCover>();
		SurpriseNormalCubesByType = new Dictionary<int, List<SolverBlock>>();
	}
}

public class SolverBottle
{
	public BoxType BoxType;
	public int LockedByColor;
	public List<int> MultiColorLock { get; set; }
	public int NumLock;
	public int GroupId;
	public int GroupLockedByColor;
	public bool IsAdjacentLocked;
	public List<int> AdjacentLockSourceIndexes { get; set; }
	public bool IsEnterOnly;
	public int EnterOnlyCubeCount;
	public int LimitedColor;
	public bool IsIceLocked;
	public Stack<SolverBlock> Blocks { get; set; }

	public SolverBottle()
	{
		MultiColorLock = new List<int>();
		AdjacentLockSourceIndexes = new List<int>();
		Blocks = new Stack<SolverBlock>();
	}
}

public class SolverCover
{
	public List<int> BoxIndexes { get; set; }

	public SolverCover()
	{
		BoxIndexes = new List<int>();
	}

	public SolverCover(IEnumerable<int> boxIndexes)
	{
		BoxIndexes = boxIndexes == null ? new List<int>() : new List<int>(boxIndexes);
	}
}
