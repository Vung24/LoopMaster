using System;
using UnityEngine;

public sealed class BuiltinLevelCatalog : ScriptableObject
{
	public const string ResourceName = "BuiltinLevelCatalog";
	public const string SourceFolder = "00Game/Resources/LevelData";
	public const string ResourceFolder = "LevelData";
	public int[] LevelIds = Array.Empty<int>();

	private static BuiltinLevelCatalog _current;

	public static BuiltinLevelCatalog Current
	{
		get
		{
			if (object.ReferenceEquals(_current, null))
			{
				_current = Resources.Load<BuiltinLevelCatalog>(ResourceName);
			}
			return _current;
		}
		set => _current = value;
	}

	public int HighestLevel => LevelIds != null && LevelIds.Length > 0 ? LevelIds[LevelIds.Length - 1] : 0;

	public static void SetCurrent(BuiltinLevelCatalog catalog)
	{
		_current = catalog;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	public static void ResetCache()
	{
		_current = null;
	}

	public static void Invalidate()
	{
		_current = null;
	}

	public bool HasLevel(int levelNumber)
	{
		return LevelIds != null && Array.BinarySearch(LevelIds, levelNumber) >= 0;
	}

	public int GetHighestLevelAtOrBelow(int maximum)
	{
		if (LevelIds == null || LevelIds.Length == 0) return 0;
		if (maximum >= LevelIds[LevelIds.Length - 1]) return LevelIds[LevelIds.Length - 1];
		if (maximum < LevelIds[0]) return 0;
		int index = Array.BinarySearch(LevelIds, maximum);
		if (index >= 0) return LevelIds[index];
		int nextIndex = ~index;
		return nextIndex > 0 ? LevelIds[nextIndex - 1] : 0;
	}
}
