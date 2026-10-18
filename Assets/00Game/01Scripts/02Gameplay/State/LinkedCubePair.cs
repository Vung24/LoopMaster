using System;
using System.Collections.Generic;

public enum LinkedCubePairState { Pending, Launching, Consumed }
public enum LinkedCubeLaunchFailure { None, Busy, BlockedPartner, Capacity, InvalidState }

/// <summary>Level-owned identity and state. Neither cube visuals nor their lifetime own the pair.</summary>
public sealed class LinkedCubePair
{
	public int Id { get; }
	public BoxInfo FirstBox { get; }
	public BoxInfo SecondBox { get; }
	public CubeInfo FirstCube { get; private set; }
	public CubeInfo SecondCube { get; private set; }
	public int FirstBoxIndex { get; }
	public int SecondBoxIndex { get; }
	public LinkedCubePairState State { get; internal set; }

	internal LinkedCubePair(int id, BoxInfo first, int firstIndex, BoxInfo second, int secondIndex,
		LinkedCubePairState state = LinkedCubePairState.Pending)
	{
		Id = id;
		State = state;
		FirstBox = first;
		SecondBox = second;
		FirstBoxIndex = firstIndex;
		SecondBoxIndex = secondIndex;
		RebindMembers();
		first.LinkedPair = this;
		second.LinkedPair = this;
	}

	internal void RebindMembers()
	{
		FirstCube = Member(FirstBox);
		SecondCube = Member(SecondBox);
		if (State != LinkedCubePairState.Consumed && (FirstCube == null || SecondCube == null))
			throw new InvalidOperationException("Linked cube pair " + Id + " lost a member.");
	}

	private CubeInfo Member(BoxInfo box)
	{
		for (int i = 0; i < box.CubeInfos.Count; i++)
			if (box.CubeInfos[i].LinkedPairId == Id) return box.CubeInfos[i];
		return null;
	}

	public bool Contains(BoxInfo box) => box == FirstBox || box == SecondBox;

	public static void Validate(Puzzle puzzle)
	{
		if (puzzle == null || puzzle.Boxes == null) return;
		int pairId = 0, firstBox = -1, memberCount = 0;
		for (int b = 0; b < puzzle.Boxes.Count; b++)
		{
			BoxData box = puzzle.Boxes[b];
			if (box == null || box.Cubes == null) continue;
			foreach (CubeData cube in box.Cubes)
			{
				if (cube == null) continue;
				if (cube.LinkedPairId < 0) throw Invalid("negative pair ID", b);
				if (cube.LinkedPairId == 0) continue;
				if (pairId != 0 && pairId != cube.LinkedPairId) throw Invalid("prototype supports one pair", b);
				if (memberCount > 0 && b == firstBox) throw Invalid("members must be in different boxes", b);
				if (cube.IsSecret != 0 || cube.GroupKeyId != 0 || cube.NumLock != 0 || cube.IsFixed != 0)
					throw Invalid("unsupported linked member", b);
				pairId = cube.LinkedPairId;
				if (memberCount == 0) firstBox = b;
				memberCount++;
			}
		}
		if (memberCount == 0) return;
		if (memberCount != 2) throw Invalid("each pair needs exactly two members", firstBox);
		if (puzzle.Groups != null && puzzle.Groups.Count > 0 || puzzle.Covers != null && puzzle.Covers.Count > 0 ||
			puzzle.SurpriseDatas != null && puzzle.SurpriseDatas.Count > 0)
			throw Invalid("prototype requires an ordinary uncovered board", firstBox);
		for (int b = 0; b < puzzle.Boxes.Count; b++)
		{
			BoxData box = puzzle.Boxes[b];
			if (box == null || box.Cubes == null || box.BoxType != BoxType.Normal ||
				box.LockColor != 0 || box.NumLock != 0 || box.IsAdjacentLocked != 0 || box.IsEnterOnly != 0 ||
				box.LimitedColor != 0 || box.MultiColorLock != null && box.MultiColorLock.Count > 0)
				throw Invalid("prototype requires normal unlocked boxes", b);
			foreach (CubeData cube in box.Cubes)
				if (cube == null || cube.Type <= 0 || cube.Type > 14 || cube.IsSecret != 0 ||
					cube.GroupKeyId != 0 || cube.NumLock != 0 || cube.IsFixed != 0)
					throw Invalid("prototype requires ordinary cubes", b);
		}
	}

	private static InvalidOperationException Invalid(string reason, int boxIndex)
	{
		return new InvalidOperationException("Linked cubes: " + reason + " (box " + boxIndex + ").");
	}
}

/// <summary>One accepted action owns all unissued slices and its busy contribution.</summary>
public sealed class LinkedCubeLaunch
{
	public GameData Data { get; }
	public LinkedCubePair Pair { get; }
	public readonly List<SliceInfo> Created = new List<SliceInfo>();
	private readonly HashSet<SliceInfo> _landing = new HashSet<SliceInfo>();
	private int _firstRemaining, _secondRemaining;
	private readonly int _firstType, _secondType;
	private bool _released;
	public bool IsEmitting => _firstRemaining > 0 || _secondRemaining > 0;
	public bool IsActive => !_released;
	public bool CanComplete => !IsEmitting && _landing.Count == 0;

	private LinkedCubeLaunch(GameData data, LinkedCubePair pair)
	{
		Data = data;
		Pair = pair;
		_firstRemaining = pair.FirstCube.Size;
		_secondRemaining = pair.SecondCube.Size;
		_firstType = pair.FirstCube.Type;
		_secondType = pair.SecondCube.Type;
	}

	public static bool TryBegin(GameData data, BoxInfo source, out LinkedCubeLaunch launch,
		out LinkedCubeLaunchFailure failure)
	{
		launch = null;
		if (!CanBegin(data, source, out failure)) return false;
		LinkedCubePair pair = source.LinkedPair;
		launch = new LinkedCubeLaunch(data, pair);
		data.UndoData.BeginLinked(data, pair);
		data.ActiveLinkedLaunch = launch;
		data.OnJumpingCube += 2;
		pair.State = LinkedCubePairState.Launching;
		pair.FirstBox.State = pair.SecondBox.State = BoxState.OnPopup;
		return true;
	}

	public static bool CanBegin(GameData data, BoxInfo source, out LinkedCubeLaunchFailure failure)
	{
		failure = LinkedCubeLaunchFailure.InvalidState;
		if (data == null || source == null || !data.Boxes.Contains(source)) return false;
		LinkedCubePair pair = source.LinkedPair;
		if (pair == null || pair != data.LinkedPair || !pair.Contains(source)) return false;
		failure = LinkedCubeLaunchFailure.Busy;
		if (data.ActiveLinkedLaunch != null || data.OnJumpingCube != 0 || pair.State == LinkedCubePairState.Launching)
			return false;
		failure = LinkedCubeLaunchFailure.InvalidState;
		CubeInfo top = source.PeekTopCube();
		if (pair.State != LinkedCubePairState.Pending || top == null || top.LinkedPairId != pair.Id) return false;
		if (pair.FirstCube == null || pair.SecondCube == null ||
			pair.FirstCube.LinkedPairId != pair.Id || pair.SecondCube.LinkedPairId != pair.Id ||
			!pair.FirstBox.CubeInfos.Contains(pair.FirstCube) || !pair.SecondBox.CubeInfos.Contains(pair.SecondCube) ||
			pair.FirstBox.PathIndex < 0 || pair.SecondBox.PathIndex < 0 ||
			pair.FirstBox.PathIndex >= data.PathPoints.Count || pair.SecondBox.PathIndex >= data.PathPoints.Count ||
			pair.FirstBox.PathIndex == pair.SecondBox.PathIndex) return false;
		failure = LinkedCubeLaunchFailure.Busy;
		foreach (BoxInfo box in data.Boxes)
			if (box.State != BoxState.Free) return false;
		failure = LinkedCubeLaunchFailure.BlockedPartner;
		if (pair.FirstBox.PeekTopCube() != pair.FirstCube || pair.SecondBox.PeekTopCube() != pair.SecondCube)
			return false;
		failure = LinkedCubeLaunchFailure.InvalidState;
		if (!pair.FirstBox.CanPopup() || !pair.SecondBox.CanPopup() ||
			!IsFullOrdinaryMember(pair.FirstCube) || !IsFullOrdinaryMember(pair.SecondCube))
			return false;
		failure = LinkedCubeLaunchFailure.Capacity;
		if (ProjectedGroups(data) > data.GetPathLength()) return false;
		failure = LinkedCubeLaunchFailure.None;
		return true;
	}

	private static bool IsFullOrdinaryMember(CubeInfo cube)
	{
		return cube.Size == GameData.SliceCountPerGroup && cube.Type >= 1 && cube.Type <= 14 &&
			!cube.IsSecret && cube.NumLock == 0 && cube.GroupKeyId == 0;
	}

	private static int ProjectedGroups(GameData data)
	{
		// Admission requires two full members. Adding S slices of any color increases
		// ceil(colorCount / S) by exactly one, including when both members share a color.
		return checked(data.GetTransferGroup() + 2);
	}

	public SliceInfo Emit(bool second)
	{
		if (_released) throw new InvalidOperationException("Cannot emit a released paired launch.");
		if (second ? _secondRemaining == 0 : _firstRemaining == 0) return null;
		BoxInfo box = second ? Pair.SecondBox : Pair.FirstBox;
		CubeInfo member = second ? Pair.SecondCube : Pair.FirstCube;
		int remaining = second ? _secondRemaining : _firstRemaining;
		int type = second ? _secondType : _firstType;
		if (box.PeekTopCube() != member || member.LinkedPairId != Pair.Id || member.Size != remaining ||
			member.Type != type || box.State != BoxState.OnPopup)
			throw new InvalidOperationException("Linked pair " + Pair.Id + " source changed during emission.");
		var slice = new SliceInfo(member.Type, box);
		if (!Data.AddTransferSlice(slice)) throw new InvalidOperationException("Duplicate linked slice.");
		box.PopupSlice();
		Created.Add(slice);
		_landing.Add(slice);
		Data.UndoData.AddLinkedSlice(slice);
		if (second) _secondRemaining--; else _firstRemaining--;
		if (member.Size == 0)
		{
			box.ClearCubes();
			box.State = BoxState.Free;
		}
		if (!IsEmitting) Pair.State = LinkedCubePairState.Consumed;
		Data.UndoData.CaptureLinkedExpected(box);
		return slice;
	}

	public void ConfirmLanding(SliceInfo slice)
	{
		if (_released) return;
		if (slice == null || slice.PathIndex < 0 || !_landing.Remove(slice))
			throw new InvalidOperationException("Invalid or duplicate linked landing confirmation.");
	}

	public void Complete()
	{
		if (_released) return;
		if (!CanComplete) throw new InvalidOperationException("Paired launch has outstanding emission/landing.");
		Release();
		Data.UndoData.PublishLinked();
	}

	public void Cancel()
	{
		if (_released) return;
		Data.UndoData.Clear();
		Release();
	}

	private void Release()
	{
		if (_released) return;
		if (Data.ActiveLinkedLaunch != this || Data.OnJumpingCube < 2)
		{
			// The count is no longer trustworthy. Retire this owner without altering
			// another owner's count; the caller must discard/reload this broken board.
			_released = true;
			if (Data.ActiveLinkedLaunch == this) Data.ActiveLinkedLaunch = null;
			_landing.Clear();
			throw new InvalidOperationException("Linked launch accounting owner mismatch.");
		}
		_released = true;
		Data.OnJumpingCube -= 2;
		Data.ActiveLinkedLaunch = null;
		_landing.Clear();
	}
}
