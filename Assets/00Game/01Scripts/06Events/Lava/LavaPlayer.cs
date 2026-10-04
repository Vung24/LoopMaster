using DG.Tweening;
using Snui.Profile;
using UnityEngine;

namespace Snui.Lava
{
	public class LavaPlayer : MonoBehaviour
	{
		public bool IsUser;
		public AudioClip FallClip;
		public AudioClip JumpClip;

		private CanvasGroup _canvasGroup;
		private Snui.Profile.Avatar _avatar;

		public Snui.Profile.Avatar Avatar => _avatar;

		private void Awake()
		{
			_canvasGroup = GetComponent<CanvasGroup>();
			_avatar = GetComponentInChildren<Snui.Profile.Avatar>(true);
		}

		public void SetAlpha(float alpha)
		{
			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = alpha;
			}
		}

		public Tween DOFade(float alpha, float duration)
		{
			return _canvasGroup != null ? _canvasGroup.DOFade(alpha, duration) : null;
		}

		public Tween DOJump(Vector3 endValue, float jumpPower, int numJumps, float duration)
		{
			if (JumpClip != null && GameAudioPlayer.Current != null)
			{
				GameAudioPlayer.Current.PlayOneShot(JumpClip);
			}
			return transform.DOJump(endValue, jumpPower, numJumps, duration);
		}
	}
}