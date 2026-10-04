using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Profile
{
	public class ProfileAvatarFrameCell : MonoBehaviour
	{
		public Image Bg;
		public AvatarFrame Frame;
		public GameObject Selector;
		public GameObject Lock;
		public const string LOCK_COLOR = "#C3C3C3";

		public Button Button => GetComponent<Button>();

		public void UpdateStatus(string name)
		{
			bool isUnlocked = Managers.Avatar == null || Managers.Avatar.IsAvatarFrameUnlock(name);
			if (!isUnlocked)
			{
				if (Bg != null)
				{
					Bg.color = ColorUtils.FromString(LOCK_COLOR);
				}
				if (Lock != null)
				{
					Lock.SetActive(true);
				}
			}
			else
			{
				if (Lock != null)
				{
					Lock.SetActive(false);
				}
			}

			if (Frame != null)
			{
				Frame.UpdateFrame(name, !isUnlocked).Forget();
			}
		}
	}
}