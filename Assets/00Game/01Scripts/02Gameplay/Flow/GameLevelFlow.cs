
public sealed class GameLevelFlow
{
	private readonly GameController _controller;
	private readonly BuiltinPuzzleLoader _loader;
	private Puzzle _template;
	private int _puzzleId;

	public GameLevelFlow(GameController controller, BuiltinPuzzleLoader loader)
	{
		_controller = controller;
		_loader = loader;
	}

	public void Capture(GameData data)
	{
		if (data == null) return;
		_template = data.Puzzle;
		_puzzleId = data.Id;
	}

	public bool HasNext(int currentLevel) => _loader != null &&
		_loader.HasExactLevel(currentLevel + 1);

	public bool LoadNext(int currentLevel)
	{
		return _loader != null && currentLevel > 0 &&
			_loader.LoadExactLevel(currentLevel + 1, _controller);
	}

	public bool Replay(GameData currentData, int level)
	{
		if (currentData == null || level < 1 || GameProgressStore.IsLevelCompleted(level)) return false;
		if (_loader != null && _loader.HasExactLevel(level) &&
			_loader.LoadExactLevel(level, _controller)) return true;
		if (_template == null) return false;

		_controller.Initialize(GameLevelDataConverter.FromPuzzle(
			_template, _puzzleId, level));
		return true;
	}
}
