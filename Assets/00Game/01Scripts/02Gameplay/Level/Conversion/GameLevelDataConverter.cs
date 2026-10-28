using System;
using System.Collections.Generic;

public static class GameLevelDataConverter
{
	public static GameData FromJson(string json, int fallbackLevelNumber = 1,
		Dictionary<int, int> colorMap = null)
	{
		ExportedPuzzleLevel level = ExportedPuzzleJsonDecoder.DecodeLevel(json);
		int levelNumber = level.LevelNumber > 0 ? level.LevelNumber : Math.Max(1, fallbackLevelNumber);
		int puzzleId = level.PuzzleId > 0 ? level.PuzzleId : levelNumber;
		return FromPuzzle(level.Puzzle, puzzleId, levelNumber, colorMap);
	}

	public static GameData FromPuzzle(Puzzle puzzle, int puzzleId, int levelNumber,
		Dictionary<int, int> colorMap = null)
	{
		if (puzzle == null) throw new ArgumentNullException(nameof(puzzle));
		Normalize(puzzle);
		int displayLevel = Math.Max(1, levelNumber);
		puzzle.SetPuzzleIndex(displayLevel, displayLevel - 1);
		return new GameData(puzzleId > 0 ? puzzleId : displayLevel, puzzle, colorMap);
	}

	private static void Normalize(Puzzle puzzle)
	{
		if (puzzle.PathSectionDistance <= 0f) puzzle.PathSectionDistance = 0.2f;
		if (puzzle.Boxes == null) puzzle.Boxes = new List<BoxData>();
		if (puzzle.Paths == null) puzzle.Paths = new List<GamePath>();
		if (puzzle.MapData == null) puzzle.MapData = new List<MapComponent>();
		if (puzzle.Groups == null) puzzle.Groups = new List<GroupData>();
		if (puzzle.Covers == null) puzzle.Covers = new List<RollingCoverData>();
		if (puzzle.SurpriseDatas == null) puzzle.SurpriseDatas = new List<SurpriseData>();

		foreach (BoxData box in puzzle.Boxes)
		{
			if (box == null) continue;
			if (box.Cubes == null) box.Cubes = new List<CubeData>();
			if (box.LinkedCubes == null) box.LinkedCubes = new List<List<int>>();
		}
		foreach (GamePath path in puzzle.Paths)
			if (path != null && path.ControlPoints == null) path.ControlPoints = new List<ControlPoint>();
	}
}
