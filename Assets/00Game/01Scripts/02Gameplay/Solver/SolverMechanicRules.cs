using System.Collections.Generic;

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
