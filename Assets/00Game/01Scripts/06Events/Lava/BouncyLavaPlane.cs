using DG.Tweening;
using Spine;
using Spine.Unity;
using UnityEngine;

public class BouncyLavaPlane : Snui.Lava.LavaPlane
{
	[SerializeField] private int _planeIndex;
	[SerializeField] private SkeletonGraphic _splashGraphic;

	public int PlaneIndex
	{
		get => _planeIndex;
		set => _planeIndex = value;
	}

	public override Tween Idle(float delay)
	{
		return null;
	}

	public override void Hit()
	{
	}

	public override void Fall()
	{
		if (_skeletonGraphic != null)
		{
			_skeletonGraphic.Initialize(true);
			var state = _skeletonGraphic.AnimationState;
			if (state != null)
			{
				state.Event -= OnSpineEvent;
				state.Event += OnSpineEvent;
				state.SetAnimation(0, $"water_Pillar_{_planeIndex}", false);
			}
		}
	}

	private void OnSpineEvent(TrackEntry trackEntry, Spine.Event e)
	{
		if (_planeIndex == 6 && e != null && e.Data != null && e.Data.Name == "splash")
		{
			if (_splashGraphic != null)
			{
				_splashGraphic.Initialize(true);
				_splashGraphic.AnimationState?.SetAnimation(0, "animation", false);
			}
		}
	}

	private void OnDestroy()
	{
		if (_skeletonGraphic != null && _skeletonGraphic.AnimationState != null)
		{
			_skeletonGraphic.AnimationState.Event -= OnSpineEvent;
		}
	}
}