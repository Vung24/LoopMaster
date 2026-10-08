namespace Snui
{
	public enum EventState
	{
		Lock = 0,
		WaitingStart = 1,
		Ongoing = 2,
		Win = 3,
		Fail = 4,
		Expire = 5,
		CD = 6,
		Close = 7
	}

	public enum RaceTutorialStage
	{
		ForceShowPrompt = 0,
		ShowInfoDialog = 1,
		End = 2
	}
}
