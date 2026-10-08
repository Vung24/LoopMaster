using System;
using System.Collections;
using System.Collections.Generic;

public class GameWinArgs
{
	public const string ChampionArenaScore = "championArenaScore";
	public const string SpecialCollectible = "specialCollectible";
	public const string SpaceRaceStar = "spaceRaceStar";
	public const string KeyHunterDrivers = "keyhunterDrivers";
	public const string PassPoints = "passPoints";
	public const string PowerRushScore = "powerRush";
	public const string TeamGoalScore = "teamGoal";
	public const string TeamBattleScore = "teamBattle";
	public const string SpecialCount = "specialCount";
	public const string ChallengeStar = "challengeStar";
	public const string SurfingRushScore = "surfingRushScore";

	public static bool TryGet(object args, string key, out int intVal)
	{
		intVal = 0;
		if (args == null) return false;

		if (args is IDictionary<string, object> dictObj && dictObj.TryGetValue(key, out var valObj))
		{
			if (valObj is int iv) { intVal = iv; return true; }
			if (int.TryParse(valObj?.ToString(), out int parsed)) { intVal = parsed; return true; }
		}

		if (args is IDictionary<string, int> dictInt && dictInt.TryGetValue(key, out var valInt))
		{
			intVal = valInt;
			return true;
		}

		if (args is IDictionary dict && dict.Contains(key))
		{
			var v = dict[key];
			if (v is int iv) { intVal = iv; return true; }
			if (int.TryParse(v?.ToString(), out int parsed)) { intVal = parsed; return true; }
		}

		return false;
	}
}

