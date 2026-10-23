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
		if (_data.ActiveLinkedLaunch != null) return false;
		_data.ToolPathUsedCount++;
		_data.ExtendPath();
		return true;
	}

	public int UseShuffleBooster(BoxInfo box = null)
	{
		if (_data.ActiveLinkedLaunch != null) return 0;
		if (_data.Boxes.Any(candidate => candidate != null && candidate.State != BoxState.Free)) return 0;
		if (box == null || !_data.Boxes.Contains(box) || !box.CanShuffle()) return 0;
		if (!box.ShuffleCubes()) return 0;

		_data.UndoData.Clear();
		_data.ToolShuffleUsedCount++;
		return 1;
	}

	public bool UndoLastMove()
	{
		if (_data.UndoData.IsLinked)
		{
			if (!PrepareLinkedUndo(out LinkedCubeUndoPlan plan)) return false;
			return UndoLinked(plan);
		}
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

	public bool PrepareLinkedUndo(out LinkedCubeUndoPlan plan)
	{
		plan = null;
		return _data.UndoData.LinkedRecord != null && _data.UndoData.LinkedRecord.Prepare(out plan);
	}

	public bool UndoLinked(LinkedCubeUndoPlan plan)
	{
		LinkedCubeUndoRecord record = _data.UndoData.LinkedRecord;
		if (record == null || !record.Commit(plan)) return false;
		_data.UndoData.Clear();
		return true;
	}

	public bool CanMove(SliceInfo slice, bool allowOverlap = false)
	{
		return ConveyorRules.CanMove(_data, slice, allowOverlap);
	}

	public bool IsSliceBlocked(SliceInfo slice, SliceInfo other, bool allowOverlap)
	{
		return ConveyorRules.IsSliceBlocked(_data, slice, other, allowOverlap);
	}

	public int GetSliceDistance(SliceInfo from, SliceInfo to)
	{
		return ConveyorRules.GetSliceDistance(_data, from, to);
	}

	public bool RevealTopSecretRun(BoxInfo box)
	{
		if (box == null) return false;
		box.ClearCubes();
		CubeInfo cube = box.PeekTopCube();
		bool revealed = cube != null && cube.IsSecret;
		if (revealed)
		{
			cube.IsSecret = false;
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
		return ConveyorRules.IsBoxBlockIndex(_data, pathIndex);
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
			_data.UnlockGroupColorLock(key.GroupKeyId);
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
