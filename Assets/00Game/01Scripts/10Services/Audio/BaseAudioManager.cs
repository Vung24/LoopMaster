using System.Collections.Generic;
using UnityEngine;

public abstract class BaseAudioManager : MonoBehaviour
{
	protected AudioSource _audioSource;
	protected Dictionary<GameObject, AudioSource> _gameObjAudios;

	private bool _soundOn = true;
	private bool _musicOn = true;

	public void Awake()
	{
		_audioSource = GetComponent<AudioSource>();
		if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
		_gameObjAudios = new Dictionary<GameObject, AudioSource>();
	}

	protected abstract AudioClip GetAudioClip(string audioName);

	public virtual void IsSoundOn(bool isOn)
	{
		_soundOn = isOn;
	}

	public virtual void IsMusicOn(bool isOn)
	{
		_musicOn = isOn;
		if (_audioSource != null) _audioSource.mute = !isOn || !GameAudioSettings.MusicEnabled;
	}

	public virtual void PlayOneShot(string audioName, GameObject targetObj = null)
	{
		PlayOneShot(GetAudioClip(audioName), targetObj);
	}

	public virtual void PlayAudio(string audioName, bool isLoop = false, GameObject targetObj = null)
	{
		PlayAudio(GetAudioClip(audioName), isLoop, targetObj);
	}

	public void PlayOneShot(AudioClip audioClip, GameObject targetObj = null)
	{
		if (!_soundOn || !GameAudioSettings.EffectsEnabled || audioClip == null) return;
		AudioSource source = GetSource(targetObj);
		if (source != null) source.PlayOneShot(audioClip);
	}

	public void PlayOneShotForPan(AudioClip audioClip, float panValue, GameObject targetObj = null)
	{
		if (!_soundOn || !GameAudioSettings.EffectsEnabled || audioClip == null) return;
		AudioSource source = GetSource(targetObj);
		if (source == null) return;
		float previousPan = source.panStereo;
		source.panStereo = Mathf.Clamp(panValue, -1f, 1f);
		source.PlayOneShot(audioClip);
		source.panStereo = previousPan;
	}

	public virtual void PlayAudio(AudioClip audioClip, bool isLoop = false, GameObject targetObj = null)
	{
		if (!_soundOn || !GameAudioSettings.EffectsEnabled || audioClip == null) return;
		AudioSource source = GetSource(targetObj);
		if (source == null) return;
		source.clip = audioClip;
		source.loop = isLoop;
		source.Play();
	}

	public void PlayAudio()
	{
		if (_soundOn && GameAudioSettings.EffectsEnabled && _audioSource != null && _audioSource.clip != null) _audioSource.Play();
	}

	public virtual void StopAudio()
	{
		if (_audioSource != null) _audioSource.Stop();
	}

	public virtual void StopLoopAudio()
	{
		if (_audioSource != null && _audioSource.loop) _audioSource.Stop();
	}

	public virtual void StopAudio(GameObject targetObj)
	{
		AudioSource source = GetExistingSource(targetObj);
		if (source != null) source.Stop();
	}

	public virtual void StopAllAudio()
	{
		if (_audioSource != null) _audioSource.Stop();
		foreach (AudioSource source in _gameObjAudios.Values)
			if (source != null) source.Stop();
	}

	public virtual void PauseAllAudio()
	{
		if (_audioSource != null) _audioSource.Pause();
		foreach (AudioSource source in _gameObjAudios.Values)
			if (source != null) source.Pause();
	}

	public virtual void UnPauseAllAudio()
	{
		if (_audioSource != null) _audioSource.UnPause();
		foreach (AudioSource source in _gameObjAudios.Values)
			if (source != null) source.UnPause();
	}

	public virtual void StopMusic()
	{
		if (_audioSource != null) _audioSource.Stop();
	}

	public virtual void PlayMusic()
	{
		if (_musicOn && _audioSource != null && _audioSource.clip != null)
		{
			GameAudioSettings.RegisterMusicSource(_audioSource); _audioSource.Play();
		}
	}

	public virtual void PlayMusic(string audioName, bool isLoop = false, GameObject targetObj = null)
	{
		PlayMusic(GetAudioClip(audioName), isLoop, targetObj);
	}

	public virtual void PlayMusic(AudioClip audioClip, bool isLoop = false, GameObject targetObj = null)
	{
		if (!_musicOn || audioClip == null) return;
		AudioSource source = GetSource(targetObj);
		if (source == null) return;
		GameAudioSettings.RegisterMusicSource(source);
		source.clip = audioClip;
		source.loop = isLoop;
		source.Play();
	}

	public virtual bool IsPlaying(GameObject targetObj = null)
	{
		AudioSource source = GetExistingSource(targetObj);
		return source != null && source.isPlaying;
	}

	public void AddGameObjAudio(GameObject gameObject, AudioSource audio)
	{
		if (gameObject == null || audio == null) return;
		_gameObjAudios[gameObject] = audio;
	}

	public void RemoveGameObjAudio(GameObject gameObject)
	{
		if (gameObject != null) _gameObjAudios.Remove(gameObject);
	}

	private AudioSource GetSource(GameObject targetObj)
	{
		if (targetObj == null) return _audioSource;
		if (_gameObjAudios.TryGetValue(targetObj, out AudioSource source) && source != null)
			return source;
		source = targetObj.GetComponent<AudioSource>();
		if (source == null) source = targetObj.AddComponent<AudioSource>();
		_gameObjAudios[targetObj] = source;
		return source;
	}

	private AudioSource GetExistingSource(GameObject targetObj)
	{
		if (targetObj == null) return _audioSource;
		_gameObjAudios.TryGetValue(targetObj, out AudioSource source);
		return source;
	}
}
