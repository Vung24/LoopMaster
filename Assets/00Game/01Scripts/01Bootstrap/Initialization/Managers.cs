using Snui;
using Snui.Profile;
using Snui.Team;
using UnityEngine;

public class Managers : MonoBehaviour
{
	public static BaseAvatarManager Avatar { get; set; }
	public static BaseHeartManager Heart { get; set; }
	public static PuzzleStore PuzzleStore { get; set; }
	public static BaseTeamManager Team { get; set; }
	public static SnuiManager SnuiManager { get; set; }
	public static EventManager Event { get; set; }
	public static BasePassManager Pass { get; set; }
	public static Snui.StreakLine.BaseStreakLineManager StreakLine { get; set; }
	public static Snui.Lava.BaseLavaManager Lava { get; set; }
}
