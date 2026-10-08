using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ConveyorFlow
{
	private readonly IReadOnlyList<Box> _boxes;
	private readonly IReadOnlyList<GameRoad> _roads;
	private readonly IReadOnlyList<PathArrow> _pathArrows;
	private readonly Func<bool> _canTick;
	private readonly Func<Slice, bool> _checkInBox;
	private readonly Action _checkGameOver;
	private GameData Data;
	private CoreGameplayEngine _engine;
	private float _normalTickTime;
	private float _speedTickTime;
	private int _tickCount;
	private float _failureTickInterval;
	// Native UpdateNormalTick adds 0.005s on failure; retain motion at its 0.2s limit.
	private const float FailureTickIncrement = 0.005f;
	private const float MaxFailureTickInterval = 0.2f;

	public bool IsSpeedUpActive { get; private set; }
	public bool IsFailureSlowdownActive => _failureTickInterval > 0f;
	public float FailureTickInterval => _failureTickInterval;
	// Box launch cadence follows manual speed, independently of automatic catch-up.
	public int LaunchTickCount => _tickCount;

	public ConveyorFlow(IReadOnlyList<Box> boxes, IReadOnlyList<GameRoad> roads,
		IReadOnlyList<PathArrow> pathArrows, Func<bool> canTick,
		Func<Slice, bool> checkInBox, Action checkGameOver)
	{
		_boxes = boxes;
		_roads = roads;
		_pathArrows = pathArrows;
		_canTick = canTick;
		_checkInBox = checkInBox;
		_checkGameOver = checkGameOver;
	}

	public void Reset(GameData data, CoreGameplayEngine engine)
	{
		Data = data;
		_engine = engine;
		_normalTickTime = 0f;
		_speedTickTime = 0f;
		_tickCount = 0;
		IsSpeedUpActive = false;
		_failureTickInterval = 0f;
	}

	public void BeginFailureSlowdown(float normalInterval)
	{
		SetSpeedUp(false);
		_failureTickInterval = Mathf.Clamp(normalInterval, 0.02f, MaxFailureTickInterval);
		_normalTickTime = _speedTickTime = 0f;
	}

	public void EndFailureSlowdown()
	{
		_failureTickInterval = 0f;
		_normalTickTime = _speedTickTime = 0f;
	}

	public void SetSpeedUp(bool speedUp)
	{
		if (IsSpeedUpActive == speedUp) return;
		IsSpeedUpActive = speedUp;
		_normalTickTime = 0f;
		_speedTickTime = 0f;
	}

	public void Tick(float delta, float normalInterval, float speedInterval)
	{
		if (Data == null || !_canTick() || delta <= 0f) return;
		_normalTickTime = Mathf.Min(0.5f, _normalTickTime + delta);
		_speedTickTime = Mathf.Min(0.5f, _speedTickTime + delta);
		const float tolerance = 0.000001f;
		while (_canTick())
		{
			float normalTickInterval = IsFailureSlowdownActive ? _failureTickInterval : normalInterval;
			float speedTickInterval = IsFailureSlowdownActive ? _failureTickInterval : speedInterval;
			float normalLag = _normalTickTime - normalTickInterval;
			float speedLag = _speedTickTime - speedTickInterval;
			bool normalDue = normalLag >= -tolerance;
			bool speedDue = speedLag >= -tolerance;
			if (!normalDue && !speedDue) break;
			if (normalDue && (!speedDue || normalLag >= speedLag - tolerance))
			{
				_normalTickTime = Mathf.Max(0f, normalLag);
				UpdateNormalTick(normalTickInterval);
			}
			else
			{
				_speedTickTime = Mathf.Max(0f, speedLag);
				UpdateSpeedUpTick(speedTickInterval);
			}
		}
	}

	private void UpdateNormalTick(float interval)
	{
		if (IsFailureSlowdownActive)
			_failureTickInterval = Mathf.Min(MaxFailureTickInterval, _failureTickInterval + FailureTickIncrement);
		if (!IsSpeedUpActive) _tickCount++;
		UpdatePathWarning();
		UpdateTransferSlice(interval, false);
		if (!IsSpeedUpActive) UpdateTransferArrow(interval);
		_checkGameOver();
	}

	private void UpdateSpeedUpTick(float interval)
	{
		// Launch cadence and arrows still follow the player's selected road speed.
		// Automatic catch-up only changes the selected slices, not box launch timing.
		if (IsSpeedUpActive) _tickCount++;
		UpdateTransferSlice(interval, true);
		if (IsSpeedUpActive) UpdateTransferArrow(interval);
	}

	private void UpdateTransferArrow(float interval)
	{
		if ((_tickCount & 1) != 0) return;
		for (int i = 0; i < _pathArrows.Count; i++)
			if (_pathArrows[i] != null) _pathArrows[i].MoveNextPath(interval * 2f);
	}

	private void UpdateTransferSlice(float interval, bool isSpeedUp)
	{
		if (Data.TransferSlice.Count == 0) return;

		Data.TransferSlice = Data.TransferSlice.Where(slice => slice != null)
			.OrderByDescending(slice => slice.PathIndex).ToList();
		List<SliceInfo> arrivedSlices = Data.TransferSlice.ToList();
		for (int i = 0; i < arrivedSlices.Count; i++)
		{
			SliceInfo info = arrivedSlices[i];
			if (info != null && info.SliceSc != null) _checkInBox(info.SliceSc);
		}

		MoveTransferSlice(interval, isSpeedUp);
	}

	private void MoveTransferSlice(float interval, bool isSpeedUp)
	{
		List<SliceInfo> moveSlices = GetMoveSlice(isSpeedUp);
		if (moveSlices.Count == 0) return;

		int moveIndex = GetSliceBeginMoveIndex(moveSlices);
		bool isOverlapMove = false;
		if (moveIndex < 0)
		{
			isOverlapMove = IsNoBoxBlock();
			moveIndex = 0;
		}

		for (int moved = 0; moved < moveSlices.Count; moved++)
		{
			SliceInfo info = moveSlices[moveIndex];
			if (IsSliceCanMove(info, isOverlapMove) && info.SliceSc != null)
				info.SliceSc.MoveNextPath(interval);

			moveIndex++;
			if (moveIndex >= moveSlices.Count) moveIndex = 0;
		}
	}

	private List<SliceInfo> GetMoveSlice(bool isSpeedUp)
	{
		// Advance the head first so each following slice can use the slot it vacates.
		// The cyclic traversal in MoveTransferSlice also keeps trains packed at the seam.
		List<SliceInfo> moveSlices = Data.TransferSlice.Where(slice => slice != null && slice.PathIndex >= 0)
			.OrderByDescending(slice => slice.PathIndex).ToList();
		List<SliceInfo> speedSlices = GetSpeedUpSlice(moveSlices);
		if (isSpeedUp) return speedSlices;
		var speedSet = new HashSet<SliceInfo>(speedSlices);
		return moveSlices.Where(slice => !speedSet.Contains(slice)).ToList();
	}

	private List<SliceInfo> GetSpeedUpSlice(List<SliceInfo> moveSlices)
	{
		var result = new List<SliceInfo>();
		if (moveSlices == null || moveSlices.Count == 0) return result;
		if (IsSpeedUpActive) return new List<SliceInfo>(moveSlices);
		if (moveSlices.Count < 2) return result;

		int beginIndex = Mathf.Max(0, GetSliceBeginMoveIndex(moveSlices));
		SliceInfo previous = moveSlices[(beginIndex - 1 + moveSlices.Count) % moveSlices.Count];
		var groups = new List<List<SliceInfo>>();
		var groupDistances = new List<int>();
		int collectingType = -1;

		for (int visited = 0; visited < moveSlices.Count; visited++)
		{
			SliceInfo current = moveSlices[beginIndex];
			int distance = GetSliceDistance(current, previous);
			if (current.Type == previous.Type)
			{
				if (distance > 1)
				{
					groups.Add(new List<SliceInfo>());
					groupDistances.Add(distance);
					collectingType = current.Type;
				}
				if (current.Type == collectingType && groups.Count > 0)
					groups[groups.Count - 1].Add(current);
			}
			else collectingType = -1;

			previous = current;
			beginIndex++;
			if (beginIndex >= moveSlices.Count) beginIndex = 0;
		}

		if (moveSlices.All(slice => slice.Type == moveSlices[0].Type))
		{
			if (groups.Count < 2) return result;
			int largestGapIndex = groupDistances.IndexOf(groupDistances.Max());
			groups.RemoveAt(largestGapIndex);
			groupDistances.RemoveAt(largestGapIndex);
		}
		for (int i = 0; i < groups.Count; i++)
			if (groupDistances[i] < 51) result.AddRange(groups[i]);
		return result;
	}

	private int GetSliceBeginMoveIndex(List<SliceInfo> moveSlices)
	{
		if (moveSlices == null) return -1;
		for (int i = 0; i < moveSlices.Count; i++)
			if (IsSliceCanMove(moveSlices[i], false)) return i;
		return -1;
	}

	private bool IsNoBoxBlock()
	{
		return !_boxes.Any(box => box != null && box.Info != null &&
			box.Info.State == BoxState.OnPopup);
	}

	public void UpdatePathWarning()
	{
		if (Data == null) return;
		int warning = Mathf.RoundToInt(Data.GetPathBusyRate());
		for (int i = 0; i < _roads.Count; i++)
			if (_roads[i] != null) _roads[i].SetWarningByProgress(warning);
	}

	private int GetSliceDistance(SliceInfo sliceA, SliceInfo sliceB)
	{
		return _engine == null ? int.MaxValue : _engine.GetSliceDistance(sliceA, sliceB);
	}

	private bool IsSliceCanMove(SliceInfo slice, bool isOverlapMove = false)
	{
		return _engine != null && _engine.CanMove(slice, isOverlapMove);
	}
}
