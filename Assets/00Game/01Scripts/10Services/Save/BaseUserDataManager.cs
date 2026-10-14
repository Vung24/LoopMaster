using UnityEngine;

public abstract class BaseUserDataManager : MonoBehaviour, IGameManager
{

	public ManagerStatus status { get; protected set; } = ManagerStatus.Shutdown;

	public virtual int CurrentPuzzleIndex
	{
		get => GetInt(nameof(CurrentPuzzleIndex));
		set => SetInt(nameof(CurrentPuzzleIndex), Mathf.Max(0, value));
	}

	public virtual int NormalPuzzleIndex
	{
		get => GetInt(nameof(NormalPuzzleIndex));
		set => SetInt(nameof(NormalPuzzleIndex), Mathf.Max(0, value));
	}

	public int CurrentLevelPlayCount
	{
		get => GetInt(nameof(CurrentLevelPlayCount));
		set => SetInt(nameof(CurrentLevelPlayCount), Mathf.Max(0, value));
	}

	[System.Obsolete("Use CurrentLevelPlayCount")]
	public int PlayCurrentLevelCount
	{
		get => CurrentLevelPlayCount;
		set => CurrentLevelPlayCount = value;
	}

	public bool IsSoundOn
	{
		get => GetBool(nameof(IsSoundOn), true);
		set => SetBool(nameof(IsSoundOn), value);
	}

	public bool IsMusicOn
	{
		get => GetBool(nameof(IsMusicOn), true);
		set => SetBool(nameof(IsMusicOn), value);
	}

	public bool IsVibrate
	{
		get => GameVibrationHelper.Enabled;
		set
		{
			GameVibrationHelper.SetEnabled(value);
		}
	}

	public bool ColorBlindnessModeSwitch
	{
		get => GetBool(nameof(ColorBlindnessModeSwitch));
		set { SetBool(nameof(ColorBlindnessModeSwitch), value); OnColorBlindChanged(); }
	}

	public bool RateUsEnabled
	{
		get => GetBool(nameof(RateUsEnabled));
		set => SetBool(nameof(RateUsEnabled), value);
	}

	public bool IsAcceptPrivacy
	{
		get => true;
		set { }
	}

	public int FinishLevelCountToday
	{
		get => GetInt(nameof(FinishLevelCountToday));
		set => SetInt(nameof(FinishLevelCountToday), Mathf.Max(0, value));
	}

	public virtual void Awake()
	{
		GameVibrationHelper.Enabled = IsVibrate;
	}

	public abstract void Startup();

	public virtual void ResetAfterSignOut()
	{
	}

	public void DeleteAll()
	{
		string[] keys =
		{
			nameof(CurrentPuzzleIndex), nameof(NormalPuzzleIndex),
			nameof(CurrentLevelPlayCount), nameof(IsSoundOn), nameof(IsMusicOn),
			nameof(IsVibrate), nameof(ColorBlindnessModeSwitch),
			nameof(RateUsEnabled), nameof(FinishLevelCountToday),
			nameof(UserDataManager.HasShowSpeedUpTutorial), nameof(UserDataManager.HasShowFailTutorial),
			nameof(UserDataManager.FeatureTutorialState)
		};
		InventoryManager.Ensure().ClearRewardData();
		LevelBoxManager.ClearOfflineData();
		SpecialStreakManager.ClearOfflineData();
		GameProgressStore.DeleteProgress();
		InventoryManager.Ensure().ResetInventory();
		GamePrefs.DeleteKey(GamePrefKeys.FreeRevive);
		foreach (string key in keys) GamePrefs.DeleteKey(GamePrefKeys.UserData(key));
		GamePrefs.Save();
	}

	public int GetInt(string key, int defaultValue = 0) => GamePrefs.GetInt(GamePrefKeys.UserData(key), defaultValue);
	public void SetInt(string key, int value) { GamePrefs.SetInt(GamePrefKeys.UserData(key), value); GamePrefs.Save(); }
	public bool GetBool(string key, bool defaultValue = false) => GetInt(key, defaultValue ? 1 : 0) != 0;
	public void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);
	public string GetString(string key, string defaultValue = null) => GamePrefs.GetString(GamePrefKeys.UserData(key), defaultValue ?? string.Empty);
	public void SetString(string key, string value) { GamePrefs.SetString(GamePrefKeys.UserData(key), value ?? string.Empty); GamePrefs.Save(); }

	protected virtual void OnLoginDayChanged() { }
	protected virtual void OnColorBlindChanged() { }
	public virtual string GetFeedbackExtraInfo() => string.Empty;
	public virtual string GetFeedbackValueProperties() => string.Empty;
	public virtual string GetFeedbackStatusProperties() => string.Empty;
	public virtual string GetFeedbackTimeProperties() => string.Empty;
}
