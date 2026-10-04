using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Profile
{
	public class AvatarFrameContent : MonoBehaviour
	{
		[SerializeField] private Image _image;
		[SerializeField] private SkeletonGraphic _spine;

		public void SetLock()
		{
			Color lockColor = new Color(0.5f, 0.5f, 0.5f, 1f);
			if (_image != null)
			{
				_image.color = lockColor;
			}

			if (_spine != null)
			{
				_spine.color = lockColor;
				if (_spine.AnimationState != null)
				{
					_spine.AnimationState.ClearTracks();
				}
			}
		}
	}
}