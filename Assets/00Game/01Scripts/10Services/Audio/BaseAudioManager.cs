using System.Collections.Generic;
using UnityEngine;

public abstract class BaseAudioManager : MonoBehaviour
{
	protected AudioSource _audioSource;
	protected Dictionary<GameObject, AudioSource> _gameObjAudios;
	private AudioSource _musicSource;
	private readonly Dictionary<GameObject, AudioSource> _gameObjMusic = new Dictionary<GameObject, AudioSource>();
	private readonly List<GameObject> _removedTargets = new List<GameObject>();
	public bool IsPaused { get; private set; }

	private bool _soundOn = true;
	private bool _musicOn = true;

	public void Awake()
	{
		_audioSource = GetComponent<AudioSource>();
		if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
		_audioSource.playOnAwake = false;
		_audioSource.spatialBlend = 0f;
		GameAudioSettings.RegisterEffectsSource(_audioSource);
		_gameObjAudios = new Dictionary<GameObject, AudioSource>();
	}

	protected abstract AudioClip GetAudioClip(string audioName);

	public virtual void IsSoundOn(bool isOn)
	{
		_soundOn = isOn;
		if (isOn) return;
		if (_audioSource != null) _audioSource.Stop();
		foreach (AudioSource source in _gameObjAudios.Values)
			if (source != null) source.Stop();
	}

	public virtual void IsMusicOn(bool isOn)
	{
		_musicOn = isOn;
		if (_musicSource != null) _musicSource.mute = !isOn || !GameAudioSettings.MusicEnabled;
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
		if (IsPaused || !_soundOn || !GameAudioSettings.EffectsEnabled || audioClip == null) return;
		AudioSource source = GetSource(targetObj);
		if (source == null || !source.isActiveAndEnabled) return;
		source.pitch = 1f;
		source.panStereo = 0f;
		source.PlayOneShot(audioClip);
	}

	public void PlayOneShotForPan(AudioClip audioClip, float panValue, GameObject targetObj = null)
	{
		if (IsPaused || !_soundOn || !GameAudioSettings.EffectsEnabled || audioClip == null) return;
		AudioSource source = GetSource(targetObj);
		if (source == null || !source.isActiveAndEnabled) return;
		source.pitch = 1f;
		source.panStereo = Mathf.Clamp(panValue, -1f, 1f);
		source.PlayOneShot(audioClip);
	}

	public virtual void PlayAudio(AudioClip audioClip, bool isLoop = false, GameObject targetObj = null)
	{
		if (IsPaused || !_soundOn || !GameAudioSettings.EffectsEnabled || audioClip == null) return;
		AudioSource source = GetSource(targetObj);
		if (source == null || !source.isActiveAndEnabled) return;
		source.pitch = 1f;
		source.panStereo = 0f;
		source.clip = audioClip;
		source.loop = isLoop;
		source.Play();
	}

	public void PlayAudio()
	{
		if (!IsPaused && _soundOn && GameAudioSettings.EffectsEnabled && _audioSource != null && _audioSource.clip != null) _audioSource.Play();
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
		if (targetObj != null && _gameObjMusic.TryGetValue(targetObj, out AudioSource music) && music != null) music.Stop();
	}

	public virtual void StopAllAudio()
	{
		if (_audioSource != null) _audioSource.Stop();
		foreach (AudioSource source in _gameObjAudios.Values)
			if (source != null) source.Stop();
		if (_musicSource != null) _musicSource.Stop();
		foreach (AudioSource source in _gameObjMusic.Values)
			if (source != null) source.Stop();
	}

	public virtual void PauseAllAudio()
	{
		IsPaused = true;
		if (_audioSource != null) _audioSource.Pause();
		foreach (AudioSource source in _gameObjAudios.Values)
			if (source != null) source.Pause();
		if (_musicSource != null) _musicSource.Pause();
		foreach (AudioSource source in _gameObjMusic.Values)
			if (source != null) source.Pause();
	}

	public virtual void UnPauseAllAudio()
	{
		IsPaused = false;
		if (_audioSource != null) _audioSource.UnPause();
		foreach (AudioSource source in _gameObjAudios.Values)
			if (source != null) source.UnPause();
		if (_musicSource != null) _musicSource.UnPause();
		foreach (AudioSource source in _gameObjMusic.Values)
			if (source != null) source.UnPause();
	}

	public virtual void StopMusic()
	{
		if (_musicSource != null) _musicSource.Stop();
		foreach (AudioSource source in _gameObjMusic.Values)
			if (source != null) source.Stop();
	}

	public virtual void PlayMusic()
	{
		if (_musicOn && _musicSource != null && _musicSource.clip != null)
		{
			_musicSource.Play();
			if (IsPaused) _musicSource.Pause();
		}
	}

	public virtual void PlayMusic(string audioName, bool isLoop = false, GameObject targetObj = null)
	{
		PlayMusic(GetAudioClip(audioName), isLoop, targetObj);
	}

	public virtual void PlayMusic(AudioClip audioClip, bool isLoop = false, GameObject targetObj = null)
	{
		if (!_musicOn || audioClip == null) return;
		AudioSource source = GetMusicSource(targetObj);
		if (source == null) return;
		source.clip = audioClip;
		source.loop = isLoop;
		source.Play();
		if (IsPaused) source.Pause();
	}

	public virtual bool IsPlaying(GameObject targetObj = null)
	{
		AudioSource source = GetExistingSource(targetObj);
		if (source != null && source.isPlaying) return true;
		if (targetObj == null) return _musicSource != null && _musicSource.isPlaying;
		return _gameObjMusic.TryGetValue(targetObj, out AudioSource music) && music != null && music.isPlaying;
	}

	public void AddGameObjAudio(GameObject gameObject, AudioSource audio)
	{
		if (gameObject == null || audio == null) return;
		_gameObjAudios[gameObject] = audio;
		GameAudioSettings.RegisterEffectsSource(audio);
	}

	public void RemoveGameObjAudio(GameObject gameObject)
	{
		if (gameObject == null) return;
		if (_gameObjAudios.TryGetValue(gameObject, out AudioSource source)) GameAudioSettings.UnregisterEffectsSource(source);
		_gameObjAudios.Remove(gameObject);
	}

	private AudioSource GetSource(GameObject targetObj)
	{
		if (targetObj == null) return _audioSource;
		if (_gameObjAudios.TryGetValue(targetObj, out AudioSource source) && source != null)
			return source;
		source = targetObj.GetComponent<AudioSource>();
		if (_gameObjMusic.TryGetValue(targetObj, out AudioSource music) && source == music) source = null;
		if (source == null) source = targetObj.AddComponent<AudioSource>();
		source.playOnAwake = false;
		GameAudioSettings.RegisterEffectsSource(source);
		_gameObjAudios[targetObj] = source;
		return source;
	}

	private AudioSource GetMusicSource(GameObject targetObj)
	{
		if (targetObj == null)
		{
			if (_musicSource == null) _musicSource = CreateMusicSource(gameObject);
			return _musicSource;
		}
		if (_gameObjMusic.TryGetValue(targetObj, out AudioSource source) && source != null) return source;
		source = CreateMusicSource(targetObj);
		_gameObjMusic[targetObj] = source;
		return source;
	}

	private AudioSource CreateMusicSource(GameObject owner)
	{
		AudioSource source = owner.AddComponent<AudioSource>();
		source.playOnAwake = false;
		source.spatialBlend = 0f;
		GameAudioSettings.RegisterMusicSource(source);
		return source;
	}

	protected void RemoveDestroyedSources()
	{
		RemoveDestroyedSources(_gameObjAudios);
		RemoveDestroyedSources(_gameObjMusic);
	}

	private void RemoveDestroyedSources(Dictionary<GameObject, AudioSource> sources)
	{
		_removedTargets.Clear();
		foreach (KeyValuePair<GameObject, AudioSource> entry in sources)
			if (entry.Key == null || entry.Value == null) _removedTargets.Add(entry.Key);
		for (int i = 0; i < _removedTargets.Count; i++)
		{
			AudioSource source = sources[_removedTargets[i]];
			GameAudioSettings.UnregisterEffectsSource(source);
			GameAudioSettings.UnregisterMusicSource(source);
			sources.Remove(_removedTargets[i]);
		}
		_removedTargets.Clear();
	}

	protected void ReleaseSources()
	{
		GameAudioSettings.UnregisterEffectsSource(_audioSource);
		GameAudioSettings.UnregisterMusicSource(_musicSource);
		foreach (AudioSource source in _gameObjAudios.Values) GameAudioSettings.UnregisterEffectsSource(source);
		foreach (AudioSource source in _gameObjMusic.Values) GameAudioSettings.UnregisterMusicSource(source);
		_gameObjAudios.Clear();
		_gameObjMusic.Clear();
	}

	private AudioSource GetExistingSource(GameObject targetObj)
	{
		if (targetObj == null) return _audioSource;
		_gameObjAudios.TryGetValue(targetObj, out AudioSource source);
		return source;
	}
}
