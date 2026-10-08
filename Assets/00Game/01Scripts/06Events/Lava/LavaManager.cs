using Snui.Lava;

public class LavaManager : BaseLavaManager
{
	public override int GetUnlockLevel() => 41;
	public override int GetPromptInterval() => 0;
	public override int GetCurSkin() => 2;
	public override int GetRetryInterval() => 900;

	public override void LoadConfig()
	{
		_config = new LavaConfig
		{
			Duration = 86400,
			LevelGoal = 7,
			CoinRewardCount = 10000,
			PlayerCount = 100,
			PromptInterval = 5
		};
	}
}
