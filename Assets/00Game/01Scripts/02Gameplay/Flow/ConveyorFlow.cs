using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ConveyorFlow
{
	private readonly IReadOnlyList<Box> _boxes;
	private readonly IReadOnlyList<GameRoad> _roads;
	private readonly IReadOnlyList<PathArrow> _pathArrows;
	private readonly Func<bool> _canTick;
	private readonly Func<Slice, bool> _checkInBox;
	private readonly Action _checkGameOver;
	private readonly List<SliceInfo> _arrivedSlices = new List<SliceInfo>();
	private readonly List<SliceInfo> _orderedSlices = new List<SliceInfo>();
	private readonly List<SliceInfo> _moveSlices = new List<SliceInfo>();
	private readonly List<SliceInfo> _speedSlices = new List<SliceInfo>();
	private readonly HashSet<SliceInfo> _speedSet = new HashSet<SliceInfo>();
	private readonly List<SliceInfo> _groupSlices = new List<SliceInfo>();
	private readonly List<SpeedGroup> _speedGroups = new List<SpeedGroup>();
	private SliceInfo[] _sortScratch = Array.Empty<SliceInfo>();

	private struct SpeedGroup
	{
		public int Start;
		public int Count;
		public int Distance;
	}
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
		ClearSliceBuffers();
	}

	private void ClearSliceBuffers()
	{
		_arrivedSlices.Clear();
		_orderedSlices.Clear();
		_moveSlices.Clear();
		_speedSlices.Clear();
		_speedSet.Clear();
		_groupSlices.Clear();
		_speedGroups.Clear();
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

		// Keep arrival order stable, including slices sharing a point while launching.
		int retained = 0;
		for (int i = 0; i < Data.TransferSlice.Count; i++)
		{
			SliceInfo info = Data.TransferSlice[i];
			if (info != null) Data.TransferSlice[retained++] = info;
		}
		Data.TransferSlice.RemoveRange(retained, Data.TransferSlice.Count - retained);
		StableSortByPathIndex(Data.TransferSlice);
		_arrivedSlices.Clear();
		_arrivedSlices.AddRange(Data.TransferSlice);
		try
		{
			// Arrival callbacks can remove slices or otherwise change the live list.
			for (int i = 0; i < _arrivedSlices.Count; i++)
			{
				SliceInfo info = _arrivedSlices[i];
				if (info.SliceSc != null) _checkInBox(info.SliceSc);
			}

			MoveTransferSlice(interval, isSpeedUp);
		}
		finally { ClearSliceBuffers(); }
	}

	private void StableSortByPathIndex(List<SliceInfo> slices)
	{
		int count = slices.Count;
		bool sorted = true;
		for (int i = 1; i < count; i++)
			if (slices[i - 1].PathIndex < slices[i].PathIndex) { sorted = false; break; }
		if (sorted) return;
		if (_sortScratch.Length < count)
			Array.Resize(ref _sortScratch, Math.Max(count, Math.Max(16, _sortScratch.Length * 2)));

		// Merge from the left on ties to match LINQ's stable OrderByDescending.
		for (int width = 1; width < count; width *= 2)
		{
			for (int start = 0; start < count; start += width * 2)
			{
				int middle = Math.Min(start + width, count);
				int end = Math.Min(start + width * 2, count);
				int left = start, right = middle;
				for (int target = start; target < end; target++)
					_sortScratch[target] = right >= end ||
						(left < middle && slices[left].PathIndex >= slices[right].PathIndex)
						? slices[left++] : slices[right++];
			}
			for (int i = 0; i < count; i++) slices[i] = _sortScratch[i];
		}
		Array.Clear(_sortScratch, 0, count);
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
		_orderedSlices.Clear();
		for (int i = 0; i < Data.TransferSlice.Count; i++)
		{
			SliceInfo slice = Data.TransferSlice[i];
			if (slice != null && slice.PathIndex >= 0) _orderedSlices.Add(slice);
		}
		// Usually already sorted; still handle mutations made by arrival callbacks.
		StableSortByPathIndex(_orderedSlices);
		List<SliceInfo> speedSlices = GetSpeedUpSlice(_orderedSlices);
		if (isSpeedUp) return speedSlices;
		_speedSet.Clear();
		for (int i = 0; i < speedSlices.Count; i++) _speedSet.Add(speedSlices[i]);
		_moveSlices.Clear();
		for (int i = 0; i < _orderedSlices.Count; i++)
			if (!_speedSet.Contains(_orderedSlices[i])) _moveSlices.Add(_orderedSlices[i]);
		return _moveSlices;
	}

	private List<SliceInfo> GetSpeedUpSlice(List<SliceInfo> moveSlices)
	{
		List<SliceInfo> result = _speedSlices;
		result.Clear();
		_groupSlices.Clear();
		_speedGroups.Clear();
		if (moveSlices == null || moveSlices.Count == 0) return result;
		if (IsSpeedUpActive) { result.AddRange(moveSlices); return result; }
		if (moveSlices.Count < 2) return result;

		int beginIndex = Mathf.Max(0, GetSliceBeginMoveIndex(moveSlices));
		SliceInfo previous = moveSlices[(beginIndex - 1 + moveSlices.Count) % moveSlices.Count];
		int collectingType = -1;
		bool sameType = true;
		int largestGapIndex = -1;

		for (int visited = 0; visited < moveSlices.Count; visited++)
		{
			SliceInfo current = moveSlices[beginIndex];
			if (current.Type != moveSlices[0].Type) sameType = false;
			int distance = GetSliceDistance(current, previous);
			if (current.Type == previous.Type)
			{
				if (distance > 1)
				{
					_speedGroups.Add(new SpeedGroup { Start = _groupSlices.Count, Distance = distance });
					if (largestGapIndex < 0 || distance > _speedGroups[largestGapIndex].Distance)
						largestGapIndex = _speedGroups.Count - 1;
					collectingType = current.Type;
				}
				if (current.Type == collectingType && _speedGroups.Count > 0)
				{
					_groupSlices.Add(current);
					int last = _speedGroups.Count - 1;
					SpeedGroup group = _speedGroups[last];
					group.Count++;
					_speedGroups[last] = group;
				}
			}
			else collectingType = -1;

			previous = current;
			beginIndex++;
			if (beginIndex >= moveSlices.Count) beginIndex = 0;
		}

		if (sameType && _speedGroups.Count < 2) return result;
		for (int i = 0; i < _speedGroups.Count; i++)
		{
			SpeedGroup group = _speedGroups[i];
			if ((sameType && i == largestGapIndex) || group.Distance >= 51) continue;
			for (int member = 0; member < group.Count; member++)
				result.Add(_groupSlices[group.Start + member]);
		}
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
		for (int i = 0; i < _boxes.Count; i++)
		{
			Box box = _boxes[i];
			if (box != null && box.Info != null && box.Info.State == BoxState.OnPopup) return false;
		}
		return true;
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
