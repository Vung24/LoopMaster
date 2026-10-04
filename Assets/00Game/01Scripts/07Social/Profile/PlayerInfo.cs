using System;

namespace Snui.Profile
{
	[Serializable]
	public class PlayerInfo
	{
		public string Name { get; set; } = "Player";
		public string Avatar;
		public string AvatarFrame;
		public string Token;
		public string NameEffect;
	}
}
