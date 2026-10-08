using DG.Tweening;
using Spine.Unity;
using UnityEngine;

namespace Snui.Lava
{
	public class LavaPlane : MonoBehaviour
	{
		public int PlaneType;

		[SerializeField] protected SkeletonGraphic _skeletonGraphic;
		protected Vector3 _initialLocalPos;

		public Transform GetPlaneNode() => HierarchyReferenceCache.Child(transform, "Plane");
		public Transform GetTutorialNode() => HierarchyReferenceCache.Child(transform, "Tutorial");
		public Transform GetTopNode() => HierarchyReferenceCache.Child(transform, "Top");
		public Transform GetRewardNode() => HierarchyReferenceCache.Child(transform, "Rewards");

		protected virtual void Awake()
		{
			if (_skeletonGraphic == null)
			{
				TryGetComponent(out _skeletonGraphic);
			}
			_initialLocalPos = transform.localPosition;
		}

		public virtual Tween Idle(float delay)
		{
			if (PlaneType != 1) return null;

			return transform.DOLocalMoveY(_initialLocalPos.y + 9f, 3f)
				.SetDelay(delay)
				.SetEase(Ease.InOutSine)
				.SetLoops(-1, LoopType.Yoyo)
				.SetLink(gameObject);
		}

		public virtual void Hit()
		{
			if (_skeletonGraphic != null)
			{
				_skeletonGraphic.Initialize(true);
				_skeletonGraphic.AnimationState?.SetAnimation(0, "hit", false);
			}
		}

		public virtual void Fall()
		{
			if (_skeletonGraphic != null)
			{
				_skeletonGraphic.Initialize(true);
				_skeletonGraphic.AnimationState?.SetAnimation(0, "fall", false);
			}
		}
	}
}