using Cysharp.Threading.Tasks;
using UnityEngine;

public class PuzzleStore : MonoBehaviour
{
	public uint PuzzleVersion { get; set; }

	public virtual UniTask<HardLevel> GetCurHardLevel()
	{
		int curLevel = GameProgressStore.NextPlayableLevel;
		if (curLevel > 0 && curLevel % 10 == 0)
		{
			return UniTask.FromResult(HardLevel.VeryHard);
		}
		if (curLevel > 0 && curLevel % 5 == 0)
		{
			return UniTask.FromResult(HardLevel.Hard);
		}
		return UniTask.FromResult(HardLevel.Normal);
	}
}