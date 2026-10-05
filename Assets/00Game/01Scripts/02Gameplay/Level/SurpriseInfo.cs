using System;
using System.Collections.Generic;
using System.Linq;

public class SurpriseInfo
{
	public int SurpriseType;
	public List<CubeInfo> NormalCubes;
	public SurpriseInfo() { NormalCubes = new List<CubeInfo>(); }
	public SurpriseInfo(SurpriseData data, Dictionary<int, int> colorMap) : this()
	{
		if (data == null) return;
		SurpriseType = CubeInfo.MapColor(data.SurpriseType, colorMap);
		if (data.NormalCubes != null)
			NormalCubes.AddRange(data.NormalCubes.Select(cube => new CubeInfo(cube, colorMap)));
	}
}
