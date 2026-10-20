using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PooledBoxFinishEffect : PooledGameObject
{
	private ParticleSystem[] _particles;
	private Animation[] _animations;
	private Transform _textNode;
	private SpriteRenderer _textRenderer;
	private float _lifetime;
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

	public float Lifetime => _lifetime;

	public void InitializeComponents()
	{
		if (_initialized) return;
		_particles = GetComponentsInChildren<ParticleSystem>(true);
		_animations = GetComponentsInChildren<Animation>(true);
		_textNode = HierarchyReferenceCache.Child(transform, "Text");
		if (_textNode != null)
			_textRenderer = _textNode.GetComponentInChildren<SpriteRenderer>(true);

		_lifetime = 0.5f;
		if (_particles != null)
		{
			for (int i = 0; i < _particles.Length; i++)
			{
				var p = _particles[i];
				if (p == null) continue;
				var main = p.main;
				main.stopAction = ParticleSystemStopAction.None;
				main.loop = false;
				_lifetime = Mathf.Max(_lifetime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
			}
		}
		if (_animations != null)
		{
			for (int i = 0; i < _animations.Length; i++)
			{
				var anim = _animations[i];
				if (anim != null && anim.clip != null)
					_lifetime = Mathf.Max(_lifetime, anim.clip.length);
			}
		}
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
		if (_particles != null)
		{
			for (int i = 0; i < _particles.Length; i++)
			{
				var p = _particles[i];
				if (p != null) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
		}
		if (_textNode != null) _textNode.gameObject.SetActive(false);
		if (_textRenderer != null)
		{
			_textRenderer.sprite = null;
			_textRenderer.gameObject.SetActive(false);
		}
		transform.localPosition = Vector3.zero;
		transform.localRotation = Quaternion.identity;
	}

	public void Play(Vector3 localPosition, bool showText, Sprite sprite, Camera viewCamera)
	{
		InitializeComponents();
		transform.localPosition = localPosition;
		_hasPendingRecycle = false;
		CancelAutoRecycle();
		transform.localRotation = Quaternion.identity;
		gameObject.SetActive(true);

		bool hasText = showText && sprite != null;
		if (_textNode != null)
		{
			_textNode.gameObject.SetActive(hasText);
			if (_textRenderer != null)
			{
				_textRenderer.sprite = sprite;
				if (hasText)
				{
					GameplayEffects.BringComboToFront(_textRenderer);
					Camera cam = viewCamera != null ? viewCamera : Camera.main;
					if (cam != null) _textRenderer.transform.rotation = cam.transform.rotation;
					_textRenderer.gameObject.SetActive(true);
				}
				else
				{
					_textRenderer.gameObject.SetActive(false);
				}
			}
		}

		if (_particles != null)
		{
			for (int i = 0; i < _particles.Length; i++)
			{
				var p = _particles[i];
				if (p == null) continue;
				p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
				if (p.gameObject.activeInHierarchy) p.Play(false);
			}
		}

		if (_animations != null)
		{
			for (int i = 0; i < _animations.Length; i++)
			{
				var anim = _animations[i];
				if (anim != null && anim.clip != null) anim.Play();
			}
		}

		_hasPendingRecycle = true;
		ScheduleAutoRecycle(_lifetime + 0.1f);
	}

	private async UniTaskVoid AutoRecycleAsync(float delay, CancellationToken cancellationToken)
	{
		bool isCancelled = await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken)
			.SuppressCancellationThrow();
		if (isCancelled || this == null) return;
		Recycle();
	}
}

