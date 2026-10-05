using System.Collections.Generic;

public class UndoData
{
	public BoxInfo PopupBox;
	public List<SliceInfo> Slices;
	public List<UndoBox> BoxData;
	public int Type;
	public bool IsOnUndoing;

	public UndoData()
	{
		Slices = new List<SliceInfo>();
		BoxData = new List<UndoBox>();
	}

	public bool CanUndo() => !IsOnUndoing && Type >= 0 && PopupBox != null &&
		(Slices.Count > 0 || BoxData.Count > 0);

	public void Init(BoxInfo box) { Clear(); PopupBox = box; }

	public void AddUndo(List<SliceInfo> slices)
	{
		if (slices == null || slices.Count == 0) return;
		Slices.AddRange(slices);
		Type = slices[0].Type;
	}

	public void UpdateUndoBox(SliceInfo slice, BoxInfo box)
	{
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
		PopupBox = null;
		Type = -1;
		Slices.Clear();
		BoxData.Clear();
	}
}
