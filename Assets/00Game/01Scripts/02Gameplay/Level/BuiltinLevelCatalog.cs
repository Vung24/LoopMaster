using System;
using UnityEngine;

public sealed class BuiltinLevelCatalog : ScriptableObject
{
	public const string ResourceName = "BuiltinLevelCatalog";
	public const string SourceFolder = "00Game/05LevelData/LevelData";
	public const string ResourceFolder = "LevelData";
	public int[] LevelIds = Array.Empty<int>();

	public bool HasLevel(int levelNumber)
	{
		return LevelIds != null && Array.BinarySearch(LevelIds, levelNumber) >= 0;
	}

	public int GetHighestLevelAtOrBelow(int maximum)
	{
		int available = 0;
		if (LevelIds == null) return available;
		foreach (int number in LevelIds)
		{
			if (number > maximum) break;
			available = number;
		}
		return available;
	}
}
