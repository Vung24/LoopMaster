using System;

namespace Snui.Profile
{
	[Serializable]
	public class PlayerInfo
	{
		[Obsolete]
		public const string AVATAR_FRAME_PASS = "pass";
		[Obsolete]
		public const string NAME_EFFECT_PASS = "pass";

		public string Name { get; set; } = "Player";
		public string Avatar;
		public virtual string AvatarFrame { get; set; }
		public virtual string NameEffects { get; set; }
		public string BadgeToken;
		public string Token;
		public string NameEffect => NameEffects;
	}
}
