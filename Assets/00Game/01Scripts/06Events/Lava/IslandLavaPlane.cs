using Spine.Unity;
using UnityEngine;

namespace Snui.Lava
{
	public class IslandLavaPlane : LavaPlane
	{
		public override void Hit()
		{
		}

		public override void Fall()
		{
			if (_skeletonGraphic != null)
			{
				_skeletonGraphic.Initialize(true);
				_skeletonGraphic.AnimationState?.SetAnimation(0, "loop", false);
			}
		}
	}
}