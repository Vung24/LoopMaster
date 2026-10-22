using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class LinkedCubeLaunch
{
	public GameData Data { get; }
	public LinkedCubePair Pair { get; }
	private readonly List<SliceInfo> _created = new List<SliceInfo>();
	public IReadOnlyList<SliceInfo> Created => _created;
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
		if (pair == null || !data.ContainsLinkedPair(pair) || !pair.Contains(source)) return false;
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
		if (pair.FirstCube.IsSecret || pair.SecondCube.IsSecret) return false;
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
		return checked(data.GetTransferGroup() + 2);
	}

	public SliceInfo Emit(bool second)
	{
		if (_released) throw new InvalidOperationException("Cannot emit a released paired launch.");
		if (second)
			return EmitMember(Pair.SecondBox, Pair.SecondCube, ref _secondRemaining, _secondType);
		return EmitMember(Pair.FirstBox, Pair.FirstCube, ref _firstRemaining, _firstType);
	}

	private SliceInfo EmitMember(BoxInfo box, CubeInfo member, ref int remaining, int type)
	{
		if (remaining == 0) return null;
		if (box.PeekTopCube() != member || member.LinkedPairId != Pair.Id || member.Size != remaining ||
			member.Type != type || box.State != BoxState.OnPopup)
			throw new InvalidOperationException("Linked pair " + Pair.Id + " source changed during emission.");
		var slice = new SliceInfo(member.Type, box);
		if (!Data.AddTransferSlice(slice)) throw new InvalidOperationException("Duplicate linked slice.");
		box.PopupSlice();
		_created.Add(slice);
		_landing.Add(slice);
		Data.UndoData.AddLinkedSlice(slice);
		remaining--;
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
