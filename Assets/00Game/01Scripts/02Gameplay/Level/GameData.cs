using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum HardLevel { Normal = 0, Hard = 1, VeryHard = 2 }

public class UndoBox
{
	public BoxInfo Box;
	public int Size;
	public UndoBox(BoxInfo box, int size) { Box = box; Size = size; }
}

public class UndoData
{
	public BoxInfo PopupBox;
	public List<SliceInfo> Slices;
	public List<UndoBox> BoxData;
	public int Type;
	public bool IsOnUndoing;

	public UndoData()
	{
		Slices = new List<SliceInfo>();
		BoxData = new List<UndoBox>();
	}

	public bool CanUndo() => !IsOnUndoing && Type >= 0 && PopupBox != null &&
		(Slices.Count > 0 || BoxData.Count > 0);

	public void Init(BoxInfo box) { Clear(); PopupBox = box; }

	public void AddUndo(List<SliceInfo> slices)
	{
		if (slices == null || slices.Count == 0) return;
		Slices.AddRange(slices);
		Type = slices[0].Type;
	}

	public void UpdateUndoBox(SliceInfo slice, BoxInfo box)
	{
		if (PopupBox == box) { Clear(); return; }
		if (!CanUndo() || !Slices.Remove(slice)) return;
		UndoBox undoBox = null;
		for (int i = 0; i < BoxData.Count; i++)
		{
			if (BoxData[i].Box != box) continue;
			undoBox = BoxData[i];
			break;
		}
		if (undoBox == null) BoxData.Add(new UndoBox(box, 1));
		else undoBox.Size++;
	}

	public void Clear()
	{
		if (IsOnUndoing) return;
		PopupBox = null;
		Type = -1;
		Slices.Clear();
		BoxData.Clear();
	}
}

public class SliceInfo
{
	public int Type;
	public int PathIndex;
	public Slice SliceSc;
	public BoxInfo SourceBox;
	public bool IsMoved;

	public SliceInfo(int type, BoxInfo sourceBox)
	{
		PathIndex = -1;
		Type = type;
		SourceBox = sourceBox;
	}
}

public class CubeInfo
{
	public int Type;
	public int Size;
	public bool IsSecret;
	public int GroupKeyId;
	public int GroupKeyColor;
	public int NumLock;
	public Cube CubeSc;

	public CubeInfo(CubeData data, Dictionary<int, int> colorMap)
	{
		if (data == null) throw new ArgumentNullException(nameof(data));
		Type = MapColor(data.Type, colorMap);
		Size = GameData.SliceCountPerGroup;
		IsSecret = data.IsSecret > 0;
		GroupKeyId = data.GroupKeyId;
		NumLock = data.NumLock;
	}

	public CubeInfo(int type, int size) { Type = type; Size = size; }

	public bool ClearGroupColorLockKey()
	{
		if (GroupKeyId <= 0) return false;
		GroupKeyId = 0;
		GroupKeyColor = 0;
		return true;
	}

	public bool IsFull() => Size > GameData.SliceCountPerGroup - 1;

	internal static int MapColor(int type, Dictionary<int, int> colorMap)
	{
		int mapped;
		return colorMap != null && colorMap.TryGetValue(type, out mapped) ? mapped : type;
	}
}

public enum BoxState { Free = 0, OnPopup = 1, OnPush = 2, OnUnlocking = 3 }

public class GroupInfo
{
	public int Id;
	public int LockedByColor;
	public List<int> BoxIndexes;
	public bool IsLocked;

	public GroupInfo(GroupData data)
	{
		if (data == null) throw new ArgumentNullException(nameof(data));
		BoxIndexes = data.BoxIndexes == null ? new List<int>() : new List<int>(data.BoxIndexes);
		IsLocked = true;
		Id = data.Id;
		LockedByColor = data.LockedByColor;
	}

	public bool Unlock() { bool oldValue = IsLocked; IsLocked = false; return oldValue; }
}

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
	public int GetSliceCount() => CubeInfos.Where(cube => cube != null).Sum(cube => cube.Size);
	public int GetWantedCount() => GetTotalSize() - GetSliceCount();

	public bool IsFinished()
	{
		if (CubeInfos.Count == 0) return true;
		if (BoxType == BoxType.Single || GetSliceCount() < GetTotalSize() || HasSurpriseCube()) return false;
		int type = CubeInfos[CubeInfos.Count - 1].Type;
		return CubeInfos.All(cube => cube.Type == type);
	}

	public bool IsSameColorOrEmpty() => IsEmpty() || IsCubeSameType();

	private bool IsCubeSameType()
	{
		if (IsEmpty()) return false;
		int type = GetTopType();
		return CubeInfos.All(cube => cube.Type == type);
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

	public bool CanPopup() => !CheckLimitedColorPopup() && !IsEnterOnly && !HasBlockingLock();

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
	public bool HasSurpriseCube() => CubeInfos.Any(cube => CubeData.IsSurpriseColor(cube.Type));
	public bool IsSurpriseCubeFinished() => !IsFinished() && HasSurpriseCube() &&
		GetSliceCount() >= GetTotalSize() && IsCubeSameType();
}

public class SurpriseInfo
{
	public int SurpriseType;
	public List<CubeInfo> NormalCubes;
	public SurpriseInfo() { NormalCubes = new List<CubeInfo>(); }
	public SurpriseInfo(SurpriseData data, Dictionary<int, int> colorMap) : this()
	{
		if (data == null) return;
		SurpriseType = CubeInfo.MapColor(data.SurpriseType, colorMap);
		if (data.NormalCubes != null)
			NormalCubes.AddRange(data.NormalCubes.Select(cube => new CubeInfo(cube, colorMap)));
	}
}

public class GameData
{
	private const float DefaultPathSectionDistance = 0.2f;
	public const int SliceCountPerGroup = 10;
	public const int NormalCubeCapacity = 4;
	public const int LongBoxCubeCapacity = 6;
	public const int PathToolExtraGroupCount = 2;
	public const float BoxComboTime = 6f;
	public const float BoxEmojiComboTime = 1f;

	public static GameData Current { get; private set; }
	public static void ClearCurrent() => Current = null;
	internal static void SetCurrent(GameData data) => Current = data;
	public float Duration;
	public int TotalCubeCount, FinishBoxCount, SecretCount, SecretClearCount;
	public int ColorLockCount, ColorLockClearCount, OnlyEnterBoxCount, OnlyEnterClearCount;
	public int ToolUndoUsedCount, ToolShuffleUsedCount, ToolPathUsedCount, PlayStepCount;
	public string FailReason;
	public int FailCount;
	public bool IsLogFirstBoxEvent;
	public float LastFinishBoxTime;
	public int BoxCombo, BoxEmojiCombo;
	public int Id { get; private set; }
	public Puzzle Puzzle { get; private set; }
	public List<BoxInfo> Boxes;
	public List<GroupInfo> Groups;
	public List<SliceInfo> TransferSlice;
	public List<SurpriseInfo> SurpriseInfos;
	public List<Vector3> PathPoints;
	public List<int> PathSectionIndices;
	public UndoData UndoData;
	public int ExtraPathLength;
	private Dictionary<int, int> _colorMap;
	private float _pathDistance;
	private float _roadSpacingScale = 1f;
	public float RoadSpacingScale => _roadSpacingScale;
	public int DifferentColorSliceSpacing = 2;
	public List<int> LimitedColor;
	public int OnJumpingCube;
	public bool IsPathCircle, IsNearOver;

	public GameData()
	{
		Boxes = new List<BoxInfo>();
		Groups = new List<GroupInfo>();
		TransferSlice = new List<SliceInfo>();
		SurpriseInfos = new List<SurpriseInfo>();
		PathPoints = new List<Vector3>();
		PathSectionIndices = new List<int>();
		UndoData = new UndoData();
		_colorMap = new Dictionary<int, int>();
		LimitedColor = new List<int>();
		Current = this;
	}

#if UNITY_EDITOR
	/// <summary>Detached state for debug search; preserves Current and strips scene references.</summary>
	public GameData CreateDebugSimulationCopy()
	{
		// Do not call the constructor: it replaces the live GameData.Current.
		GameData copy = (GameData)MemberwiseClone();
		copy.UndoData = new UndoData();
		copy.LimitedColor = new List<int>(LimitedColor);
		copy.Groups = Groups.Select(group => new GroupInfo(new GroupData(
			group.Id, group.BoxIndexes, group.LockedByColor)) { IsLocked = group.IsLocked }).ToList();
		copy.Boxes = new List<BoxInfo>(Boxes.Count);
		foreach (BoxInfo box in Boxes)
		{
			var cloned = new BoxInfo(new BoxData(), null)
			{
				Pos = box.Pos, Rotation = box.Rotation, LockColor = box.LockColor,
				NumLock = box.NumLock, LimitedColor = box.LimitedColor,
				IsAdjacentLocked = box.IsAdjacentLocked, IsEnterOnly = box.IsEnterOnly,
				PathIndex = box.PathIndex, State = box.State, LastPopupType = box.LastPopupType,
				IsLastUnlockSecret = box.IsLastUnlockSecret, BoxType = box.BoxType,
				GroupInfo = box.GroupInfo == null ? null : copy.Groups[Groups.IndexOf(box.GroupInfo)],
				CubeInfos = box.CubeInfos.Select(CloneDebugCube).ToList()
			};
			copy.Boxes.Add(cloned);
		}
		copy.TransferSlice = TransferSlice.Select(slice => new SliceInfo(slice.Type,
			slice.SourceBox == null ? null : copy.Boxes[Boxes.IndexOf(slice.SourceBox)])
		{
			PathIndex = slice.PathIndex, IsMoved = slice.IsMoved
		}).ToList();
		copy.SurpriseInfos = SurpriseInfos.Select(info => new SurpriseInfo
		{
			SurpriseType = info.SurpriseType,
			NormalCubes = info.NormalCubes.Select(CloneDebugCube).ToList()
		}).ToList();
		return copy;
	}

	private static CubeInfo CloneDebugCube(CubeInfo cube) => new CubeInfo(cube.Type, cube.Size)
	{
		IsSecret = cube.IsSecret, GroupKeyId = cube.GroupKeyId,
		GroupKeyColor = cube.GroupKeyColor, NumLock = cube.NumLock
	};
#endif

	public GameData(int id, Puzzle puzzle, Dictionary<int, int> colorMap = null) : this()
	{
		Id = id;
		Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
		_colorMap = colorMap == null ? new Dictionary<int, int>() : new Dictionary<int, int>(colorMap);
		InitPath();
		if (puzzle.SurpriseDatas != null)
			SurpriseInfos.AddRange(puzzle.SurpriseDatas.Where(item => item != null)
				.Select(item => new SurpriseInfo(item, _colorMap)));
		if (puzzle.Boxes != null)
		{
			foreach (BoxData boxData in puzzle.Boxes)
			{
				if (boxData == null) continue;
				var box = new BoxInfo(boxData, _colorMap);
				Boxes.Add(box);
				TotalCubeCount += boxData.Cubes == null ? 0 : boxData.Cubes.Count;
				SecretCount += boxData.Cubes == null ? 0 : boxData.Cubes.Count(cube => cube != null && cube.IsSecret > 0);
				if (boxData.LockColor > 0) ColorLockCount++;
				if (boxData.IsEnterOnly > 0) OnlyEnterBoxCount++;
			}
		}
		foreach (BoxInfo box in Boxes) SetBoxPathIndex(box);
		if (puzzle.Groups != null) Groups.AddRange(puzzle.Groups.Where(group => group != null)
			.Select(group => new GroupInfo(group)));
		LimitedColor.AddRange(Boxes.Where(box => box.LimitedColor > 0)
			.Select(box => box.LimitedColor).Distinct());
		InitGroupColorLocks();
	}

	private void InitGroupColorLocks()
	{
		foreach (GroupInfo group in Groups)
			foreach (int index in group.BoxIndexes)
				if (index >= 0 && index < Boxes.Count) Boxes[index].GroupInfo = group;

		foreach (CubeInfo cube in Boxes.SelectMany(box => box.CubeInfos))
		{
			if (cube.GroupKeyId <= 0) continue;
			GroupInfo group = GetGroupInfo(cube.GroupKeyId);
			if (group != null) cube.GroupKeyColor = group.LockedByColor;
		}
	}

	public float GetPathSectionDistance() =>
		(Puzzle != null && Puzzle.PathSectionDistance > 0f
			? Puzzle.PathSectionDistance : DefaultPathSectionDistance) * _roadSpacingScale;

	public void ConfigureRoadSpacing(float scale)
	{
		scale = Math.Max(0.4f, Math.Min(1f, scale));
		if (Math.Abs(scale - _roadSpacingScale) < 0.00001f) return;
		// Preserve physical entry positions when increasing the number of road slots.
		var previousPoints = new List<Vector3>(PathPoints);
		_roadSpacingScale = scale;
		InitPath();
		if (previousPoints.Count == 0 || PathPoints.Count == 0) return;
		foreach (BoxInfo box in Boxes)
		{
			if (box == null || box.PathIndex < 0) continue;
			box.PathIndex = GetNearestPathIndex(previousPoints[box.PathIndex % previousPoints.Count]);
		}
		foreach (SliceInfo slice in TransferSlice)
		{
			if (slice == null || slice.PathIndex < 0) continue;
			slice.PathIndex = GetNearestPathIndex(previousPoints[slice.PathIndex % previousPoints.Count]);
		}
	}
	private void InitPath()
	{
		PathPoints.Clear();
		PathSectionIndices.Clear();
		_pathDistance = 0f;
		IsPathCircle = false;

		List<GamePath> paths = GetPaths();
		if (paths == null || paths.Count == 0) return;

		var sampledPaths = new List<List<Vector3>>(paths.Count);
		var pathDistances = new List<float>(paths.Count);
		foreach (GamePath path in paths)
		{
			List<Vector3> sampled = GetEquidistantPoints(
				path == null ? null : path.ControlPoints, 50);
			float distance = GetPointPathDistance(sampled);
			sampledPaths.Add(sampled);
			pathDistances.Add(distance);
			_pathDistance += distance;
		}

		float sectionDistance = GetPathSectionDistance();
		for (int i = 0; i < sampledPaths.Count; i++)
		{
			int sectionCount = (int)(pathDistances[i] / sectionDistance);
			List<Vector3> sectionPoints = ResamplePoints(sampledPaths[i], sectionCount);
			PathSectionIndices.Add(PathPoints.Count);
			PathPoints.AddRange(sectionPoints);
		}

		IsPathCircle = IsPathCircled();
		if (IsPathCircle && PathPoints.Count > 0)
			PathPoints.RemoveAt(PathPoints.Count - 1);
	}

	private void SetBoxPathIndex(BoxInfo box)
	{
		if (box == null || PathPoints.Count == 0) return;
		box.PathIndex = box.PathIndex < 1
			? GetNearestPathIndex(box.GetJumpOutPos())
			: box.PathIndex - 1;
	}

	private bool IsPathCircled()
	{
		List<GamePath> paths = GetPaths();
		if (paths == null || paths.Count != 1 || paths[0] == null) return false;
		GamePath path = paths[0];
		if (path.ControlPoints == null || path.ControlPoints.Count == 0) return false;
		Vector3 first = path.ControlPoints[0].Position;
		Vector3 last = path.ControlPoints[path.ControlPoints.Count - 1].Position;
		return Vector3.Distance(first, last) < 0.01f || path.IsCircle > 0;
	}

	private static List<Vector3> GetEquidistantPoints(List<ControlPoint> curve, int pointsPerSegment)
	{
		var result = new List<Vector3>();
		if (curve == null || curve.Count < 2) return result;

		for (int i = 0; i < curve.Count - 1; i++)
		{
			ControlPoint current = curve[i];
			ControlPoint next = curve[i + 1];
			int sampleCount = i == curve.Count - 2 ? pointsPerSegment : pointsPerSegment - 1;
			List<Vector3> segment = SampleCubicBezierEquidistant(
				current.Position, current.OutTangent, next.InTangent, next.Position, sampleCount);
			int firstPoint = i == 0 ? 0 : 1;
			for (int point = firstPoint; point < segment.Count; point++) result.Add(segment[point]);
		}
		return result;
	}

	private static List<Vector3> SampleCubicBezierEquidistant(
		Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int pointCount)
	{
		return BezierSampler.SampleCubicBezierEquidistant(p0, p1, p2, p3, pointCount);
	}
	private static float GetPointPathDistance(List<Vector3> points)
	{
		float distance = 0f;
		if (points == null) return distance;
		for (int i = 1; i < points.Count; i++)
			distance += Vector3.Distance(points[i - 1], points[i]);
		return distance;
	}

	private static List<Vector3> ResamplePoints(List<Vector3> points, int count)
	{
		var result = new List<Vector3>();
		if (points == null || points.Count == 0 || count <= 0) return result;
		if (points.Count == 1 || count == 1)
		{
			result.Add(points[0]);
			return result;
		}

		var cumulative = new float[points.Count];
		for (int i = 1; i < points.Count; i++)
			cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);

		float totalDistance = cumulative[cumulative.Length - 1];
		if (totalDistance <= 0.00001f)
		{
			for (int i = 0; i < count; i++) result.Add(points[0]);
			return result;
		}

		int segmentIndex = 0;
		for (int i = 0; i < count; i++)
		{
			float targetDistance = totalDistance * i / (count - 1f);
			while (segmentIndex < cumulative.Length - 2 && cumulative[segmentIndex + 1] < targetDistance)
				segmentIndex++;
			float segmentDistance = cumulative[segmentIndex + 1] - cumulative[segmentIndex];
			float progress = segmentDistance <= 0.00001f ? 0f :
				(targetDistance - cumulative[segmentIndex]) / segmentDistance;
			Vector3 from = points[segmentIndex];
			Vector3 to = points[segmentIndex + 1];
			result.Add(new Vector3(
				from.x + (to.x - from.x) * progress,
				from.y + (to.y - from.y) * progress,
				from.z + (to.z - from.z) * progress));
		}
		return result;
	}

	private int GetNearestPathIndex(Vector3 position)
	{
		int nearestIndex = 0;
		float nearestDistance = float.MaxValue;
		for (int i = 0; i < PathPoints.Count; i++)
		{
			float x = position.x - PathPoints[i].x;
			float z = position.z - PathPoints[i].z;
			float distance = x * x + z * z;
			if (distance >= nearestDistance) continue;
			nearestDistance = distance;
			nearestIndex = i;
		}
		return nearestIndex;
	}
	public int GetRealColorType(int type) { int value; return _colorMap.TryGetValue(type, out value) ? value : -1; }
	public int GetPuzzleActualLevel() => Puzzle == null || Puzzle.InnerIndex < 0 ? -1 : Puzzle.InnerIndex + 1;
	public int GetPuzzleMainLevel() => Puzzle == null ? -1 : Puzzle.MainLevel;
	public HardLevel GetHardLevel() => Puzzle == null ? HardLevel.Normal : (HardLevel)Puzzle.HardLevel;
	public bool CanUndo() => UndoData.CanUndo();

	public bool IsIgnoreBox(int type, BoxInfo boxInfo)
	{
		if (LimitedColor.Contains(type) || IsTheColorBox(boxInfo, type)) return false;
		return GetTheColorBox(type).Count > 0;
	}

	private List<BoxInfo> GetTheColorBox(int type) => Boxes.Where(box => IsTheColorBox(box, type)).ToList();

	private bool IsTheColorBox(BoxInfo box, int type)
	{
		if (box == null || box.IsEmpty() || box.IsFull() || box.HasBlockingLock() ||
			box.IsLongBoxTypeIncompatible(type)) return false;
		CubeInfo top = box.GetTopCube();
		return (top == null || top.NumLock <= 0) && box.CubeInfos.All(cube => cube.Type == type);
	}

	public void CircleTransferSlice(SliceInfo slice)
	{
		if (TransferSlice.Remove(slice)) TransferSlice.Add(slice);
	}

	public bool IsGameCanSpeedUp()
	{
		if (IsGameFailed() || IsGameOver()) return false;
		var seenTypes = new HashSet<int>();
		int missingSlices = 0;
		foreach (BoxInfo box in Boxes.Where(item => !item.IsFinished()))
		{
			if (box.HasSurpriseCube() || box.IsEmpty() || !box.IsSameColorOrEmpty()) continue;
			int type = box.GetTopType();
			if (!seenTypes.Add(type)) continue;
			missingSlices += GameData.GetSliceCapacity(box.BoxType) - box.GetSliceCount();
		}
		return missingSlices <= TransferSlice.Count;
	}

	public bool ShouldForceUnlockAdjacentBox(BoxInfo lockedBox)
	{
		if (lockedBox == null) return false;
		return !Boxes.Any(box => box != lockedBox && (box.IsEmpty() || !box.IsFinished()) &&
			box.CanUnlockAdjacentBox(lockedBox));
	}

	private Dictionary<int, int> GetTransferTypes()
	{
		return TransferSlice.GroupBy(slice => slice.Type)
			.ToDictionary(group => group.Key, group => group.Count());
	}

	public bool IsNoSolution()
	{
		Dictionary<int, int> available = GetTransferTypes();
		if (available.Any(pair => pair.Value >= GetSliceCapacityByType(pair.Key))) return false;

		foreach (BoxInfo box in Boxes.Where(item => !item.IsFinished()))
		{
			foreach (CubeInfo cube in box.GetReachableCubes())
			{
				int count;
				available.TryGetValue(cube.Type, out count);
				count += cube.Size;
				available[cube.Type] = count;
				if (cube.GroupKeyId > 0 || cube.IsSecret || count >= GetSliceCapacityByType(cube.Type))
					return false;
			}
		}

		return true;
	}

	public float GetPathDistance() => _pathDistance;
	public void SetRuntimePathDistance(float distance) => _pathDistance = Math.Max(0f, distance);
	public List<GamePath> GetPaths() => Puzzle == null ? null : Puzzle.Paths;

	public bool AddTransferSlice(SliceInfo slice)
	{
		if (TransferSlice.Contains(slice)) return false;
		TransferSlice.Add(slice);
		return true;
	}

	public bool RemoveTransferSlice(SliceInfo slice) => TransferSlice.Remove(slice);
	// These lists are mutable (including in debug simulations), so read their current
	// entries directly rather than keeping an index that can become stale.
	public GroupInfo GetGroupInfo(int id)
	{
		for (int i = 0; i < Groups.Count; i++)
			if (Groups[i].Id == id) return Groups[i];
		return null;
	}

	public SurpriseInfo GetSurpriseInfo(int type)
	{
		for (int i = 0; i < SurpriseInfos.Count; i++)
			if (SurpriseInfos[i].SurpriseType == type) return SurpriseInfos[i];
		return null;
	}

	public bool TryUnlockGroupColorLock(int id)
	{
		GroupInfo group = GetGroupInfo(id);
		return group != null && group.Unlock();
	}

	public int GetFinishBoxCount() => Boxes.Count(box => box.IsFinished());
	public bool IsGameOver() => TransferSlice.Count == 0 && Boxes.All(box => box.IsFinished());
	public List<MapComponent> GetMapData() => Puzzle == null ? null : Puzzle.MapData;

	public bool IsGameFailed()
	{
		if (IsTransferHaveSpace()) return false;
		IEnumerable<BoxInfo> availableBoxes = Boxes.Where(box =>
			box != null && box.GetSliceCount() < GetSliceCapacity(box.BoxType));
		return !TransferSlice.Any(slice => slice != null &&
			availableBoxes.Any(box => box.IsSliceCanJumpIn(slice)));
	}

	public bool IsTransferHaveSpace() => GetTransferGroup() < GetPathLength();
	public bool CanCubeJumpToTransfer() => OnJumpingCube + GetTransferGroup() < GetPathLength();
	public int GetTransferAvailableGroup() =>
		Math.Max(0, GetPathLength() - GetTransferGroup() - Math.Max(0, OnJumpingCube));
	public int GetTransferGroup() => TransferSlice.GroupBy(slice => slice.Type)
		.Sum(group => (group.Count() + SliceCountPerGroup - 1) / SliceCountPerGroup);
	public float GetPathBusyRate()
	{
		int capacity = GetTransferMaxGroup() * SliceCountPerGroup;
		return capacity <= 0 ? 0f : TransferSlice.Count * 100f / capacity;
	}
	public int GetTransferMaxGroup() => GetPathLength();
	public int GetPathLength() => ExtraPathLength + (Puzzle == null ? 0 : Puzzle.PathLength);
	public int ExtendPath(int groupCount = PathToolExtraGroupCount)
	{
		if (groupCount > 0) ExtraPathLength += groupCount;
		return GetPathLength();
	}
	public bool IsTransferFull() => GetPathLength() <= TransferSlice.Count / SliceCountPerGroup;
	public bool IsWarningState() => GetPathLength() - 1 <= GetTransferGroup();

	public Vector3 GetPosByPathIndex(int index)
	{
		if (PathPoints.Count == 0) return Vector3.zero;
		int wrapped = ((index % PathPoints.Count) + PathPoints.Count) % PathPoints.Count;
		return PathPoints[wrapped] + new Vector3(0f, 0.58f, 0f);
	}

	public static int GetCubeCapacity(BoxType boxType) =>
		boxType == BoxType.Long ? LongBoxCubeCapacity : NormalCubeCapacity;
	public static int GetSliceCapacity(BoxType boxType) => GetCubeCapacity(boxType) * SliceCountPerGroup;
	public static int GetSliceCapacityByType(int type) =>
		(CubeData.IsLongBoxColor(type) ? LongBoxCubeCapacity : NormalCubeCapacity) * SliceCountPerGroup;

	public bool HasSecretCube() => Boxes.Any(box => box.CubeInfos.Any(cube => cube.IsSecret));
	public bool HasColorLock() => Boxes.Any(box => box.LockColor > 0);
	public bool HasLimitedColor() => Boxes.Any(box => box.LimitedColor > 0);
	public bool HasSingleTypeBox() => Boxes.Any(box => box.BoxType == BoxType.Single);
	public bool HasLongTypeBox() => Boxes.Any(box => box.BoxType == BoxType.Long);
	public bool HasGroupColorLock() => Groups.Any(group => group.IsLocked);
	public bool HasAdjacentLock() => Boxes.Any(box => box.IsAdjacentLocked);
	public bool HasEnterOnlyBox() => Boxes.Any(box => box.IsEnterOnly);
	public bool HasCubeNumLock() => Boxes.Any(box => box.CubeInfos.Any(cube => cube.NumLock > 0));
	public bool HasSurpriseCube() => Boxes.Any(box => box.HasSurpriseCube());
	public bool HasNumLockBox() => Boxes.Any(box => box.NumLock > 0);
}
