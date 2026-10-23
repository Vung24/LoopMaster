using System.Collections.Generic;

public sealed class LinkedCubeUndoEntry
{
	public int Id { get; internal set; }
	public SliceInfo Slice { get; internal set; }
	public BoxInfo Source { get; internal set; }
	public BoxInfo Destination { get; internal set; }
	public int Color { get; internal set; }
}

public sealed class LinkedCubeUndoPlan
{
	internal LinkedCubeUndoRecord Owner;
	public IReadOnlyList<LinkedCubeUndoEntry> Entries { get; internal set; }
	public IReadOnlyList<BoxInfo> Boxes { get; internal set; }
	public LinkedCubePair Pair { get; internal set; }
}

internal sealed class LinkedCubeUndoRecord
{
	private sealed class BoxSnapshot
	{
		internal readonly List<CubeInfo> Cubes = new List<CubeInfo>();
		private readonly int _lastPopupType;
		private readonly bool _lastUnlockSecret;

		internal BoxSnapshot(BoxInfo box)
		{
			foreach (CubeInfo cube in box.CubeInfos) Cubes.Add(Clone(cube));
			_lastPopupType = box.LastPopupType;
			_lastUnlockSecret = box.IsLastUnlockSecret;
		}

		internal bool Matches(BoxInfo box)
		{
			if (box.CubeInfos.Count != Cubes.Count || box.LastPopupType != _lastPopupType ||
				box.IsLastUnlockSecret != _lastUnlockSecret) return false;
			for (int i = 0; i < Cubes.Count; i++)
			{
				CubeInfo a = Cubes[i], b = box.CubeInfos[i];
				if (a == null || b == null)
				{
					if (a != b) return false;
					continue;
				}
				if (a.Type != b.Type || a.Size != b.Size || a.LinkedPairId != b.LinkedPairId ||
					a.IsSecret != b.IsSecret || a.NumLock != b.NumLock || a.GroupKeyId != b.GroupKeyId ||
					a.GroupKeyColor != b.GroupKeyColor) return false;
			}
			return true;
		}

		internal void Restore(BoxInfo box, List<CubeInfo> prepared)
		{
			box.CubeInfos = prepared;
			box.LastPopupType = _lastPopupType;
			box.IsLastUnlockSecret = _lastUnlockSecret;
			box.State = BoxState.Free;
		}

		internal static CubeInfo Clone(CubeInfo cube)
		{
			if (cube == null) return null;
			return new CubeInfo(cube.Type, cube.Size)
			{
				LinkedPairId = cube.LinkedPairId, IsSecret = cube.IsSecret,
				NumLock = cube.NumLock, GroupKeyId = cube.GroupKeyId, GroupKeyColor = cube.GroupKeyColor
			};
		}
	}

	private readonly GameData _data;
	private readonly LinkedCubePair _pair;
	private readonly List<LinkedCubeUndoEntry> _entries = new List<LinkedCubeUndoEntry>();
	private readonly List<BoxInfo> _boxes = new List<BoxInfo>();
	private readonly Dictionary<BoxInfo, BoxSnapshot> _before = new Dictionary<BoxInfo, BoxSnapshot>();
	private readonly Dictionary<BoxInfo, BoxSnapshot> _expected = new Dictionary<BoxInfo, BoxSnapshot>();
	internal bool IsPublished;

	internal LinkedCubeUndoRecord(GameData data, LinkedCubePair pair)
	{
		_data = data;
		_pair = pair;
		Touch(pair.FirstBox);
		Touch(pair.SecondBox);
	}

	private void Touch(BoxInfo box)
	{
		if (_before.ContainsKey(box)) return;
		_boxes.Add(box);
		_before.Add(box, new BoxSnapshot(box));
		CaptureExpected(box);
	}

	internal void CaptureExpected(BoxInfo box)
	{
		if (_before.ContainsKey(box)) _expected[box] = new BoxSnapshot(box);
	}

	internal void Add(SliceInfo slice)
	{
		_entries.Add(new LinkedCubeUndoEntry
		{
			Id = _entries.Count, Slice = slice, Source = slice.SourceBox, Color = slice.Type
		});
	}

	internal bool BeforeArrival(SliceInfo slice, BoxInfo box)
	{
		LinkedCubeUndoEntry entry = null;
		for (int i = 0; i < _entries.Count; i++)
			if (_entries[i].Slice == slice) { entry = _entries[i]; break; }
		if (entry == null) return !_before.ContainsKey(box);
		if (_expected.TryGetValue(box, out BoxSnapshot expected) && !expected.Matches(box)) return false;
		Touch(box);
		entry.Destination = box;
		return true;
	}

	internal bool CanUndo()
	{
		if (!IsPublished || _data.ActiveLinkedLaunch != null || _data.OnJumpingCube != 0 || _pair.State != LinkedCubePairState.Consumed ||
			_entries.Count != GameData.SliceCountPerGroup * 2) return false;
		foreach (BoxInfo box in _data.Boxes)
			if (box.State != BoxState.Free) return false;
		foreach (BoxInfo box in _boxes)
			if (!_data.Boxes.Contains(box) || !_expected[box].Matches(box)) return false;
		foreach (LinkedCubeUndoEntry entry in _entries)
		{
			bool onTransfer = _data.TransferSlice.Contains(entry.Slice);
			if (entry.Destination == null && !onTransfer || entry.Destination != null && onTransfer) return false;
		}
		return true;
	}

	internal bool Prepare(out LinkedCubeUndoPlan plan)
	{
		plan = null;
		if (!CanUndo()) return false;
		plan = new LinkedCubeUndoPlan { Owner = this, Entries = _entries.AsReadOnly(), Boxes = _boxes.AsReadOnly(), Pair = _pair };
		return true;
	}

	internal bool Commit(LinkedCubeUndoPlan plan)
	{
		if (plan == null || plan.Owner != this || !CanUndo()) return false;
		// Prepare all allocations first. The remaining commit only replaces already validated state.
		var restored = new Dictionary<BoxInfo, List<CubeInfo>>();
		foreach (BoxInfo box in _boxes)
		{
			var cubes = new List<CubeInfo>(_before[box].Cubes.Count);
			foreach (CubeInfo cube in _before[box].Cubes) cubes.Add(BoxSnapshot.Clone(cube));
			restored.Add(box, cubes);
		}
		foreach (LinkedCubeUndoEntry entry in _entries) _data.RemoveTransferSlice(entry.Slice);
		foreach (BoxInfo box in _boxes) _before[box].Restore(box, restored[box]);
		_pair.State = LinkedCubePairState.Pending;
		_pair.RebindMembers();
		_data.ToolUndoUsedCount++;
		return true;
	}
}
