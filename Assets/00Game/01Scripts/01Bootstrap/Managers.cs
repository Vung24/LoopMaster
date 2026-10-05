using Snui.Profile;
using Snui.Team;
using UnityEngine;

public class Managers : MonoBehaviour
{
	public static BaseAvatarManager Avatar { get; set; }
	public static BaseHeartManager Heart { get; set; }
	public static PuzzleStore PuzzleStore { get; set; }
	public static BaseTeamManager Team { get; set; }
}