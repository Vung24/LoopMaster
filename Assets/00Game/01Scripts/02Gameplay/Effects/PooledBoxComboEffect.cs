using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;

public class PooledBoxComboEffect : PooledGameObject
{
	private struct ComboVariant
	{
		public int Variant;
		public Transform Transform;
		public SkeletonAnimation Skeleton;
		public MeshRenderer MeshRenderer;
		public Spine.Animation Animation;
	}

	private ComboVariant[] _variants;
	private bool _initialized;
	private CancellationTokenSource _recycleCancellation;
	private bool _hasPendingRecycle;
	private float _remainingLifetime;
	private float _recycleAt;

	private void OnEnable()
	{
		if (_hasPendingRecycle) ScheduleAutoRecycle(_remainingLifetime);
	}

	private void OnDisable()
	{
		if (_hasPendingRecycle) _remainingLifetime = Mathf.Max(0f, _recycleAt - Time.time);
		CancelAutoRecycle();
	}

	private void OnDestroy()
	{
		_hasPendingRecycle = false;
		CancelAutoRecycle();
	}

	private void ScheduleAutoRecycle(float delay)
	{
		_remainingLifetime = delay;
		_recycleAt = Time.time + delay;
		_recycleCancellation = new CancellationTokenSource();
		AutoRecycleAsync(delay, _recycleCancellation.Token).Forget();
	}

	private void CancelAutoRecycle()
	{
		if (_recycleCancellation == null) return;
		_recycleCancellation.Cancel();
		_recycleCancellation.Dispose();
		_recycleCancellation = null;
	}

	public void InitializeComponents()
	{
		if (_initialized) return;
		int count = transform.childCount;
		var variants = new List<ComboVariant>(count);
		for (int i = 0; i < count; i++)
		{
			Transform child = transform.GetChild(i);
			if (child.name.StartsWith("Combo", StringComparison.OrdinalIgnoreCase) &&
				int.TryParse(child.name.Substring(5), out int variantIndex))
			{
				SkeletonAnimation skeleton = child.GetComponent<SkeletonAnimation>();
				MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
				Spine.Animation anim = null;
				if (skeleton != null)
				{
					skeleton.Initialize(false);
					if (skeleton.Skeleton != null && skeleton.Skeleton.Data != null)
						anim = GameUtils.GetSkeletonAnimation(skeleton.Skeleton.Data, "animation");
				}
				variants.Add(new ComboVariant
				{
					Variant = variantIndex,
					Transform = child,
					Skeleton = skeleton,
					MeshRenderer = meshRenderer,
					Animation = anim
				});
				child.gameObject.SetActive(false);
			}
		}
		_variants = variants.ToArray();
		_initialized = true;
	}

	protected override void RegisterRecycleCallback()
	{
		InitializeComponents();
	}

	protected override void BeforeRecycle()
	{
		_hasPendingRecycle = false;
		CancelAutoRecycle();
		if (_variants != null)
		{
			for (int i = 0; i < _variants.Length; i++)
			{
				if (_variants[i].Transform != null)
					_variants[i].Transform.gameObject.SetActive(false);
			}
		}
		transform.localPosition = Vector3.zero;
		transform.localRotation = Quaternion.identity;
	}

	public void Play(Vector3 localPosition, int variant, Camera viewCamera)
	{
		InitializeComponents();
		transform.localPosition = localPosition;
		_hasPendingRecycle = false;
		CancelAutoRecycle();
		transform.localRotation = Quaternion.identity;
		gameObject.SetActive(true);

		float lifetime = 1f;
		if (_variants != null)
		{
			for (int i = 0; i < _variants.Length; i++)
			{
				ref ComboVariant v = ref _variants[i];
				bool selected = v.Variant == variant;
				if (v.Transform != null)
					v.Transform.gameObject.SetActive(selected);

				if (!selected || v.Skeleton == null) continue;

				Camera cam = viewCamera != null ? viewCamera : Camera.main;
				if (cam != null)
					v.Transform.rotation = cam.transform.rotation;

				if (v.Skeleton.AnimationState != null && v.Skeleton.Skeleton != null)
				{
					v.Skeleton.AnimationState.ClearTracks();
					v.Skeleton.Skeleton.SetToSetupPose();
					if (v.Animation != null)
					{
						var entry = v.Skeleton.AnimationState.SetAnimation(0, v.Animation, false);
						entry.MixDuration = 0f;
						lifetime = Mathf.Max(lifetime, entry.Animation.Duration);
					}
					if (v.MeshRenderer != null)
						GameplayEffects.BringComboToFront(v.MeshRenderer);
					v.Skeleton.Update(0f);
					v.Skeleton.LateUpdate();
				}
			}
		}

		_hasPendingRecycle = true;
		ScheduleAutoRecycle(lifetime + 0.1f);
	}

	private async UniTaskVoid AutoRecycleAsync(float delay, CancellationToken cancellationToken)
	{
		bool isCancelled = await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken)
			.SuppressCancellationThrow();
		if (isCancelled || this == null) return;
		Recycle();
	}
}

