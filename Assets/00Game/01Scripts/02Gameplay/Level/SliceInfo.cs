public class SliceInfo
{
	public int Type;
	public int PathIndex;
	public Slice SliceSc;
	public BoxInfo SourceBox;
	public bool IsMoved;

	public SliceInfo(int type, BoxInfo sourceBox)
	{
		PathIndex = -1;
		Type = type;
		SourceBox = sourceBox;
	}
}
