using System;
using System.Collections.Generic;
using System.Linq;

public enum CoreMoveResult
{
	None = 0,
	Blocked = 1,
	MovedToTransfer = 2,
	MovedToBox = 3,
	BoxFinished = 4,
	GameWon = 5,
	GameFailed = 6
}

public sealed class CoreGameplayEngine
{
	private readonly GameData _data;

	public CoreGameplayEngine(GameData data)
	{
		_data = data ?? throw new ArgumentNullException(nameof(data));
	}

	public GameData Data => _data;

	public bool UsePathBooster()
	{
		_data.ToolPathUsedCount++;
		_data.ExtendPath();
		return true;
	}

	public int UseShuffleBooster()
	{
		if (_data.Boxes.Any(box => box != null && box.State != BoxState.Free)) return 0;
		List<BoxInfo> boxes = _data.Boxes.Where(box => box != null && box.CanShuffle()).ToList();
		if (boxes.Count == 0) return 0;

		_data.UndoData.Clear();
		int shuffledCount = 0;
		foreach (BoxInfo box in boxes)
		{
			if (!box.ShuffleCubes()) continue;
			shuffledCount++;
			_data.ToolShuffleUsedCount++;
		}
		return shuffledCount;
	}

	public bool UndoLastMove()
	{
		UndoData undo = _data.UndoData;
		if (undo == null || !undo.CanUndo() ||
			_data.Boxes.Any(box => box != null && box.State != BoxState.Free))
			return false;

		BoxInfo source = undo.PopupBox;
		int type = undo.Type;
		int restoredSliceCount = 0;
		undo.IsOnUndoing = true;
		try
		{
			foreach (SliceInfo slice in undo.Slices.ToList())
				if (_data.RemoveTransferSlice(slice)) restoredSliceCount++;

			foreach (UndoBox destination in undo.BoxData.ToList())
			{
				if (destination == null || destination.Box == null || destination.Size <= 0)
					continue;
				for (int i = 0; i < destination.Size; i++)
				{
					if (destination.Box.PopupSlice() == null) break;
					restoredSliceCount++;
				}
				destination.Box.ClearCubes();
			}

			for (int i = 0; i < restoredSliceCount; i++)
				if (source.PushSlice(type) == null && source.AddCube(type) == null)
					break;

			_data.ToolUndoUsedCount++;
			return restoredSliceCount > 0;
		}
		finally
		{
			undo.IsOnUndoing = false;
			undo.Clear();
		}
	}

	public CoreMoveResult PopBoxToTransfer(BoxInfo source, out List<SliceInfo> createdSlices)
	{
		createdSlices = new List<SliceInfo>();
		if (source == null || source.State != BoxState.Free || !source.CanPopup() ||
			!_data.CanCubeJumpToTransfer())
			return CoreMoveResult.Blocked;

		List<CubeInfo> topCubes = source.GetTopCubes(false);
		if (topCubes.Count == 0) return CoreMoveResult.Blocked;

		source.State = BoxState.OnPopup;
		_data.OnJumpingCube += topCubes.Count;
		foreach (CubeInfo cube in topCubes)
		{
			if (!_data.IsTransferHaveSpace()) break;
			int count = cube.Size;
			for (int i = 0; i < count; i++)
			{
				var slice = new SliceInfo(cube.Type, source)
				{
					PathIndex = source.PathIndex,
					IsMoved = false
				};
				if (!_data.AddTransferSlice(slice)) continue;
				if (source.PopupSlice() == null)
				{
					_data.RemoveTransferSlice(slice);
					break;
				}
				createdSlices.Add(slice);
			}
		}

		_data.OnJumpingCube -= topCubes.Count;
		source.ClearCubes();
		RevealTopSecretRun(source);
		source.State = BoxState.Free;

		if (source.BoxType == BoxType.Single) _data.UndoData.Clear();
		else
		{
			_data.UndoData.Init(source);
			_data.UndoData.AddUndo(createdSlices);
		}

		return createdSlices.Count > 0 ? CoreMoveResult.MovedToTransfer : CoreMoveResult.Blocked;
	}

	public CoreMoveResult AdvanceTransfer(bool allowOverlap = false)
	{
		if (_data.TransferSlice.Count == 0) return EvaluateEndState();

		List<SliceInfo> ordered = _data.TransferSlice
			.Where(slice => slice.PathIndex >= 0)
			.OrderByDescending(slice => slice.PathIndex)
			.ToList();
		int begin = GetFirstMovableIndex(ordered, allowOverlap);
		if (begin < 0) return EvaluateEndState();

		for (int offset = 0; offset < ordered.Count; offset++)
		{
			SliceInfo slice = ordered[(begin + offset) % ordered.Count];
			if (!CanMove(slice, allowOverlap)) continue;
			slice.PathIndex = WrapPathIndex(slice.PathIndex + 1);
			slice.IsMoved = true;
		}

		List<SliceInfo> arrivals = _data.TransferSlice
			.Where(slice => slice.IsMoved)
			.OrderBy(slice => slice.PathIndex)
			.ToList();
		foreach (SliceInfo slice in arrivals)
		{
			CoreMoveResult result = TryMoveSliceIntoBox(slice);
			if (result == CoreMoveResult.GameWon || result == CoreMoveResult.GameFailed)
				return result;
		}

		return EvaluateEndState();
	}

	public CoreMoveResult TryMoveSliceIntoBox(SliceInfo slice)
	{
		if (slice == null || !slice.IsMoved || !_data.TransferSlice.Contains(slice))
			return CoreMoveResult.None;

		List<BoxInfo> candidates = _data.Boxes.Where(box =>
			box.PathIndex == slice.PathIndex &&
			(box.State == BoxState.Free || box.State == BoxState.OnPush) &&
			box.IsSliceCanJumpIn(slice) && !_data.IsIgnoreBox(slice.Type, box)).ToList();

		if (candidates.Count > 1 && slice.SourceBox != null)
			candidates.RemoveAll(box => box == slice.SourceBox);
		if (candidates.Count == 0) return CoreMoveResult.None;

		BoxInfo target = candidates[0];
		target.State = BoxState.OnPush;
		_data.UndoData.UpdateUndoBox(slice, target);
		CubeInfo pushed = target.PushSlice(slice.Type);
		if (pushed == null)
		{
			target.AddCube(slice.Type);
			pushed = target.GetTopCube();
		}
		if (pushed == null)
		{
			target.State = BoxState.Free;
			return CoreMoveResult.Blocked;
		}

		_data.RemoveTransferSlice(slice);
		ResolveSurpriseBox(target);
		target.State = BoxState.Free;

		if (target.IsFinished())
		{
			HandleFinishedBox(target);
			return _data.IsGameOver() ? CoreMoveResult.GameWon : CoreMoveResult.BoxFinished;
		}
		return CoreMoveResult.MovedToBox;
	}

	public bool CanMove(SliceInfo slice, bool allowOverlap = false)
	{
		if (slice == null || slice.PathIndex < 0 || IsBoxBlockIndex(slice.PathIndex))
			return false;
		List<SliceInfo> transferSlices = _data.TransferSlice;
		for (int i = 0; i < transferSlices.Count; i++)
			if (IsSliceBlocked(slice, transferSlices[i], allowOverlap)) return false;
		return true;
	}

	public bool IsSliceBlocked(SliceInfo slice, SliceInfo other, bool allowOverlap)
	{
		if (slice == null || other == null || slice == other || other.PathIndex < 0) return false;
		int count = _data.PathPoints.Count;
		if (count <= 0) return false;
		int next = slice.PathIndex + 1;
		bool wraps = allowOverlap ? next > count : next >= count;
		if (other.PathIndex <= slice.PathIndex && wraps && other.PathIndex <= next % count)
			return true;
		int spacing = slice.Type == other.Type ? 1 : Math.Max(1, _data.DifferentColorSliceSpacing);
		return slice.PathIndex < other.PathIndex && other.PathIndex <= slice.PathIndex + spacing;
	}
	public int GetSliceDistance(SliceInfo from, SliceInfo to)
	{
		if (from == null || to == null) return 0;
		if (to.PathIndex < from.PathIndex)
			return (_data.PathPoints.Count - from.PathIndex) + to.PathIndex;
		return to.PathIndex - from.PathIndex;
	}

	public bool RevealTopSecretRun(BoxInfo box)
	{
		if (box == null) return false;
		box.ClearCubes();
		int secretType = box.GetTopType();
		bool revealed = false;
		for (int i = box.CubeInfos.Count - 1; i >= 0; i--)
		{
			CubeInfo cube = box.CubeInfos[i];
			if (cube.Type != secretType) break;
			if (!cube.IsSecret) continue;
			cube.IsSecret = false;
			revealed = true;
		}
		box.IsLastUnlockSecret = revealed;
		return revealed;
	}

	public bool ResolveSurpriseBox(BoxInfo box)
	{
		if (box == null) return false;
		box.ClearCubes();
		if (!box.IsSurpriseCubeFinished()) return false;
		SurpriseInfo info = _data.GetSurpriseInfo(box.GetTopType());
		if (info == null || info.NormalCubes.Count != box.CubeInfos.Count) return false;

		for (int i = 0; i < box.CubeInfos.Count; i++)
		{
			CubeInfo target = box.CubeInfos[i];
			CubeInfo replacement = info.NormalCubes[i];
			target.Type = replacement.Type;
			target.IsSecret = replacement.IsSecret;
			target.GroupKeyId = replacement.GroupKeyId;
			target.GroupKeyColor = replacement.GroupKeyColor;
			target.NumLock = replacement.NumLock;
		}
		return true;
	}

	public CoreMoveResult EvaluateEndState()
	{
		if (_data.IsGameOver()) return CoreMoveResult.GameWon;
		if (_data.IsGameFailed()) return CoreMoveResult.GameFailed;
		return CoreMoveResult.None;
	}

	private int GetFirstMovableIndex(List<SliceInfo> slices, bool allowOverlap)
	{
		for (int i = 0; i < slices.Count; i++)
			if (CanMove(slices[i], allowOverlap)) return i;
		return -1;
	}

	private bool IsBoxBlockIndex(int pathIndex)
	{
		List<BoxInfo> boxes = _data.Boxes;
		for (int i = 0; i < boxes.Count; i++)
		{
			BoxInfo box = boxes[i];
			if (box.State == BoxState.OnPopup && box.PathIndex - 1 == pathIndex) return true;
		}
		return false;
	}

	private int WrapPathIndex(int index)
	{
		int count = _data.PathPoints.Count;
		if (count <= 0) return index;
		return ((index % count) + count) % count;
	}

	private void HandleFinishedBox(BoxInfo box)
	{
		_data.LastFinishBoxTime = _data.Duration;
		_data.FinishBoxCount = _data.GetFinishBoxCount();
		_data.UndoData.Clear();

		foreach (CubeInfo key in box.CubeInfos.Where(cube => cube.GroupKeyId > 0).ToList())
		{
			_data.TryUnlockGroupColorLock(key.GroupKeyId);
			key.ClearGroupColorLockKey();
		}

		foreach (BoxInfo target in _data.Boxes)
		{
			if (target.NumLock > 0) target.NumLock--;
			foreach (CubeInfo cube in target.CubeInfos)
				if (cube.NumLock > 0) cube.NumLock--;
			if (target.IsAdjacentLocked && target.CanUnlockAdjacentBox(box))
				target.IsAdjacentLocked = false;
		}
	}
}
