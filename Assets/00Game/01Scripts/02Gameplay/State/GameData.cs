using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
	public uint TransferGroupVersion { get; private set; }
	public List<SurpriseInfo> SurpriseInfos;
	public List<Vector3> PathPoints;
	public List<int> PathSectionIndices;
	public UndoData UndoData;
	public int ExtraPathLength;
	private Dictionary<int, int> _colorMap;
	private Dictionary<int, int> _transferTypeCounts = new Dictionary<int, int>();
	private HashSet<int> _speedUpSeenTypes = new HashSet<int>();
	private Dictionary<int, int> _noSolutionCounts = new Dictionary<int, int>();
	private float _pathDistance;
	private float _roadSpacingScale = 1f;
	public float RoadSpacingScale => _roadSpacingScale;
	public int DifferentColorSliceSpacing = 2;
	public List<int> LimitedColor;
	public int OnJumpingCube;
	private List<LinkedCubePair> _linkedPairs = new List<LinkedCubePair>();
	/// <summary>All authored pairs, each with independent launch and Undo state.</summary>
	public IReadOnlyList<LinkedCubePair> LinkedPairs => _linkedPairs;
	/// <summary>First pair for legacy consumers; use the source box's pair for gameplay commands.</summary>
	public LinkedCubePair LinkedPair => _linkedPairs.Count == 0 ? null : _linkedPairs[0];
	internal bool ContainsLinkedPair(LinkedCubePair pair) => _linkedPairs.Contains(pair);
	public LinkedCubeLaunch ActiveLinkedLaunch { get; internal set; }
	public bool IsPathCircle, IsNearOver;

	public bool IsPathSectionIndex(int index) => PathSectionIndices != null && PathSectionIndices.Contains(index);

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
	public GameData CreateDebugSimulationCopy()
	{
		if (ActiveLinkedLaunch != null)
			throw new InvalidOperationException("Wait for the linked launch to finish before copying the board for search.");
		GameData copy = (GameData)MemberwiseClone();
		copy.PathPoints = new List<Vector3>(PathPoints);
		copy.PathSectionIndices = new List<int>(PathSectionIndices);
		copy._transferTypeCounts = new Dictionary<int, int>();
		copy._speedUpSeenTypes = new HashSet<int>();
		copy._noSolutionCounts = new Dictionary<int, int>();
		copy.UndoData = new UndoData();
		copy.ActiveLinkedLaunch = null;
		copy._linkedPairs = new List<LinkedCubePair>(_linkedPairs.Count);
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
		foreach (LinkedCubePair pair in _linkedPairs)
			copy._linkedPairs.Add(new LinkedCubePair(pair.Id,
				copy.Boxes[pair.FirstBoxIndex], pair.FirstBoxIndex,
				copy.Boxes[pair.SecondBoxIndex], pair.SecondBoxIndex, pair.State));
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
		LinkedPairId = cube.LinkedPairId,
		IsSecret = cube.IsSecret, GroupKeyId = cube.GroupKeyId,
		GroupKeyColor = cube.GroupKeyColor, NumLock = cube.NumLock
	};
#endif

	public GameData(int id, Puzzle puzzle, Dictionary<int, int> colorMap = null) : this()
	{
		Id = id;
		Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
		LinkedCubePair.Validate(puzzle);
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
		InitializeLinkedCubes();
	}

	public GameData RecreateFromTemplate()
	{
		if (Puzzle == null) throw new InvalidOperationException("This board has no level template to reload.");
		return new GameData(Id, Puzzle, _colorMap);
	}

	internal void InitializeLinkedCubes()
	{
		if (ActiveLinkedLaunch != null) throw new InvalidOperationException("Cannot rebuild linked cubes during launch.");
		_linkedPairs.Clear();
		var firstMembers = new Dictionary<int, int>();
		var completed = new HashSet<int>();
		foreach (BoxInfo box in Boxes) box.LinkedPair = null;
		for (int b = 0; b < Boxes.Count; b++)
		{
			BoxInfo box = Boxes[b];
			bool hasMember = false;
			foreach (CubeInfo cube in box.CubeInfos)
			{
				if (cube.LinkedPairId == 0) continue;
				int id = cube.LinkedPairId;
				if (id < 0 || hasMember || completed.Contains(id))
					throw new InvalidOperationException("Invalid linked cube IDs in runtime board.");
				hasMember = true;
				if (!firstMembers.TryGetValue(id, out int firstIndex))
				{
					firstMembers.Add(id, b);
					continue;
				}
				BoxInfo firstBox = Boxes[firstIndex];
				if (firstBox.PathIndex == box.PathIndex)
					throw new InvalidOperationException("Linked cubes require distinct path entries.");
				_linkedPairs.Add(new LinkedCubePair(id, firstBox, firstIndex, box, b));
				completed.Add(id);
			}
		}
		if (firstMembers.Count != completed.Count)
			throw new InvalidOperationException("Every linked pair requires two members.");
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
			int sectionIndex = PathPoints.Count;
			PathSectionIndices.Add(sectionIndex);
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
		for (int i = 0; i < Boxes.Count; i++)
			if (IsTheColorBox(Boxes[i], type)) return true;
		return false;
	}

	private bool IsTheColorBox(BoxInfo box, int type)
	{
		if (box == null || box.IsEmpty() || box.IsFull() || box.HasBlockingLock() ||
			box.IsLongBoxTypeIncompatible(type)) return false;
		CubeInfo top = box.GetTopCube();
		if (top != null && top.NumLock > 0) return false;
		for (int i = 0; i < box.CubeInfos.Count; i++)
			if (box.CubeInfos[i].Type != type) return false;
		return true;
	}

	public void CircleTransferSlice(SliceInfo slice)
	{
		if (TransferSlice.Remove(slice)) TransferSlice.Add(slice);
	}

	public bool IsGameCanSpeedUp()
	{
		if (IsGameFailed() || IsGameOver()) return false;
		_speedUpSeenTypes.Clear();
		int missingSlices = 0;
		for (int i = 0; i < Boxes.Count; i++)
		{
			BoxInfo box = Boxes[i];
			if (box.IsFinished()) continue;
			if (box.HasSurpriseCube() || box.IsEmpty() || !box.IsSameColorOrEmpty()) return false;
			int type = box.GetTopType();
			if (!_speedUpSeenTypes.Add(type)) return false;
			missingSlices += GameData.GetSliceCapacity(box.BoxType) - box.GetSliceCount();
		}
		return missingSlices <= TransferSlice.Count;
	}

	public bool ShouldForceUnlockAdjacentBox(BoxInfo lockedBox)
	{
		if (lockedBox == null || Boxes == null) return false;
		for (int i = 0; i < Boxes.Count; i++)
		{
			BoxInfo box = Boxes[i];
			if (box == null || box == lockedBox) continue;
			if ((box.IsEmpty() || !box.IsFinished()) && box.CanUnlockAdjacentBox(lockedBox))
				return false;
		}
		return true;
	}

	public bool IsNoSolution()
	{
		_noSolutionCounts.Clear();
		if (TransferSlice != null)
		{
			for (int i = 0; i < TransferSlice.Count; i++)
			{
				SliceInfo slice = TransferSlice[i];
				if (slice == null) continue;
				int type = slice.Type;
				_noSolutionCounts.TryGetValue(type, out int count);
				count++;
				_noSolutionCounts[type] = count;
				if (count >= GetSliceCapacityByType(type))
					return false;
			}
		}

		if (Boxes != null)
		{
			for (int i = 0; i < Boxes.Count; i++)
			{
				BoxInfo box = Boxes[i];
				if (box == null || box.IsFinished()) continue;

				List<CubeInfo> cubes = box.CubeInfos;
				if (cubes == null) continue;
				for (int j = cubes.Count - 1; j >= 0; j--)
				{
					CubeInfo cube = cubes[j];
					if (cube == null) continue;
					if (cube.NumLock > 0) break;
					if (cube.IsSecret) return false;

					int type = cube.Type;
					_noSolutionCounts.TryGetValue(type, out int count);
					count += cube.Size;
					_noSolutionCounts[type] = count;
					if (cube.GroupKeyId > 0 || count >= GetSliceCapacityByType(type))
						return false;
				}
			}
		}

		return true;
	}

	public float GetPathDistance() => _pathDistance;
	public void SetRuntimePathDistance(float distance) => _pathDistance = Math.Max(0f, distance);
	public List<GamePath> GetPaths() => Puzzle == null ? null : Puzzle.Paths;

	// Direct edits to slice types or same-count list replacements must invalidate HUD data.
	// Gameplay queries deliberately continue reading the public list directly.
	public void InvalidateTransferGroup()
	{
		unchecked { TransferGroupVersion++; }
	}

	public bool AddTransferSlice(SliceInfo slice)
	{
		if (TransferSlice.Contains(slice)) return false;
		TransferSlice.Add(slice);
		InvalidateTransferGroup();
		return true;
	}

	public bool RemoveTransferSlice(SliceInfo slice)
	{
		if (!TransferSlice.Remove(slice)) return false;
		InvalidateTransferGroup();
		return true;
	}
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

	public int GetFinishBoxCount()
	{
		int count = 0;
		for (int i = 0; i < Boxes.Count; i++)
			if (Boxes[i] != null && Boxes[i].IsFinished()) count++;
		return count;
	}
	public bool IsGameOver()
	{
		if (TransferSlice.Count != 0) return false;
		for (int i = 0; i < Boxes.Count; i++)
			if (Boxes[i] != null && !Boxes[i].IsFinished()) return false;
		return true;
	}
	public List<MapComponent> GetMapData() => Puzzle == null ? null : Puzzle.MapData;

	public bool IsGameFailed()
	{
		if (IsTransferHaveSpace()) return false;
		if (TransferSlice.Count == 0) return true;
		for (int boxIndex = 0; boxIndex < Boxes.Count; boxIndex++)
		{
			BoxInfo box = Boxes[boxIndex];
			if (box == null || box.GetSliceCount() >= GetSliceCapacity(box.BoxType)) continue;
			for (int sliceIndex = 0; sliceIndex < TransferSlice.Count; sliceIndex++)
			{
				SliceInfo slice = TransferSlice[sliceIndex];
				if (slice != null && box.IsSliceCanJumpIn(slice)) return false;
			}
		}
		return true;
	}

	public bool IsTransferHaveSpace() => GetTransferGroup() < GetPathLength();
	public bool CanCubeJumpToTransfer() => OnJumpingCube + GetTransferGroup() < GetPathLength();
	public int GetTransferAvailableGroup() =>
		Math.Max(0, GetPathLength() - GetTransferGroup() - Math.Max(0, OnJumpingCube));
	public int GetTransferGroup()
	{
		_transferTypeCounts.Clear();
		for (int i = 0; i < TransferSlice.Count; i++)
		{
			int type = TransferSlice[i].Type;
			_transferTypeCounts.TryGetValue(type, out int count);
			_transferTypeCounts[type] = count + 1;
		}
		int groups = 0;
		foreach (int count in _transferTypeCounts.Values)
			groups = checked(groups + (count + SliceCountPerGroup - 1) / SliceCountPerGroup);
		return groups;
	}
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

	// public bool HasSecretCube() => Boxes.Any(box => box.CubeInfos.Any(cube => cube.IsSecret));
	// public bool HasColorLock() => Boxes.Any(box => box.LockColor > 0);
	// public bool HasLimitedColor() => Boxes.Any(box => box.LimitedColor > 0);
	// public bool HasSingleTypeBox() => Boxes.Any(box => box.BoxType == BoxType.Single);
	// public bool HasLongTypeBox() => Boxes.Any(box => box.BoxType == BoxType.Long);
	// public bool HasGroupColorLock() => Groups.Any(group => group.IsLocked);
	// public bool HasAdjacentLock() => Boxes.Any(box => box.IsAdjacentLocked);
	// public bool HasEnterOnlyBox() => Boxes.Any(box => box.IsEnterOnly);
	// public bool HasCubeNumLock() => Boxes.Any(box => box.CubeInfos.Any(cube => cube.NumLock > 0));
	// public bool HasSurpriseCube() => Boxes.Any(box => box.HasSurpriseCube());
	// public bool HasNumLockBox() => Boxes.Any(box => box.NumLock > 0);
}
