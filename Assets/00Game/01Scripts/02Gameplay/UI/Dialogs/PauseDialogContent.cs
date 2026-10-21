using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PauseDialogContent : Snui.BasePopup
{
	[SerializeField] private bool PlayOnStart = true;
	[SerializeField] private bool EnterAnim = true;
	[SerializeField] private bool ExitAnim = true;
	[SerializeField] private Component _bg;
	[SerializeField] private Component _title;
	[SerializeField] private Toggle _musicTgl;
	[SerializeField] private Toggle _audioTgl;
	[SerializeField] private Toggle _vibrateTgl;
	[SerializeField] private Button _settingBtn;
	[SerializeField] private Button _homeBtn;
	[SerializeField] private Button _retryBtn;
	[SerializeField] private Button _continueBtn;

	private GameController _controller;
	private float _timeScaleBeforePause = 1f;
	private bool _ownsPause;
	private Coroutine _fadeRoutine;
	private Snui.Dialog _confirmDialog;
	private bool _isConfirming;
	private SettingsDialog _settings;
	private bool _settingsLoading;
	private int _modalVersion;
	private int _animationSession;

	public override bool IsBusy => _isConfirming || _settingsLoading || _settings != null;

	protected override void Awake()
	{
		base.Awake();
		_controller = GameController.Current;
		_canvasGroup = GetComponent<CanvasGroup>();
		AddButtonListener(_continueBtn, Resume);
		AddButtonListener(_retryBtn, Replay);
		AddButtonListener(_homeBtn, ReturnHome);
		AddButtonListener(_settingBtn, ToggleSettings);
		AddToggleListener(_musicTgl, SetMusic);
		AddToggleListener(_audioTgl, SetAudio);
		AddToggleListener(_vibrateTgl, SetVibration);
	}

	protected override void OnOpened()
	{
		base.OnOpened();
		_animationSession++;
		if (!PlayOnStart) return;
		_controller = _controller != null ? _controller : GameController.Current;
		if (_controller != null) _controller.LevelStarted += OnLevelStarted;
		_timeScaleBeforePause = Time.timeScale;
		Time.timeScale = 0f;
		_ownsPause = true;
		EnableResumeButton(_closeBtn);
		EnableResumeButton(_continueBtn);
		if (_canvasGroup != null)
		{
			_canvasGroup.interactable = true;
			_canvasGroup.blocksRaycasts = true;
			if (EnterAnim)
			{
				_canvasGroup.alpha = 0f;
				_fadeRoutine = StartCoroutine(FadeCanvas(0f, 1f, 0.16f));
			}
			else _canvasGroup.alpha = 1f;
		}
		EnableResumeButton(_homeBtn);
		RefreshToggles();
	}

	protected override void OnDisable()
	{
		if (_controller != null) _controller.LevelStarted -= OnLevelStarted;
		if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
		CancelConfirmation();
		CancelSettings();
		_fadeRoutine = null;
		RestoreTimeScale();
		base.OnDisable();
	}

	public void Bind(GameController controller)
	{
		if (_controller != null && _ownsPause) _controller.LevelStarted -= OnLevelStarted;
		_controller = controller;
		if (_controller != null && _ownsPause) _controller.LevelStarted += OnLevelStarted;
		RefreshToggles();
	}

	private void RefreshToggles()
	{
		SetToggleValue(_musicTgl, GameAudioSettings.MusicEnabled);
		SetToggleValue(_audioTgl, GameAudioSettings.EffectsEnabled);
		SetToggleValue(_vibrateTgl, GameVibrationHelper.Enabled);
	}

	private void SetMusic(bool enabled)
	{
		GameAudioSettings.SetMusicEnabled(enabled);
	}

	private void SetAudio(bool enabled)
	{
		GameAudioSettings.SetEffectsEnabled(enabled);
	}

	private void SetVibration(bool enabled)
	{
		GameVibrationHelper.SetEnabled(enabled);
	}

	private void Resume()
	{
		TryClose();
	}

	protected override async UniTask PlayCloseAnimationAsync()
	{
		int session = _animationSession;
		if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
		_fadeRoutine = null;
		if (!ExitAnim || _canvasGroup == null) return;
		float from = _canvasGroup.alpha;
		float elapsed = 0f;
		while (elapsed < 0.12f)
		{
			elapsed += Time.unscaledDeltaTime;
			_canvasGroup.alpha = Mathf.Lerp(from, 0f, Mathf.Clamp01(elapsed / 0.12f));
			await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
			if (session != _animationSession || !isActiveAndEnabled) return;
		}
	}

	private void Replay()
	{
		OpenConfirmation(PauseConfirmType.Replay);
	}

	private void ReturnHome()
	{
		OpenConfirmation(PauseConfirmType.Quit);
	}

	private void OpenConfirmation(PauseConfirmType type) => LoadConfirmation(type).Forget();
	private async UniTask LoadConfirmation(PauseConfirmType type)
	{
		if (_isClosing || _isConfirming || _settingsLoading || _settings != null || !isActiveAndEnabled || _controller == null) return;
		GameData data = _controller.Data;
		int version = _modalVersion;
		_isConfirming = true;
		if (_canvasGroup != null) _canvasGroup.interactable = false;
		try
		{
			var content = await Snui.Dialog.ShowContent<PauseConfirmDialogContent>(transform.parent,
				type == PauseConfirmType.Replay ? "ReplayDialogContent" : "QuitLevelDialogContent");
			var frame = content.GetComponentInParent<Snui.Dialog>();
			if (this == null || version != _modalVersion || !isActiveAndEnabled || _controller == null || _controller.Data != data)
			{ if (frame != null) Destroy(frame.gameObject); return; }
			_confirmDialog = frame;
			content.Dismissed += result =>
			{
				if (!_isConfirming || !isActiveAndEnabled || _controller == null || _controller.Data != data) return;
				CancelConfirmation();
				if (result != PauseConfirmDialogContent.RESULT_CONFIRM) return;
				CompleteResume();
				if (type == PauseConfirmType.Replay) _controller.ReplayLevel();
				else _controller.ReturnHome();
			};
		}
		catch { if (this != null && version == _modalVersion) CancelConfirmation(); throw; }
	}

	private void CancelConfirmation()
	{
		_modalVersion++;
		_isConfirming = false;
		if (_confirmDialog != null) { _confirmDialog.gameObject.SetActive(false); Destroy(_confirmDialog.gameObject); }
		_confirmDialog = null;
		if (_canvasGroup != null) _canvasGroup.interactable = true;
	}
	private void OnLevelStarted(int level) => CompleteResume();

	private void ToggleSettings() => LoadSettings().Forget();
	private async UniTask LoadSettings()
	{
		if (_isClosing || _isConfirming || _settingsLoading || _settings != null || !isActiveAndEnabled) return;
		int version = _modalVersion;
		_settingsLoading = true;
		if (_canvasGroup != null) _canvasGroup.interactable = false;
		try
		{
			var settings = await SettingsDialog.Show(transform.parent);
			if (this == null || version != _modalVersion || !isActiveAndEnabled)
			{ if (settings != null) Destroy(settings.gameObject); return; }
			_settings = settings;
			if (_settings != null) _settings.Closed += OnSettingsClosed;
		}
		finally
		{
			if (this != null && version == _modalVersion)
			{
				_settingsLoading = false;
				if (_settings == null && _canvasGroup != null) _canvasGroup.interactable = !_isClosing && !_isConfirming;
			}
		}
	}
	private void OnSettingsClosed()
	{
		if (_settings != null) _settings.Closed -= OnSettingsClosed;
		_settings = null;
		if (!isActiveAndEnabled) return;
		RefreshToggles();
		if (_canvasGroup != null) _canvasGroup.interactable = !_isClosing && !_isConfirming;
	}
	private void CancelSettings()
	{
		_modalVersion++;
		_settingsLoading = false;
		if (_settings != null)
		{
			_settings.Closed -= OnSettingsClosed;
			_settings.gameObject.SetActive(false); Destroy(_settings.gameObject);
		}
		_settings = null;
	}
	protected override void OnDestroy()
	{
		CancelSettings();
		if (_continueBtn != null) _continueBtn.onClick.RemoveListener(Resume);
		if (_retryBtn != null) _retryBtn.onClick.RemoveListener(Replay);
		if (_homeBtn != null) _homeBtn.onClick.RemoveListener(ReturnHome);
		if (_settingBtn != null) _settingBtn.onClick.RemoveListener(ToggleSettings);
		if (_musicTgl != null) _musicTgl.onValueChanged.RemoveListener(SetMusic);
		if (_audioTgl != null) _audioTgl.onValueChanged.RemoveListener(SetAudio);
		if (_vibrateTgl != null) _vibrateTgl.onValueChanged.RemoveListener(SetVibration);
		base.OnDestroy();
	}

	private void RestoreTimeScale()
	{
		if (!_ownsPause) return;
		Time.timeScale = _timeScaleBeforePause;
		_ownsPause = false;
	}

	private System.Collections.IEnumerator FadeCanvas(float from, float to, float duration)
	{
		if (_canvasGroup == null) yield break;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			_canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
			yield return null;
		}
		_canvasGroup.alpha = to;
	}

	private void CompleteResume()
	{
		RestoreTimeScale();
		ForceClose();
	}

	private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction callback)
	{
		if (button == null) return;
		button.onClick.RemoveListener(callback);
		button.onClick.AddListener(callback);
	}

	private static void EnableResumeButton(Button button)
	{
		if (button == null) return;
		button.interactable = true;
		button.GetComponent<Snui.Snui_Button>()?.EnableButton();
	}

	private static void AddToggleListener(Toggle toggle, UnityEngine.Events.UnityAction<bool> callback)
	{
		if (toggle == null) return;
		toggle.onValueChanged.RemoveListener(callback);
		toggle.onValueChanged.AddListener(callback);
	}

	private static void SetToggleValue(Toggle toggle, bool value)
	{
		if (toggle == null) return;
		toggle.SetIsOnWithoutNotify(value);
		PauseToggle pauseToggle = toggle as PauseToggle;
		if (pauseToggle != null) pauseToggle.RefreshVisual();
	}
}

public static class GameAudioSettings
{
	private const string MusicKey = GamePrefKeys.Music;
	private const string EffectsKey = GamePrefKeys.Effects;
	private static readonly Dictionary<AudioSource, bool> OriginalMusicMute = new Dictionary<AudioSource, bool>();
	private static readonly Dictionary<AudioSource, bool> OriginalEffectsMute = new Dictionary<AudioSource, bool>();

	public static bool MusicEnabled { get; private set; } = GamePrefs.GetInt(MusicKey, 1) != 0;
	public static bool EffectsEnabled { get; private set; } = GamePrefs.GetInt(EffectsKey, 1) != 0;

	public static void SetMusicEnabled(bool enabled)
	{
		MusicEnabled = enabled;
		GamePrefs.SetInt(MusicKey, enabled ? 1 : 0);
		ApplyMusicPreference();
		if (BgmSource.Current != null) BgmSource.Current.RefreshMusicPreference();
		GamePrefs.Save();
	}

	public static void SetEffectsEnabled(bool enabled)
	{
		EffectsEnabled = enabled;
		GamePrefs.SetInt(EffectsKey, enabled ? 1 : 0);
		ApplyEffectsPreference();
		GamePrefs.Save();
	}

	public static void ApplyMusicPreference()
	{
		ApplyPreference(OriginalMusicMute, MusicEnabled);
	}

	public static void ApplyEffectsPreference()
	{
		ApplyPreference(OriginalEffectsMute, EffectsEnabled);
	}

	private static void ApplyPreference(Dictionary<AudioSource, bool> sources, bool enabled)
	{
		List<AudioSource> destroyedSources = null;
		foreach (KeyValuePair<AudioSource, bool> entry in sources)
		{
			AudioSource source = entry.Key;
			if (source == null)
			{
				if (destroyedSources == null) destroyedSources = new List<AudioSource>();
				destroyedSources.Add(source);
				continue;
			}
			source.mute = entry.Value || !enabled;
		}
		if (destroyedSources == null) return;
		for (int i = 0; i < destroyedSources.Count; i++) sources.Remove(destroyedSources[i]);
	}

	public static void RegisterMusicSource(AudioSource source)
	{
		if (source == null || OriginalMusicMute.ContainsKey(source)) return;
		OriginalMusicMute[source] = source.mute;
		source.mute = source.mute || !MusicEnabled;
	}

	public static void UnregisterMusicSource(AudioSource source)
	{
		if (!ReferenceEquals(source, null)) OriginalMusicMute.Remove(source);
	}

	public static void RegisterEffectsSource(AudioSource source)
	{
		if (source == null || OriginalEffectsMute.ContainsKey(source)) return;
		OriginalEffectsMute[source] = source.mute;
		source.mute = source.mute || !EffectsEnabled;
	}

	public static void UnregisterEffectsSource(AudioSource source)
	{
		if (!ReferenceEquals(source, null)) OriginalEffectsMute.Remove(source);
	}

	public static void PauseEffects()
	{
		foreach (AudioSource source in OriginalEffectsMute.Keys)
			if (source != null) source.Pause();
	}

	public static void UnPauseEffects()
	{
		foreach (AudioSource source in OriginalEffectsMute.Keys)
			if (source != null) source.UnPause();
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetAudioSettings()
	{
		MusicEnabled = GamePrefs.GetInt(MusicKey, 1) != 0;
		EffectsEnabled = GamePrefs.GetInt(EffectsKey, 1) != 0;
		ApplyMusicPreference();
		ApplyEffectsPreference();
	}
}
