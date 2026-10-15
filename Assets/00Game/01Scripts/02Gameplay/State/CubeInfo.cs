using System;
using System.Collections.Generic;

public class CubeInfo
{
	public int Type;
	public int Size;
	public bool IsSecret;
	public int GroupKeyId;
	public int GroupKeyColor;
	public int NumLock;
	public Cube CubeSc;

	public CubeInfo(CubeData data, Dictionary<int, int> colorMap)
	{
		if (data == null) throw new ArgumentNullException(nameof(data));
		Type = MapColor(data.Type, colorMap);
		Size = GameData.SliceCountPerGroup;
		IsSecret = data.IsSecret > 0;
		GroupKeyId = data.GroupKeyId;
		NumLock = data.NumLock;
	}

	public CubeInfo(int type, int size) { Type = type; Size = size; }

	public bool ClearGroupColorLockKey()
	{
		if (GroupKeyId <= 0) return false;
		GroupKeyId = 0;
		GroupKeyColor = 0;
		return true;
	}

	public bool IsFull() => Size > GameData.SliceCountPerGroup - 1;

	internal static int MapColor(int type, Dictionary<int, int> colorMap)
	{
		int mapped;
		return colorMap != null && colorMap.TryGetValue(type, out mapped) ? mapped : type;
	}
}
