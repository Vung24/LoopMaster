using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GameplaySceneBuilder
{
	private readonly List<Box> _boxes = new List<Box>();
	private readonly Dictionary<BoxInfo, Box> _boxesByInfo = new Dictionary<BoxInfo, Box>();
	private readonly List<GameRoad> _roads = new List<GameRoad>();
	private readonly List<PathArrow> _pathArrows = new List<PathArrow>();
	private readonly List<GameObject> _mapItems = new List<GameObject>();
	private readonly Dictionary<int, GroupRollDoor> _groupRollDoors = new Dictionary<int, GroupRollDoor>();
	private Capacity _capacityIndicator;
	private GameData Data;
	private GameLoader _gameLoader;
	private Transform GameRoot;

	public IReadOnlyList<Box> Boxes => _boxes;
	public IReadOnlyList<GameRoad> Roads => _roads;
	public IReadOnlyList<PathArrow> PathArrows => _pathArrows;

	public void Build(GameData data, GameLoader loader, Transform gameRoot, Action onMapCreated)
	{
		Data = data;
		_gameLoader = loader;
		GameRoot = gameRoot;
		CreateRoad();
		CreateMap(onMapCreated);
		CreateBoxes();
		CreateGroupRollDoors();
		CreateArrow();
	}

	private void CreateRoad()
	{
		List<GamePath> paths = Data.GetPaths();
		if (paths == null || _gameLoader.GameRoad == null) return;
		for (int i = 0; i < paths.Count; i++)
		{
			GameObject roadObject = Object.Instantiate(_gameLoader.GameRoad, GameRoot);
			GameRoad road = roadObject.GetComponent<GameRoad>() ?? roadObject.AddComponent<GameRoad>();
			road.InitCurve(paths[i]);
			_roads.Add(road);
		}
	}

	private void CreateMap(Action onMapCreated)
	{
		List<MapComponent> mapData = Data.GetMapData();
		if (mapData == null) return;
		for (int i = 0; i < mapData.Count; i++)
		{
			MapComponent item = mapData[i];
			if (item == null || string.IsNullOrEmpty(item.Name)) continue;
			GameObject prefab = _gameLoader.GetMapPrefab(item.Name);
			if (prefab == null) continue;
			GameObject instance = Object.Instantiate(prefab, GameRoot);
			instance.name = item.Name;
			instance.transform.localPosition = item.Position;
			instance.transform.localRotation = Quaternion.Euler(item.Rotation);
			instance.transform.localScale = item.Scale == Vector3.zero ? Vector3.one : item.Scale;
			_mapItems.Add(instance);
			Capacity capacity = instance.GetComponent<Capacity>();
			if (capacity != null) _capacityIndicator = capacity;
		}
		onMapCreated?.Invoke();
	}

	private void CreateBoxes()
	{
		for (int i = 0; i < Data.Boxes.Count; i++)
		{
			BoxInfo info = Data.Boxes[i];
			GameObject prefab = GetBoxObject(info);
			if (prefab == null) continue;
			GameObject boxObject = Object.Instantiate(prefab, GameRoot);
			Box box = boxObject.GetComponent<Box>() ?? boxObject.AddComponent<Box>();
			box.Init(info, _gameLoader);
			_boxes.Add(box);
			_boxesByInfo[info] = box;
		}
	}

	private GameObject GetBoxObject(BoxInfo box)
	{
		if (box == null) return null;
		if (box.BoxType == BoxType.Long) return _gameLoader.LongBox != null ? _gameLoader.LongBox : _gameLoader.Box;
		if (box.IsEnterOnly && _gameLoader.BoxEnterOnly != null) return _gameLoader.BoxEnterOnly;
		if (box.LimitedColor > 0 && _gameLoader.LimitedColorBox != null) return _gameLoader.LimitedColorBox;
		if (box.BoxType == BoxType.Single) return _gameLoader.SingleTypeBox != null ? _gameLoader.SingleTypeBox : _gameLoader.Box;
		return _gameLoader.Box;
	}

	private void CreateGroupRollDoors()
	{
		if (_gameLoader.GroupColorLock == null) return;
		foreach (GroupInfo group in Data.Groups)
		{
			if (group == null || !group.IsLocked) continue;
			var groupBoxes = new List<Box>();
			for (int i = 0; i < group.BoxIndexes.Count; i++)
			{
				int index = group.BoxIndexes[i];
				if (index >= 0 && index < _boxes.Count) groupBoxes.Add(_boxes[index]);
			}
			if (groupBoxes.Count == 0) continue;
			GameObject doorObject = new GameObject("GroupRollDoors" + group.Id);
			doorObject.transform.SetParent(GameRoot, false);
			GroupRollDoor door = doorObject.AddComponent<GroupRollDoor>();
			door.Init(group, groupBoxes, _gameLoader.GroupColorLock);
			_groupRollDoors[group.Id] = door;
		}
	}

	private void CreateArrow()
	{
		if (_gameLoader.RoadArrow == null || Data.PathPoints.Count == 0) return;
		int pointCount = Data.PathPoints.Count;
		int arrowCount = Mathf.FloorToInt(Data.GetPathDistance() * 0.25f);
		if (arrowCount <= 0) return;

		for (int i = 0; i < arrowCount; i++)
		{
			int pathIndex = Mathf.FloorToInt(i * pointCount / (float)arrowCount);
			GameObject arrowObject = Object.Instantiate(_gameLoader.RoadArrow, GameRoot);
			PathArrow arrow = arrowObject.GetComponent<PathArrow>() ?? arrowObject.AddComponent<PathArrow>();
			arrow.Init(pathIndex);
			_pathArrows.Add(arrow);
		}
	}

	public Box GetBox(BoxInfo info)
	{
		return info != null && _boxesByInfo.TryGetValue(info, out Box box) && box != null ? box : null;
	}

	public void UpdateCapacity(int used, int maximum)
	{
		if (_capacityIndicator != null) _capacityIndicator.UpdateCapacity(used, maximum);
	}

	public bool TryGetGroupRollDoor(int groupId, out GroupRollDoor door) =>
		_groupRollDoors.TryGetValue(groupId, out door);

	public void RemoveGroupRollDoor(int groupId, GroupRollDoor expectedDoor)
	{
		if (_groupRollDoors.TryGetValue(groupId, out GroupRollDoor door) && door == expectedDoor)
			_groupRollDoors.Remove(groupId);
	}

	public void Clear(Transform gameRoot)
	{
		_boxes.Clear();
		_boxesByInfo.Clear();
		_roads.Clear();
		_pathArrows.Clear();
		_mapItems.Clear();
		_groupRollDoors.Clear();
		_capacityIndicator = null;
		Data = null;
		_gameLoader = null;
		GameRoot = null;
		if (gameRoot == null) return;
		for (int i = gameRoot.childCount - 1; i >= 0; i--)
		{
			GameObject child = gameRoot.GetChild(i).gameObject;
			child.SetActive(false);
			Object.Destroy(child);
		}
	}
}
