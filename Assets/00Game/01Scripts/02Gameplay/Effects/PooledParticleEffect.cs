using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PooledParticleEffect : PooledGameObject
{
	private ParticleSystem[] _particles;
	private Animation[] _animations;
	private float _lifetime;
	private bool _initialized;

	public float Lifetime => _lifetime;

	public void InitializeComponents()
	{
		if (_initialized) return;
		_particles = GetComponentsInChildren<ParticleSystem>(true);
		_animations = GetComponentsInChildren<Animation>(true);
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
		if (_particles != null)
		{
			for (int i = 0; i < _particles.Length; i++)
			{
				var p = _particles[i];
				if (p != null) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
		}
	}

	public void Play(Vector3 worldPosition, Quaternion worldRotation, Color? tintColor)
	{
		InitializeComponents();
		transform.SetPositionAndRotation(worldPosition, worldRotation);
		gameObject.SetActive(true);

		if (_particles != null)
		{
			for (int i = 0; i < _particles.Length; i++)
			{
				var p = _particles[i];
				if (p == null) continue;
				p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
				if (tintColor.HasValue)
				{
					var main = p.main;
					main.startColor = tintColor.Value;
				}
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

		AutoRecycleAsync(_lifetime + 0.05f).Forget();
	}

	private async UniTaskVoid AutoRecycleAsync(float delay)
	{
		bool isCancelled = await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy())
			.SuppressCancellationThrow();
		if (isCancelled || this == null) return;
		Recycle();
	}
}

