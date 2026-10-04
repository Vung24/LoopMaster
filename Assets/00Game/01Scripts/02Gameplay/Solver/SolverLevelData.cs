using System.Collections.Generic;

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

public static class SolverMultiColorLockRules
{
	public static List<int> ClonePendingColors(IEnumerable<int> colors)
	{
		var result = new List<int>();
		if (colors == null)
		{
			return result;
		}

		foreach (int color in colors)
		{
			if (color > 0 && !result.Contains(color))
			{
				result.Add(color);
			}
		}

		return result;
	}

	public static bool HasPendingColors(IReadOnlyList<int> colors)
	{
		return colors != null && colors.Count > 0;
	}

	public static bool CompleteColor(List<int> pendingColors, int color)
	{
		if (pendingColors == null || color <= 0)
		{
			return false;
		}

		bool removed = false;
		for (int i = pendingColors.Count - 1; i >= 0; i--)
		{
			if (pendingColors[i] == color)
			{
				pendingColors.RemoveAt(i);
				removed = true;
			}
		}

		return removed;
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

public static class SolverCoverRules
{
	public static List<SolverCover> CloneCovers(IEnumerable<SolverCover> covers)
	{
		var result = new List<SolverCover>();
		if (covers == null)
		{
			return result;
		}

		foreach (SolverCover cover in covers)
		{
			result.Add(new SolverCover(cover?.BoxIndexes));
		}

		return result;
	}

	public static bool IsBoxCovered(IReadOnlyList<SolverCover> covers, int boxIndex)
	{
		if (covers == null || boxIndex < 0)
		{
			return false;
		}

		for (int i = 0; i < covers.Count; i++)
		{
			SolverCover cover = covers[i];
			if (cover?.BoxIndexes != null && cover.BoxIndexes.Contains(boxIndex))
			{
				return true;
			}
		}

		return false;
	}

	public static bool RevealNextBoxFromEachCover(IReadOnlyList<SolverCover> covers)
	{
		if (covers == null)
		{
			return false;
		}

		bool changed = false;
		for (int i = 0; i < covers.Count; i++)
		{
			List<int> indexes = covers[i]?.BoxIndexes;
			if (indexes != null && indexes.Count > 0)
			{
				indexes.RemoveAt(indexes.Count - 1);
				changed = true;
			}
		}

		return changed;
	}
}

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

public static class SolverSurpriseRules
{
	public static Dictionary<int, List<SolverBlock>> BuildNormalCubesByType(
		IEnumerable<SurpriseData> surpriseDatas)
	{
		var result = new Dictionary<int, List<SolverBlock>>();
		if (surpriseDatas == null)
		{
			return result;
		}

		foreach (SurpriseData data in surpriseDatas)
		{
			if (data == null || !CubeData.IsSurpriseColor(data.SurpriseType) ||
				data.NormalCubes == null || result.ContainsKey(data.SurpriseType))
			{
				continue;
			}

			var blocks = new List<SolverBlock>();
			foreach (CubeData cube in data.NormalCubes)
			{
				if (cube != null)
				{
					blocks.Add(new SolverBlock(cube.Type, cube.IsSecret > 0));
				}
			}

			result.Add(data.SurpriseType, blocks);
		}

		return result;
	}

	public static Dictionary<int, List<SolverBlock>> CloneNormalCubesByType(
		IReadOnlyDictionary<int, List<SolverBlock>> normalCubesByType)
	{
		var result = new Dictionary<int, List<SolverBlock>>();
		if (normalCubesByType == null)
		{
			return result;
		}

		foreach (KeyValuePair<int, List<SolverBlock>> pair in normalCubesByType)
		{
			result.Add(pair.Key,
				pair.Value == null ? new List<SolverBlock>() : new List<SolverBlock>(pair.Value));
		}

		return result;
	}

	public static bool TryResolveFullSurprise(
		List<SolverBlock> bottle,
		BoxType boxType,
		IReadOnlyDictionary<int, List<SolverBlock>> normalCubesByType,
		int normalCapacity = BottleRules.NormalBottleCapacity)
	{
		if (!BottleRules.IsFullSameUnlockedColor(bottle, boxType, normalCapacity) ||
			bottle == null || bottle.Count == 0)
		{
			return false;
		}

		int surpriseType = bottle[0].Color;
		if (!CubeData.IsSurpriseColor(surpriseType) || normalCubesByType == null ||
			!normalCubesByType.TryGetValue(surpriseType, out List<SolverBlock> replacements) ||
			replacements == null || replacements.Count != bottle.Count)
		{
			return false;
		}

		for (int i = 0; i < bottle.Count; i++)
		{
			bottle[i] = replacements[i];
		}

		return true;
	}
}
