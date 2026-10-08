using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Snui.Profile;

public class RacePlayer : PlayerInfo
{
	public int SkillLevel;
}

public class RobotService
{
	private static RobotService _instance;

	public static RobotService Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new RobotService();
			}
			return _instance;
		}
	}

	private static readonly string[] BotNames = new string[]
	{
		"Alex", "Jordan", "Taylor", "Morgan", "Sam", "Chris", "Pat", "Riley",
		"Casey", "Jamie", "Robin", "Logan", "Avery", "Cameron", "Dakota",
		"Skyler", "Jesse", "Reese", "Harper", "Rowan", "Quinn", "Peyton",
		"Kendall", "Emerson", "Finley", "Hayden", "Dallas", "Adrian", "Eden"
	};

	public async UniTask<List<RacePlayer>> LoadRacePlayers(Dictionary<int, int> requireSkills)
	{
		await UniTask.Yield();
		var list = new List<RacePlayer>();
		var rng = new System.Random();
		if (requireSkills != null)
		{
			foreach (var kvp in requireSkills)
			{
				int skill = kvp.Key;
				int count = kvp.Value;
				for (int i = 0; i < count; i++)
				{
					var p = new RacePlayer();
					p.Name = BotNames[rng.Next(BotNames.Length)] + rng.Next(10, 99);
					p.SkillLevel = skill;
					p.Avatar = "avatar_" + rng.Next(1, 10);
					list.Add(p);
				}
			}
		}
		return list;
	}
}