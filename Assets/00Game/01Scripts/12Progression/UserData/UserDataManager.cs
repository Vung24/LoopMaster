public class UserDataManager : BaseUserDataManager
{
	public bool PendingLevelChangeAnim { get; set; }
	public HardLevel LastLevelHardTag { get; set; }

	public int FeatureTutorialState
	{
		get => GetInt(nameof(FeatureTutorialState));
		set => SetInt(nameof(FeatureTutorialState), value);
	}

	public bool ProfileTutorialPopped
	{
		get => GetBool(nameof(ProfileTutorialPopped));
		set => SetBool(nameof(ProfileTutorialPopped), value);
	}

	public bool HasShowSpeedUpTutorial
	{
		get => GetBool(nameof(HasShowSpeedUpTutorial));
		set => SetBool(nameof(HasShowSpeedUpTutorial), value);
	}

	public bool HasShowFailTutorial
	{
		get => GetBool(nameof(HasShowFailTutorial));
		set => SetBool(nameof(HasShowFailTutorial), value);
	}

	public bool IsGameSpeedUp
	{
		get => GetBool(nameof(IsGameSpeedUp));
		set => SetBool(nameof(IsGameSpeedUp), value);
	}

	public int PuzzleTestFailCount
	{
		get => GetInt(nameof(PuzzleTestFailCount));
		set => SetInt(nameof(PuzzleTestFailCount), value);
	}

	public bool IsBlueSkin => GetBool(nameof(IsBlueSkin));

	public override int NormalPuzzleIndex
	{
		get => GetInt(nameof(NormalPuzzleIndex));
		set
		{
			SetInt(nameof(NormalPuzzleIndex), UnityEngine.Mathf.Max(0, value));
		}
	}

	public override void Startup()
	{
		status = ManagerStatus.Initializing;
		GameVibrationHelper.Enabled = IsVibrate;
		status = ManagerStatus.Started;
	}
}
