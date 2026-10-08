using UnityEngine;

namespace Snui.Profile
{
	public class AccountManager : MonoBehaviour
	{
		private static AccountManager _instance;
		public static AccountManager Instance => _instance;

		private void Awake()
		{
			_instance = this;
		}

		public static PlayerInfo GetDisplayUserInfo()
		{
			return new PlayerInfo
			{
				Name = "You",
				Avatar = "avatar_1",
				AvatarFrame = Managers.Pass != null && Managers.Pass.HasActiveVip ? AvatarFrame.PASS_VIP : AvatarFrame.DEFAULT
			};
		}
	}
}
