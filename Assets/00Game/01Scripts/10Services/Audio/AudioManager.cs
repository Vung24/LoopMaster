using System.Collections.Generic;
using UnityEngine;

public class AudioManager : BaseAudioManager, IGameManager
{
	public static AudioManager Current { get; private set; }

	public new void Awake()
	{
		base.Awake();
		Current = this;
	}
	private Dictionary<string, AudioClip> _cachedClips;
	public ManagerStatus status { get; private set; } = ManagerStatus.Shutdown;

	public void Startup()
	{
		if (status == ManagerStatus.Started) return;
		status = ManagerStatus.Initializing;
		if (_cachedClips == null) _cachedClips = new Dictionary<string, AudioClip>();
		status = ManagerStatus.Started;
	}

	protected override AudioClip GetAudioClip(string audioName)
	{
		if (string.IsNullOrEmpty(audioName)) return null;
		if (_cachedClips == null) _cachedClips = new Dictionary<string, AudioClip>();
		if (_cachedClips.TryGetValue(audioName, out AudioClip clip)) return clip;

		clip = Resources.Load<AudioClip>(audioName);
		if (clip == null) clip = Resources.Load<AudioClip>("Audio/" + audioName);
		_cachedClips[audioName] = clip;
		return clip;
	}

	public void PlayOneShotForPan(string audioName, float panValue, GameObject target)
	{
		PlayOneShotForPan(GetAudioClip(audioName), panValue, target);
	}

	public void StopBuildAudio()
	{
		StopLoopAudio();
	}

	private void OnDestroy()
	{
		if (Current == this) Current = null;
		StopAllAudio();
		_cachedClips?.Clear();
	}
}
