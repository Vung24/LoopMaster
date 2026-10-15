using System;
using System.Collections.Generic;

public class GroupInfo
{
	public int Id;
	public int LockedByColor;
	public List<int> BoxIndexes;
	public bool IsLocked;

	public GroupInfo(GroupData data)
	{
		if (data == null) throw new ArgumentNullException(nameof(data));
		BoxIndexes = data.BoxIndexes == null ? new List<int>() : new List<int>(data.BoxIndexes);
		IsLocked = true;
		Id = data.Id;
		LockedByColor = data.LockedByColor;
	}

	public bool Unlock() { bool oldValue = IsLocked; IsLocked = false; return oldValue; }
}
