using System.Collections.Generic;

public enum BoxType
{
	Normal = 0,
	Single = 1,
	Long = 2
}

public struct SolverBlock
{
	public int Color;
	public bool IsSecret;
	public int GroupKeyId;
	public int NumLock;
	public int LinkedPairId;

	public SolverBlock(int color, bool isSecret = false, int groupKeyId = 0, int numLock = 0, int linkedPairId = 0)
	{
		Color = color;
		IsSecret = isSecret;
		GroupKeyId = groupKeyId;
		NumLock = numLock;
		LinkedPairId = linkedPairId;
	}
}

public static class BottleRules
{
	public const int NormalBottleCapacity = 4;
	public const int LongBottleCapacity = 6;

	private const int LongColorMin = 101;
	private const int LongColorMax = 199;
	private const int SurpriseColorMin = 201;
	private const int SurpriseColorMax = 299;

	public static bool IsSingle(BoxType boxType) => boxType == BoxType.Single;

	public static bool IsLong(BoxType boxType) => boxType == BoxType.Long;

	public static int GetRuntimeCapacity(BoxType boxType, int normalCapacity = NormalBottleCapacity)
	{
		if (boxType == BoxType.Single)
		{
			return 1;
		}

		return boxType == BoxType.Long ? LongBottleCapacity : normalCapacity;
	}

	public static bool CanAbsorbFromBelt(BoxType boxType) => boxType != BoxType.Single;

	public static int LimitPopCount(BoxType boxType, int topGroupCount)
	{
		if (topGroupCount <= 0)
		{
			return 0;
		}

		return boxType == BoxType.Single ? 1 : topGroupCount;
	}

	public static bool IsColorCompatible(BoxType boxType, int color)
	{
		if (color <= 0)
		{
			return false;
		}

		bool isLongColor = color >= LongColorMin && color <= LongColorMax;
		return boxType == BoxType.Long ? isLongColor : !isLongColor;
	}

	public static bool IsCompleted(
		IReadOnlyList<SolverBlock> bottle,
		BoxType boxType,
		int normalCapacity = NormalBottleCapacity)
	{
		if (!IsFullSameUnlockedColor(bottle, boxType, normalCapacity))
		{
			return false;
		}

		int color = bottle[0].Color;
		return color < SurpriseColorMin || color > SurpriseColorMax;
	}

	public static bool IsFullSameUnlockedColor(
		IReadOnlyList<SolverBlock> bottle,
		BoxType boxType,
		int normalCapacity = NormalBottleCapacity)
	{
		if (bottle == null || boxType == BoxType.Single ||
			bottle.Count != GetRuntimeCapacity(boxType, normalCapacity))
		{
			return false;
		}

		int color = bottle[0].Color;
		if (!IsColorCompatible(boxType, color))
		{
			return false;
		}

		for (int i = 0; i < bottle.Count; i++)
		{
			SolverBlock block = bottle[i];
			if (block.Color != color || block.IsSecret || block.NumLock > 0)
			{
				return false;
			}
		}

		return true;
	}

	public static bool CanAcceptColor(
		BoxType boxType,
		int targetLimitedColor,
		ISet<int> limitedColors,
		int color)
	{
		if (!IsColorCompatible(boxType, color))
		{
			return false;
		}

		if (targetLimitedColor > 0)
		{
			return targetLimitedColor == color;
		}

		return limitedColors == null || !limitedColors.Contains(color);
	}

	public static bool CanAcceptOnTop(IReadOnlyList<SolverBlock> bottle, int color)
	{
		if (bottle == null || bottle.Count == 0)
		{
			return true;
		}

		SolverBlock top = bottle[bottle.Count - 1];
		return top.NumLock <= 0 && top.Color == color;
	}

	public static bool RevealTopSecretRun(IList<SolverBlock> bottle, out int revealedColor)
	{
		revealedColor = -1;
		if (bottle == null || bottle.Count == 0)
		{
			return false;
		}

		SolverBlock top = bottle[bottle.Count - 1];
		if (!top.IsSecret) return false;
		top.IsSecret = false;
		bottle[bottle.Count - 1] = top;
		revealedColor = top.Color;
		return true;
	}

	public static bool IsPureColorPriorityTarget(
		IReadOnlyList<SolverBlock> bottle,
		BoxType boxType,
		int targetLimitedColor,
		ISet<int> limitedColors,
		int color)
	{
		if (limitedColors != null && limitedColors.Contains(color))
		{
			return false;
		}

		if (bottle == null || bottle.Count == 0 || boxType == BoxType.Single || targetLimitedColor > 0)
		{
			return false;
		}

		for (int i = 0; i < bottle.Count; i++)
		{
			SolverBlock block = bottle[i];
			if (block.Color != color || block.IsSecret)
			{
				return false;
			}
		}

		return true;
	}
}
