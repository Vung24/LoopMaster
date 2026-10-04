using Cysharp.Threading.Tasks;
using Sugar.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Profile
{
	public class Avatar : MonoBehaviour
	{
		[SerializeField] private Image _mask;
		[SerializeField] private UrlImage _urlImage;
		[SerializeField] private AvatarFrame _frame;

		private void Awake()
		{
			if (_mask == null)
			{
				_mask = GetComponent<Image>();
			}

			if (_urlImage == null)
			{
				_urlImage = GetComponentInChildren<UrlImage>(true);
			}

			if (_frame == null)
			{
				_frame = GetComponentInChildren<AvatarFrame>(true);
			}
		}

		public void SetAvatar(PlayerInfo player)
		{
			if (player == null) return;
			SetAvatar(player.Avatar, player.AvatarFrame);
		}

		public void SetAvatar(string avatar, string avatarFrame)
		{
			if (_urlImage != null && !string.IsNullOrEmpty(avatar))
			{
				_urlImage.SetUrlSpriteAsync(avatar).Forget();
			}

			SetAvatarFrame(avatarFrame);
		}

		public void SetAvatarFrame(string avatarFrame)
		{
			if (_frame != null && !string.IsNullOrEmpty(avatarFrame))
			{
				_frame.SetAvatarFrame(avatarFrame);
			}
		}
	}
}