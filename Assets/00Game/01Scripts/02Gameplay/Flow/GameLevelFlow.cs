public interface ILevelDataSource
{
	bool HasExactLevel(int level);
	bool TryCreateExactLevel(int level, out GameData data);
}

public sealed class GameLevelFlow
{
	private readonly ILevelDataSource _source;
	private Puzzle _template;
	private int _puzzleId;

	public GameLevelFlow(ILevelDataSource source)
	{
		_source = source;
	}

	public void Capture(GameData data)
	{
		if (data == null) return;
		_template = data.Puzzle;
		_puzzleId = data.Id;
	}

	public bool HasNext(int currentLevel) => _source != null && currentLevel > 0 &&
		currentLevel < int.MaxValue && _source.HasExactLevel(currentLevel + 1);

	public bool TryCreateNext(int currentLevel, out GameData data)
	{
		data = null;
		if (_source == null || currentLevel < 1 || currentLevel == int.MaxValue) return false;
		GameData previous = GameData.Current;
		try { return _source.TryCreateExactLevel(currentLevel + 1, out data); }
		finally { GameData.SetCurrent(previous); }
	}

	public bool TryCreateReplay(GameData currentData, int level, out GameData data)
	{
		data = null;
		if (currentData == null || level < 1) return false;
		GameData previous = GameData.Current;
		try
		{
			if (_source != null && _source.HasExactLevel(level) &&
				_source.TryCreateExactLevel(level, out data)) return true;
#if UNITY_EDITOR
            if (GamePrefs.EditorCampaignLoader != null) return false;
#endif
			if (_template == null) return false;
			data = GameLevelDataConverter.FromPuzzle(_template, _puzzleId, level);
			return true;
		}
		finally { GameData.SetCurrent(previous); }
	}
}
