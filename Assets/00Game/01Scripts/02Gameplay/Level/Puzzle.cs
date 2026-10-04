using System;
using System.Collections.Generic;
using UnityEngine;

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

public class BoxData
{
	public BoxType BoxType;
	public int PathIndex;
	public int PathIndexV2;
	public Vector3 Position;
	public Vector3 Rotation;
	public List<CubeData> Cubes;
	public List<List<int>> LinkedCubes;
	public int LockColor;
	public int IsEnterOnly;
	public int NumLock;
	public int LimitedColor;
	public int IsAdjacentLocked;
	public List<int> MultiColorLock;

	public BoxData()
	{
		Cubes = new List<CubeData>();
		LinkedCubes = new List<List<int>>();
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

[Serializable]
public class ControlPoint
{
	public Vector3 Position;
	public Vector3 InTangent;
	public Vector3 OutTangent;

	public ControlPoint()
	{
	}

	public ControlPoint(Vector3 pos, Vector3 inTangent, Vector3 outTangent)
	{
		Position = pos;
		InTangent = inTangent;
		OutTangent = outTangent;
	}

	public ControlPoint(Vector3 pos)
	{
		Position = pos;
		InTangent = pos;
		OutTangent = pos;
	}
}

public class GamePath
{
	public int IsCircle;
	public List<ControlPoint> ControlPoints;

	public GamePath()
	{
		ControlPoints = new List<ControlPoint>();
	}
}

public class Puzzle
{
	public const int LegacyVersion = 4;
	public const int Version2 = 2;
	public const int Version3 = 3;
	public const int Version4 = 4;
	public const int Version5 = 5;
	public const int Version6 = 6;
	public const int Version7 = 7;
	public const int Version8 = 8;
	public const int Version9 = 9;
	public const int Version10 = 10;
	public const int Version11 = 11;
	public const int Version12 = 12;

	public int Version;
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
		Version = Version7;
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
