using UnityEngine;

namespace Snui.Profile
{
	public class PlayerInfoWidget : MonoBehaviour
	{
		[SerializeField] private Avatar _avatar;
		[SerializeField] private NameEffect _name;
		[SerializeField] private BadgeTokenContainer _badgeTokenContainer;
		[SerializeField] private AvatarFrame _frame;

		private void Awake()
		{
			if (_avatar == null) _avatar = GetComponentInChildren<Avatar>(true);
			if (_name == null) _name = GetComponentInChildren<NameEffect>(true);
			if (_badgeTokenContainer == null) _badgeTokenContainer = GetComponentInChildren<BadgeTokenContainer>(true);
			if (_frame == null) _frame = GetComponentInChildren<AvatarFrame>(true);
		}

		public void SetPlayerInfo(PlayerInfo player)
		{
			if (player == null) return;
			if (_avatar != null) _avatar.SetAvatar(player);
			if (_name != null) _name.SetPlayerInfo(player);
			if (_badgeTokenContainer != null) _badgeTokenContainer.SetToken(player.Token);
			if (_frame != null) _frame.SetAvatarFrame(player.AvatarFrame);
		}
	}
}