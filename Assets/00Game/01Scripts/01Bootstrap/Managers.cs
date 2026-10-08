using Snui;
using Snui.ChampionArena;
using Snui.Profile;
using Snui.SpecialCol;
using Snui.Team;
using UnityEngine;

public class Managers : MonoBehaviour
{
	public static BaseAvatarManager Avatar { get; set; }
	public static BaseHeartManager Heart { get; set; }
	public static PuzzleStore PuzzleStore { get; set; }
	public static BaseTeamManager Team { get; set; }
	public static BaseDailyGiftManager DailyGift { get; set; }
	public static BaseDailyGiftManager DailyGiftManager => DailyGift;
	public static SnuiManager SnuiManager { get; set; }
	public static BaseChampionArenaManager ChampionArena { get; set; }
	public static EventManager Event { get; set; }
	public static BasePassManager Pass { get; set; }
	public static BaseSpecialCollectibleManager SpecialCol { get; set; }
	public static Snui.StreakLine.BaseStreakLineManager StreakLine { get; set; }
	public static Snui.SpaceRace.BaseSpaceRaceManager SpaceRace { get; set; }
	public static Snui.Lava.BaseLavaManager Lava { get; set; }
}
