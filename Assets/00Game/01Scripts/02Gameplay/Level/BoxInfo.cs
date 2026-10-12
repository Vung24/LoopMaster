using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoxInfo
{
	public List<CubeInfo> CubeInfos;
	public Vector3 Pos;
	public Vector3 Rotation;
	public int LockColor;
	public int NumLock;
	public int LimitedColor;
	public bool IsAdjacentLocked;
	public bool IsEnterOnly;
	public int PathIndex;
	public BoxState State;
	public int LastPopupType;
	public bool IsLastUnlockSecret;
	public GroupInfo GroupInfo;
	public BoxType BoxType;

	public BoxInfo(BoxData data, Dictionary<int, int> colorMap)
	{
		if (data == null) throw new ArgumentNullException(nameof(data));
		CubeInfos = new List<CubeInfo>();
		if (data.Cubes != null)
			foreach (CubeData cube in data.Cubes) CubeInfos.Add(new CubeInfo(cube, colorMap));
		LockColor = CubeInfo.MapColor(data.LockColor, colorMap);
		LimitedColor = CubeInfo.MapColor(data.LimitedColor, colorMap);
		IsEnterOnly = data.IsEnterOnly > 0;
		IsAdjacentLocked = data.IsAdjacentLocked > 0;
		Pos = data.Position;
		Rotation = data.Rotation;
		PathIndex = data.PathIndexV2;
		NumLock = data.NumLock;
		BoxType = data.BoxType;
	}

	public List<CubeInfo> GetTopCubes(bool includeSecret)
	{
		var result = new List<CubeInfo>();
		if (CubeInfos.Count == 0) return result;
		int type = CubeInfos[CubeInfos.Count - 1].Type;
		for (int i = CubeInfos.Count - 1; i >= 0; i--)
		{
			CubeInfo cube = CubeInfos[i];
			if (cube.Type != type || (!includeSecret && cube.IsSecret) || cube.NumLock > 0) break;
			result.Add(cube);
			if (BoxType == BoxType.Single) break;
		}
		return result;
	}

	// Same traversal as GetTopCubes(false), without allocating a temporary list.
	public CubeInfo GetTopGroupLockKey()
	{
		if (CubeInfos.Count == 0) return null;
		int type = CubeInfos[CubeInfos.Count - 1].Type;
		for (int i = CubeInfos.Count - 1; i >= 0; i--)
		{
			CubeInfo cube = CubeInfos[i];
			if (cube.Type != type || cube.IsSecret || cube.NumLock > 0) break;
			if (cube.GroupKeyId > 0) return cube;
			if (BoxType == BoxType.Single) break;
		}
		return null;
	}

	public IEnumerable<CubeInfo> GetReachableCubes()
	{
		for (int i = CubeInfos.Count - 1; i >= 0; i--)
		{
			CubeInfo cube = CubeInfos[i];
			if (cube.IsSecret || cube.NumLock > 0) yield break;
			yield return cube;
		}
	}

	public Vector3 GetJumpOutPos()
	{
		float offset = BoxType == BoxType.Single ? -0.2f : 2.2f;
		return Pos + Quaternion.Euler(Rotation) * new Vector3(0f, 0f, offset);
	}

	public bool CanUnlockAdjacentBox(BoxInfo box)
	{
		return box != null && AdjacentLockRules.CanUnlock(
			box.Pos, box.Rotation, Pos, Rotation);
	}

	public CubeInfo PopupSlice()
	{
		CubeInfo cube = GetTopCube();
		if (cube == null) return null;
		IsLastUnlockSecret = false;
		LastPopupType = cube.Type;
		cube.Size--;
		return cube;
	}

	public CubeInfo PushSlice(int type)
	{
		CubeInfo top = GetTopCube();
		if (GetSliceCount() >= GetTotalSize() || top == null ||
			top.Size >= GameData.SliceCountPerGroup || top.Type != type) return null;
		top.Size++;
		return top;
	}

	public CubeInfo AddCube(int type, int size = 1)
	{
		if (GetSliceCount() >= GetTotalSize()) return null;
		var cube = new CubeInfo(type, size);
		CubeInfos.Add(cube);
		return cube;
	}

	public void ClearCubes() => CubeInfos.RemoveAll(cube => cube == null || cube.Size <= 0);

	public CubeInfo GetTopCube()
	{
		for (int i = CubeInfos.Count - 1; i >= 0; i--)
		{
			CubeInfo cube = CubeInfos[i];
			if (cube != null && cube.Size > 0) return cube;
			CubeInfos.RemoveAt(i);
		}
		return null;
	}

	public int GetTopType() => CubeInfos.Count == 0 ? -1 : CubeInfos[CubeInfos.Count - 1].Type;
	public bool IsEmpty() => CubeInfos.Count == 0 || GetSliceCount() == 0;
	public bool IsFull() => GetSliceCount() >= GetTotalSize();
	private int GetTotalSize() => GameData.GetSliceCapacity(BoxType);
	public int GetSliceCount()
	{
		int count = 0;
		for (int i = 0; i < CubeInfos.Count; i++)
		{
			CubeInfo cube = CubeInfos[i];
			if (cube != null) count = checked(count + cube.Size);
		}
		return count;
	}
	public int GetWantedCount() => GetTotalSize() - GetSliceCount();

	public bool IsFinished()
	{
		if (CubeInfos.Count == 0) return true;
		if (BoxType == BoxType.Single || GetSliceCount() < GetTotalSize() || HasSurpriseCube()) return false;
		int type = CubeInfos[CubeInfos.Count - 1].Type;
		for (int i = 0; i < CubeInfos.Count; i++)
			if (CubeInfos[i].Type != type) return false;
		return true;
	}

	public bool IsSameColorOrEmpty() => IsEmpty() || IsCubeSameType();

	private bool IsCubeSameType()
	{
		if (IsEmpty()) return false;
		int type = GetTopType();
		for (int i = 0; i < CubeInfos.Count; i++)
			if (CubeInfos[i].Type != type) return false;
		return true;
	}

	public bool CanShuffle() => BoxType != BoxType.Single && !HasUnFinishedCube() && !IsEmpty() &&
		!IsCubeSameType() && !IsFinished() && !HasBlockingLock();

	public bool ShuffleCubes()
	{
		if (!CanShuffle()) return false;
		List<List<CubeInfo>> groups = GetCubeGroups();
		if (groups.Count < 2) return false;

		var shuffled = new List<CubeInfo>(CubeInfos.Count);
		if (BoxType == BoxType.Long)
		{
			int split = groups.Count - groups.Count / 2;
			for (int i = split; i < groups.Count; i++) shuffled.AddRange(groups[i]);
			for (int i = 0; i < split; i++) shuffled.AddRange(groups[i]);
		}
		else if (groups.Count == 4)
		{
			shuffled.AddRange(groups[2]);
			shuffled.AddRange(groups[3]);
			shuffled.AddRange(groups[0]);
			shuffled.AddRange(groups[1]);
		}
		else
		{
			for (int i = groups[groups.Count - 1].Count - 1; i >= 0; i--)
				shuffled.Add(groups[groups.Count - 1][i]);
			for (int i = 0; i < groups.Count - 1; i++) shuffled.AddRange(groups[i]);
		}

		if (shuffled.Count != CubeInfos.Count || shuffled.SequenceEqual(CubeInfos)) return false;
		CubeInfos.Clear();
		CubeInfos.AddRange(shuffled);
		return true;
	}

	private List<List<CubeInfo>> GetCubeGroups()
	{
		var result = new List<List<CubeInfo>>();
		foreach (CubeInfo cube in CubeInfos)
		{
			if (cube == null || cube.Size <= 0) continue;
			if (result.Count == 0 || result[result.Count - 1][0].Type != cube.Type)
				result.Add(new List<CubeInfo>());
			result[result.Count - 1].Add(cube);
		}
		return result;
	}

	private bool HasUnFinishedCube() => CubeInfos.Any(cube => cube.Size != 0 && cube.Size < 10);

	public bool CanJumpSlice(int type)
	{
		if (HasBlockingLock() || IsLimitedColorLimited(type) ||
			BoxType == BoxType.Single || IsLongBoxTypeIncompatible(type)) return false;
		if (IsEmpty()) return true;
		CubeInfo top = GetTopCube();
		return !IsFinished() && GetSliceCount() < GetTotalSize() && top != null &&
			top.NumLock <= 0 && top.Type == type;
	}

	public bool CanPopup() => !IsFinished() && !CheckLimitedColorPopup() && !IsEnterOnly && !HasBlockingLock();

	public bool IsSliceCanJumpIn(SliceInfo slice)
	{
		if (slice == null) return false;
		GameData current = GameData.Current;
		if (current != null && current.LimitedColor.Contains(slice.Type) && LimitedColor != slice.Type)
			return false;
		if (!IsEmpty() && slice.SourceBox == this && LastPopupType == slice.Type && !IsLastUnlockSecret)
			return false;
		return CanJumpSlice(slice.Type);
	}

	public bool IsLongBoxTypeIncompatible(int type) =>
		CubeData.IsLongBoxColor(type) != (BoxType == BoxType.Long);
	private bool IsLimitedColorLimited(int type) => LimitedColor > 0 && LimitedColor != type;
	private bool CheckLimitedColorPopup() => LimitedColor > 0 &&
		!CubeInfos.Any(cube => cube.Type == LimitedColor && !cube.IsSecret);
	public bool HasBlockingLock() => LockColor > 0 || NumLock > 0 || IsGroupLocked() || IsAdjacentLocked;
	public bool HasBlockingLockExceptNumLock() => LockColor > 0 || IsGroupLocked() || IsAdjacentLocked;
	public bool IsGroupLocked() => GroupInfo != null && GroupInfo.IsLocked;
	public bool HasSurpriseCube()
	{
		for (int i = 0; i < CubeInfos.Count; i++)
			if (CubeData.IsSurpriseColor(CubeInfos[i].Type)) return true;
		return false;
	}
	public bool IsSurpriseCubeFinished() => !IsFinished() && HasSurpriseCube() &&
		GetSliceCount() >= GetTotalSize() && IsCubeSameType();
}
