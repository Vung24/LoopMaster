using System;
using UnityEngine;

public class GameAudioPlayer : MonoBehaviour
{
	private const float AudioRequestInterval = 0.04f;
	private AudioSource _sharedSource;

	private enum RequestKey
	{
		SlicePopIn = 0,
		SlicePopOut = 1,
		ColorLockOpen = 2,
		NumLockBlast = 3,
		AdjacentLockBlast = 4,
		Count = 5
	}

	private readonly float[] _nextAllowedAudioTime = new float[(int)RequestKey.Count];

	public static GameAudioPlayer Current { get; private set; }

	private void Awake()
	{
		Current = this;
		_sharedSource = GetComponent<AudioSource>();
		if (_sharedSource == null) _sharedSource = gameObject.AddComponent<AudioSource>();
		_sharedSource.playOnAwake = false;
		GameAudioSettings.ApplyMusicPreference();
	}

	private void OnDestroy()
	{
		if (Current == this) Current = null;
	}

	private void OnDisable()
	{
		Array.Clear(_nextAllowedAudioTime, 0, _nextAllowedAudioTime.Length);
	}

	public void PlayOneShot(AudioClip clip)
	{
		if (GameAudioSettings.EffectsEnabled && clip != null && _sharedSource != null && _sharedSource.isActiveAndEnabled)
		{
			_sharedSource.pitch = 1f;
			_sharedSource.PlayOneShot(clip);
		}
	}

	public void PlayClickAudio()
	{
		AudioClip clip = GameAssetsConfig.Instance?.ButtonClickAudio;
		if (clip != null) PlayOneShot(clip);
	}

	public void PlaySlicePopIn(AudioSource source, AudioClip clip, float pitch)
	{
		if (TryRequest(RequestKey.SlicePopIn)) PlayBoxAudio(source, clip, pitch);
	}

	public void PlaySlicePopOut(AudioSource source, AudioClip clip, float pitch)
	{
		if (TryRequest(RequestKey.SlicePopOut)) PlayBoxAudio(source, clip, pitch);
	}

	public void PlayColorLockOpen(AudioClip clip)
	{
		if (TryRequest(RequestKey.ColorLockOpen)) PlayOneShot(clip);
	}

	public void PlayNumLockBlast(AudioClip clip)
	{
		if (TryRequest(RequestKey.NumLockBlast)) PlayOneShot(clip);
	}

	public void PlayAdjacentLockBlast(AudioClip clip)
	{
		if (TryRequest(RequestKey.AdjacentLockBlast)) PlayOneShot(clip);
	}

	public void PlayBoxAudio(AudioSource source, AudioClip clip, float pitch = 1f)
	{
		if (!GameAudioSettings.EffectsEnabled || clip == null) return;
		AudioSource target = source != null ? source : _sharedSource;
		if (target == null || !target.isActiveAndEnabled) return;
		target.pitch = Mathf.Clamp(pitch, -3f, 3f);
		target.PlayOneShot(clip);
	}

	private bool TryRequest(RequestKey key)
	{
		int index = (int)key;
		if ((uint)index >= (uint)_nextAllowedAudioTime.Length) return false;
		float now = Time.unscaledTime;
		if (now < _nextAllowedAudioTime[index]) return false;
		_nextAllowedAudioTime[index] = now + AudioRequestInterval;
		return true;
	}
}
