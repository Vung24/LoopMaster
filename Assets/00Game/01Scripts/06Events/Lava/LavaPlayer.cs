using DG.Tweening;
using Snui.Profile;
using UnityEngine;
using UnityEngine.UI;

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

		public static void SetDisplayAvatar(Transform owner, int index)
		{
			var avatar = owner.GetComponentInChildren<Snui.Profile.Avatar>(true);
			if (avatar == null) return;
			var node = HierarchyReferenceCache.Descendant(avatar.transform, "UrlImage");
			var sprite = LavaSettings.Instance != null ? LavaSettings.Instance.GetAvatarSprite(index) : null;
			if (node == null || sprite == null) return;
			var image = node.GetComponent<Image>() ?? node.gameObject.AddComponent<Image>();
			image.sprite = sprite;
			image.color = Color.white;
			image.raycastTarget = false;
			var frame = HierarchyReferenceCache.Child(avatar.transform, "AvatarFrame");
			if (frame != null && LavaSettings.Instance.AvatarFrameSprite != null)
			{
				var border = frame.GetComponent<Image>() ?? frame.gameObject.AddComponent<Image>();
				border.sprite = LavaSettings.Instance.AvatarFrameSprite;
				border.color = Color.white;
			}
			foreach (var graphic in avatar.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
		}

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
			if (JumpClip != null && AudioManager.Current != null)
			{
				AudioManager.Current.PlayOneShot(JumpClip, gameObject);
			}
			return transform.DOLocalJump(endValue, jumpPower, numJumps, duration);
		}
	}
}
