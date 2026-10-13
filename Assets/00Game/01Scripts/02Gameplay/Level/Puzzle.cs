using System.Collections.Generic;
using UnityEngine;

public class Puzzle
{
	public const int LegacyVersion = 4;
	// public int Version;
	public int HardLevel;
	public float Size;
	public int PathLength;
	public float PathSectionDistance;
	public List<BoxData> Boxes;
	public List<GamePath> Paths;
	public List<MapComponent> MapData;
	public List<GroupData> Groups;
	public List<RollingCoverData> Covers;
	public List<SurpriseData> SurpriseDatas;
	public int MainLevel;
	public int InnerIndex;

	public Puzzle()
	{
		Size = 10f;
		PathSectionDistance = 0.2f;
		Boxes = new List<BoxData>();
		Paths = new List<GamePath>();
		MapData = new List<MapComponent>();
		Groups = new List<GroupData>();
		Covers = new List<RollingCoverData>();
		SurpriseDatas = new List<SurpriseData>();
		MainLevel = -1;
		InnerIndex = -1;
	}

	public void SetPuzzleIndex(int mainLevel, int innerIndex)
	{
		MainLevel = mainLevel;
		InnerIndex = innerIndex;
	}
}

public class GroupData
{
	public int Id;
	public List<int> BoxIndexes;
	public int LockedByColor;

	public GroupData()
	{
		BoxIndexes = new List<int>();
	}

	public GroupData(int id, IEnumerable<int> boxIndexes, int lockedByColor)
	{
		Id = id;
		BoxIndexes = boxIndexes == null ? new List<int>() : new List<int>(boxIndexes);
		LockedByColor = lockedByColor;
	}
}

public class RollingCoverData
{
	public List<int> BoxIndexes;

	public RollingCoverData()
	{
		BoxIndexes = new List<int>();
	}

	public RollingCoverData(List<int> boxIndexes)
	{
		BoxIndexes = boxIndexes ?? new List<int>();
	}
}

public class MapComponent
{
	public string Name;
	public Vector3 Position;
	public Vector3 Rotation;
	public Vector3 Scale;

	public MapComponent()
	{
	}

	public MapComponent(string name, Vector3 position, Vector3 rotation, Vector3 scale)
	{
		Name = name;
		Position = position;
		Rotation = rotation;
		Scale = scale;
	}
}

public class SurpriseData
{
	public int SurpriseType;
	public List<CubeData> NormalCubes;

	public SurpriseData()
	{
	}

	public SurpriseData(int surpriseType, List<CubeData> types)
	{
		SurpriseType = surpriseType;
		NormalCubes = types;
	}
}
