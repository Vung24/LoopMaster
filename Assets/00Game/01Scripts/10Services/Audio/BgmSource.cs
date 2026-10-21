using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[RequireComponent(typeof(AudioSource))]
public class BgmSource : MonoBehaviour
{
    private const float FadeInDuration = 0.3f;
    private const float FadeOutDuration = 0.2f;
    [FormerlySerializedAs("_homeMusic")] public AudioClip HomeBgm;
    [FormerlySerializedAs("_gameMusic")] public AudioClip GameBgm;
    public static BgmSource Current { get; private set; }
    private AudioSource _audioSource;
    private Tween _fade;
    private CancellationTokenSource _stopCancellation;
    private bool _playRequested;
    private bool _paused;
    private bool _sourcePaused;

    public static BgmSource EnsureInitialized()
    {
        if (Current == null) new GameObject("Bgm").AddComponent<BgmSource>();
        return Current;
    }

    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Destroy(gameObject);
            return;
        }
        GameAssetsConfig config = GameAssetsConfig.Instance;
        if (HomeBgm == null && config != null) HomeBgm = config.HomeBgm;
        if (GameBgm == null && config != null) GameBgm = config.GameBgm;
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null || HomeBgm == null || GameBgm == null)
        {
            Debug.LogError("[BgmSource] Assign an AudioSource and both Home/Game music clips on Splash Bgm or GameAssetsConfig.", this);
            enabled = false;
            return;
        }
        Current = this;
        _paused = AudioManager.Current != null && AudioManager.Current.IsPaused;
        DontDestroyOnLoad(gameObject);
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;
        _audioSource.spatialBlend = 0f;
        GameAudioSettings.RegisterMusicSource(_audioSource);
    }

    private void OnEnable()
    {
        if (Current != this) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (!GameSceneManager.OnChangeScene) Play();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "HomeScene" && scene.name != "GameScene") return;
        if (!GameSceneManager.OnChangeScene) Play();
    }

    public void Play()
    {
        if (Current != this || !isActiveAndEnabled) return;
        AudioClip clip;
        switch (SceneManager.GetActiveScene().name)
        {
            case "HomeScene": clip = HomeBgm; break;
            case "GameScene": clip = GameBgm; break;
            default: return;
        }
        _playRequested = true;
        if (!GameAudioSettings.MusicEnabled) return;
        bool interruptedStop = _stopCancellation != null;
        CancelStop();
        if (_audioSource.clip == clip && (_audioSource.isPlaying || _sourcePaused))
        {
            bool fadeInterrupted = _fade == null || !_fade.IsActive();
            if (interruptedStop || (fadeInterrupted && _audioSource.volume < 1f)) FadeIn();
            return;
        }
        KillFade();
        _audioSource.Stop();
        _sourcePaused = false;
        _audioSource.clip = clip;
        _audioSource.volume = 0f;
        _audioSource.Play();
        if (_paused)
        {
            _audioSource.Pause();
            _sourcePaused = true;
        }
        FadeIn();
    }

    private void FadeIn()
    {
        KillFade();
        _fade = _audioSource.DOFade(1f, FadeInDuration).SetUpdate(true);
        if (_paused) _fade.Pause();
    }

    public UniTask Stop()
    {
        _playRequested = false;
        return FadeOut();
    }

    private async UniTask FadeOut()
    {
        CancelStop();
        KillFade();
        if (_audioSource == null) return;
        if (!_audioSource.isPlaying || _paused)
        {
            _audioSource.Stop();
            _sourcePaused = false;
            return;
        }
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _stopCancellation = lifetime;
        _fade = _audioSource.DOFade(0f, FadeOutDuration).SetUpdate(true);
        try
        {
            await UniTask.WaitForSeconds(FadeOutDuration, ignoreTimeScale: true, cancellationToken: lifetime.Token);
            if (_stopCancellation == lifetime && _audioSource != null)
            {
                _audioSource.Stop();
                _sourcePaused = false;
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally
        {
            if (_stopCancellation == lifetime) _stopCancellation = null;
            lifetime.Dispose();
        }
    }

    public void RefreshMusicPreference()
    {
        if (!_playRequested) return;
        if (GameAudioSettings.MusicEnabled) Play();
        else FadeOut().Forget();
    }

    public void Pause()
    {
        _paused = true;
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Pause();
            _sourcePaused = true;
        }
        if (_fade != null && _fade.IsActive()) _fade.Pause();
    }

    public void UnPause()
    {
        _paused = false;
        _audioSource?.UnPause();
        _sourcePaused = false;
        if (_fade != null && _fade.IsActive()) _fade.Play();
    }

    private void CancelStop()
    {
        CancellationTokenSource lifetime = _stopCancellation;
        _stopCancellation = null;
        lifetime?.Cancel();
    }

    private void KillFade()
    {
        if (_fade != null && _fade.IsActive()) _fade.Kill();
        _fade = null;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CancelStop();
        KillFade();
        _audioSource?.Stop();
        _sourcePaused = false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CancelStop();
        KillFade();
        GameAudioSettings.UnregisterMusicSource(_audioSource);
        if (Current == this) Current = null;
    }
}
