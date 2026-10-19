using System.Collections.Generic;

public class UndoData
{
	public BoxInfo PopupBox;
	public List<SliceInfo> Slices;
	public List<UndoBox> BoxData;
	public int Type;
	public bool IsOnUndoing;
	internal LinkedCubeUndoRecord LinkedRecord { get; private set; }
	public bool IsLinked => LinkedRecord != null;

	public UndoData()
	{
		Slices = new List<SliceInfo>();
		BoxData = new List<UndoBox>();
	}

	public bool CanUndo()
	{
		if (IsOnUndoing) return false;
		if (LinkedRecord != null) return LinkedRecord.CanUndo();
		return Type >= 0 && PopupBox != null && (Slices.Count > 0 || BoxData.Count > 0);
	}

	internal void BeginLinked(GameData data, LinkedCubePair pair)
	{
		var record = new LinkedCubeUndoRecord(data, pair);
		Clear();
		LinkedRecord = record;
	}

	internal void AddLinkedSlice(SliceInfo slice)
	{
		if (LinkedRecord == null) return;
		Slices.Add(slice);
		LinkedRecord.Add(slice);
	}

	internal void CaptureLinkedExpected(BoxInfo box)
	{
		if (LinkedRecord != null) LinkedRecord.CaptureExpected(box);
	}

	internal void PublishLinked()
	{
		if (LinkedRecord != null) LinkedRecord.IsPublished = true;
	}

	public void Init(BoxInfo box) { Clear(); PopupBox = box; }

	public void AddUndo(List<SliceInfo> slices)
	{
		if (slices == null || slices.Count == 0) return;
		Slices.AddRange(slices);
		Type = slices[0].Type;
	}

	public void UpdateUndoBox(SliceInfo slice, BoxInfo box)
	{
		if (LinkedRecord != null)
		{
			if (!LinkedRecord.BeforeArrival(slice, box)) Clear();
			else Slices.Remove(slice);
			return;
		}
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
		LinkedRecord = null;
		PopupBox = null;
		Type = -1;
		Slices.Clear();
		BoxData.Clear();
	}
}
