using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BoosterOperations
{
	private readonly GameData _data;

	public BoosterOperations(GameData data)
	{
		_data = data ?? throw new ArgumentNullException(nameof(data));
	}

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
}
