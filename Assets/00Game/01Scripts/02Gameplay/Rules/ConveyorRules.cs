using System;
using System.Collections.Generic;

public static class ConveyorRules
{
	public static bool CanMove(GameData data, SliceInfo slice, bool allowOverlap = false)
	{
		if (data == null || slice == null || slice.PathIndex < 0 || IsBoxBlockIndex(data, slice.PathIndex))
			return false;
		List<SliceInfo> transferSlices = data.TransferSlice;
		for (int i = 0; i < transferSlices.Count; i++)
			if (IsSliceBlocked(data, slice, transferSlices[i], allowOverlap)) return false;
		return true;
	}

	public static bool IsSliceBlocked(GameData data, SliceInfo slice, SliceInfo other, bool allowOverlap)
	{
		if (data == null || slice == null || other == null || slice == other || other.PathIndex < 0) return false;
		int count = data.PathPoints.Count;
		if (count <= 0) return false;
		int next = slice.PathIndex + 1;
		bool wraps = allowOverlap ? next > count : next >= count;
		if (other.PathIndex <= slice.PathIndex && wraps && other.PathIndex <= next % count)
			return true;
		int spacing = slice.Type == other.Type ? 1 : Math.Max(1, data.DifferentColorSliceSpacing);
		return slice.PathIndex < other.PathIndex && other.PathIndex <= slice.PathIndex + spacing;
	}

	public static int GetSliceDistance(GameData data, SliceInfo from, SliceInfo to)
	{
		if (data == null || from == null || to == null) return 0;
		int count = data.PathPoints.Count;
		if (count <= 0) return 0;
		if (to.PathIndex < from.PathIndex)
			return (count - from.PathIndex) + to.PathIndex;
		return to.PathIndex - from.PathIndex;
	}

	public static bool IsBoxBlockIndex(GameData data, int pathIndex)
	{
		if (data == null) return false;
		List<BoxInfo> boxes = data.Boxes;
		for (int i = 0; i < boxes.Count; i++)
		{
			BoxInfo box = boxes[i];
			if (box != null && box.State == BoxState.OnPopup && box.PathIndex - 1 == pathIndex) return true;
		}
		return false;
	}
}
