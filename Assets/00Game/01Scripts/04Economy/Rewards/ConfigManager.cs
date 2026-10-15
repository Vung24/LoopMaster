using UnityEngine;

public class ConfigManager : MonoBehaviour
{
	public int status { get; set; }

	public int GetFinishLevelCoinCount(int hardLevel) => GetFinishLevelCoinCount((HardLevel)hardLevel);

	public static int GetFinishLevelCoinCount(HardLevel hardLevel)
	{
		int difficulty = (int)hardLevel;
		if (difficulty < 0 || difficulty >= 3) return 0;
		return difficulty * 40 + 20;
	}
}
