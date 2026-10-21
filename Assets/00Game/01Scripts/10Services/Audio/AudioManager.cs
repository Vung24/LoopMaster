using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : BaseAudioManager, IGameManager
{
    public static AudioManager Current { get; private set; }
    private readonly Dictionary<string, AudioClip> _cachedClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool _applicationPaused;
    private bool _applicationFocused = true;
    private bool _applicationAudioPaused;
    public ManagerStatus status { get; private set; } = ManagerStatus.Shutdown;

    public static AudioManager EnsureInitialized()
    {
        if (Current == null) new GameObject("AudioManager").AddComponent<AudioManager>();
        Current.Startup();
        BgmSource.EnsureInitialized();
        return Current;
    }

    public new void Awake()
    {
        if (Current != null && Current != this)
        {
            Destroy(this);
            return;
        }
        base.Awake();
        Current = this;
        DontDestroyOnLoad(transform.root.gameObject);
        Startup();
    }

    private void OnEnable()
    {
        if (Current == this) SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    public void Startup()
    {
        if (status == ManagerStatus.Started) return;
        status = ManagerStatus.Initializing;
        GameAssetsConfig config = GameAssetsConfig.Instance;
        if (config != null)
        {
            CacheClip(config.ButtonClickAudio);
            if (config.CommonAudioClips != null)
                for (int i = 0; i < config.CommonAudioClips.Length; i++) CacheClip(config.CommonAudioClips[i]);
        }
        status = ManagerStatus.Started;
    }

    private void CacheClip(AudioClip clip)
    {
        if (clip != null) _cachedClips[clip.name] = clip;
    }

    protected override AudioClip GetAudioClip(string audioName)
    {
        if (string.IsNullOrEmpty(audioName)) return null;
        string key = Path.GetFileNameWithoutExtension(audioName);
        if (_cachedClips.TryGetValue(key, out AudioClip clip) && clip != null) return clip;
        clip = Resources.Load<AudioClip>(audioName);
        if (clip == null) clip = Resources.Load<AudioClip>("Audio/" + key);
        if (clip != null)
        {
            _cachedClips[key] = clip;
            _missingClips.Remove(key);
            return clip;
        }
        if (_missingClips.Add(key))
            Debug.LogWarning("[AudioManager] Missing audio '" + audioName + "'. Add its clip to GameAssetsConfig.CommonAudioClips.", this);
        return null;
    }

    public void PlayOneShotForPan(string audioName, float panValue, GameObject target)
    {
        PlayOneShotForPan(GetAudioClip(audioName), panValue, target);
    }

    public override void PauseAllAudio()
    {
        base.PauseAllAudio();
        GameAudioSettings.PauseEffects();
        if (BgmSource.Current != null) BgmSource.Current.Pause();
    }

    public override void UnPauseAllAudio()
    {
        base.UnPauseAllAudio();
        GameAudioSettings.UnPauseEffects();
        if (BgmSource.Current != null) BgmSource.Current.UnPause();
    }

    public override void IsMusicOn(bool isOn)
    {
        base.IsMusicOn(isOn);
        if (BgmSource.Current == null) return;
        if (isOn) BgmSource.Current.Play();
        else BgmSource.Current.Stop().Forget();
    }

    public override void StopMusic()
    {
        base.StopMusic();
        if (BgmSource.Current != null) BgmSource.Current.Stop().Forget();
    }

    public override void PlayMusic()
    {
        base.PlayMusic();
        if (BgmSource.Current != null) BgmSource.Current.Play();
    }

    public void StopBuildAudio()
    {
        StopLoopAudio();
    }

    private void OnApplicationPause(bool paused)
    {
        _applicationPaused = paused;
        UpdateApplicationAudio();
    }

    private void OnApplicationFocus(bool focused)
    {
        _applicationFocused = focused;
        UpdateApplicationAudio();
    }

    private void UpdateApplicationAudio()
    {
        bool paused = _applicationPaused || !_applicationFocused;
        if (_applicationAudioPaused == paused) return;
        _applicationAudioPaused = paused;
        if (paused) PauseAllAudio();
        else UnPauseAllAudio();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        RemoveDestroyedSources();
        GameAudioSettings.ApplyMusicPreference();
        GameAudioSettings.ApplyEffectsPreference();
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (Current != this) return;
        StopAllAudio();
        ReleaseSources();
        Current = null;
        _cachedClips.Clear();
        _missingClips.Clear();
        status = ManagerStatus.Shutdown;
    }
}
