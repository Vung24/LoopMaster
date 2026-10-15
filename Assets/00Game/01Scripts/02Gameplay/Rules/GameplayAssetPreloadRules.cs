using System.Collections.Generic;

public static class GameplayAssetPreloadRules
{
	public static List<string> GetRequiredPrefabNames(GameData data)
	{
		var names = new List<string> { "Box", "Cube", "Slice", "CubeFinish", "BoxComboEffect", "PathCombo" };
		if (data == null) return names;
		if (data.PathPoints.Count > 1) { names.Add("GameRoad"); names.Add("BoxArrow"); }
		foreach (BoxInfo box in data.Boxes)
		{
			if (box == null) continue;
			if (box.BoxType == BoxType.Long) { AddRequired(names, "LongColorBox"); AddRequired(names, "CubeLongFinish"); }
			else if (box.IsEnterOnly) AddRequired(names, "OnlyEnterBox");
			else if (box.LimitedColor > 0) AddRequired(names, "LimitedColorBox");
			else if (box.BoxType == BoxType.Single) AddRequired(names, "SingleTypeBox");
			if (box.LockColor > 0) AddRequired(names, "BoxColorLock");
			if (box.NumLock > 0) AddRequired(names, "BoxNumLock");
			if (box.IsAdjacentLocked) AddRequired(names, "BoxAdjacentLock");
			foreach (CubeInfo cube in box.CubeInfos)
			{
				if (cube == null) continue;
				if (cube.GroupKeyId > 0) AddRequired(names, "Key");
				if (cube.NumLock > 0) AddRequired(names, "CubeNumLock");
			}
		}
		foreach (GroupInfo group in data.Groups)
			if (group != null && group.IsLocked) AddRequired(names, "KeyLockBox");
		List<MapComponent> map = data.GetMapData();
		if (map != null)
			foreach (MapComponent item in map)
			{
				string name = GetMapResourceName(item == null ? null : item.Name);
				if (name != null) AddRequired(names, name);
			}
		return names;
	}

	private static void AddRequired(List<string> names, string name)
	{
		if (!names.Contains(name)) names.Add(name);
	}

	public static string GetMapResourceName(string mapItemName)
	{
		if (string.IsNullOrEmpty(mapItemName)) return null;
		switch (mapItemName.Trim().ToLowerInvariant())
		{
			case "arrow":
			case "maparrow": return "MapArrow";
			case "enter":
			case "mapenter": return "MapEnter";
			case "exit":
			case "mapexit": return "MapExit";
			case "capacity": return "Capacity";
			default: return null;
		}
	}
}
