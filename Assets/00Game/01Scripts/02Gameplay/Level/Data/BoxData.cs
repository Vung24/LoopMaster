using System.Collections.Generic;
using UnityEngine;

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

public class CubeData
{
	public int Type;
	public int IsSecret;
	public int GroupKeyId;
	public int NumLock;
	public int IsFixed;

	public CubeData()
	{
	}

	public CubeData(int type, int isSecret = 0, int groupKeyId = 0, int numLock = 0)
	{
		Type = type;
		IsSecret = isSecret;
		GroupKeyId = groupKeyId;
		NumLock = numLock;
	}

	public static bool IsLongBoxColor(int color) => color >= 101 && color <= 199;
	public static bool IsSupportedLongBoxColor(int color) => color >= 101 && color <= 105;
	public static bool IsSurpriseColor(int color) => color >= 201 && color <= 299;
}
