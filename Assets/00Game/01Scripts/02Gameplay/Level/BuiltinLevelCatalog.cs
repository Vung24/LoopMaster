using System;
using UnityEngine;

// References keep the exported JSON assets in the player, on every build platform.
public sealed class BuiltinLevelCatalog : ScriptableObject
{
	public const string ResourceName = "BuiltinLevelCatalog";
	public const string SourceFolder = "00Game/05LevelData/LevelData";
	public TextAsset[] Levels = Array.Empty<TextAsset>();

	public TextAsset GetLevel(int levelNumber)
	{
		string name = "Level " + levelNumber;
		foreach (TextAsset level in Levels)
			if (level != null && level.name == name) return level;
		return null;
	}

	public int GetHighestLevelAtOrBelow(int maximum)
	{
		int available = 0;
		foreach (TextAsset level in Levels)
			if (level != null && level.name.StartsWith("Level ", StringComparison.Ordinal) &&
				int.TryParse(level.name.Substring(6), out int number) && number <= maximum && number > available)
				available = number;
		return available;
	}
}
