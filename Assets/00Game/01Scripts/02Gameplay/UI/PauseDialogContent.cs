using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PauseDialogContent : MonoBehaviour
{
	[SerializeField] private bool PlayOnStart = true;
	[SerializeField] private bool EnterAnim = true;
	[SerializeField] private bool ExitAnim = true;
	[SerializeField] private Component _bg;
	[SerializeField] private Component _title;
	[SerializeField] private Button _closeBtn;
	[SerializeField] private Toggle _musicTgl;
	[SerializeField] private Toggle _audioTgl;
	[SerializeField] private Toggle _vibrateTgl;
	[SerializeField] private Button _settingBtn;
	[SerializeField] private Button _homeBtn;
	[SerializeField] private Button _retryBtn;
	[SerializeField] private Button _continueBtn;

	private GameController _controller;
	private CanvasGroup _canvasGroup;
	private float _timeScaleBeforePause = 1f;
	private bool _ownsPause;
	private bool _isClosing;
	private Coroutine _fadeRoutine;
	private Snui.Dialog _confirmDialog;
	private bool _isConfirming;
	private SettingsDialog _settings;
	private bool _settingsLoading;
	private int _modalVersion;

	private void Awake()
	{
		_controller = GameController.Current;
		_canvasGroup = GetComponent<CanvasGroup>();
		AddButtonListener(_closeBtn, Resume);
		AddButtonListener(_continueBtn, Resume);
		AddButtonListener(_retryBtn, Replay);
		AddButtonListener(_homeBtn, ReturnHome);
		AddButtonListener(_settingBtn, ToggleSettings);
		AddToggleListener(_musicTgl, SetMusic);
		AddToggleListener(_audioTgl, SetAudio);
		AddToggleListener(_vibrateTgl, SetVibration);
	}

	private void OnEnable()
	{
		if (!PlayOnStart) return;
		_controller = _controller != null ? _controller : GameController.Current;
		if (_controller != null) _controller.LevelStarted += OnLevelStarted;
		_timeScaleBeforePause = Time.timeScale;
		Time.timeScale = 0f;
		_ownsPause = true;
		_isClosing = false;
		// OnlyOnce buttons stay disabled after closing this reused dialog.
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
		if (_homeBtn != null)
			_homeBtn.interactable = Application.CanStreamedLevelBeLoaded("HomeScene");
		RefreshToggles();
	}

	private void OnDisable()
	{
		if (_controller != null) _controller.LevelStarted -= OnLevelStarted;
		if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
		CancelConfirmation();
		CancelSettings();
		_fadeRoutine = null;
		RestoreTimeScale();
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
		if (_isClosing || _isConfirming || _settingsLoading || _settings != null) return;
		if (ExitAnim && _canvasGroup != null)
		{
			_isClosing = true;
			if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
			_canvasGroup.interactable = false;
			_canvasGroup.blocksRaycasts = false;
			_fadeRoutine = StartCoroutine(CloseAfterFade());
			return;
		}
		CompleteResume();
	}

	private void Replay()
	{
		OpenConfirmation(PauseConfirmType.Replay);
	}

	private void ReturnHome()
	{
		if (!Application.CanStreamedLevelBeLoaded("HomeScene"))
		{
			if (_homeBtn != null) _homeBtn.interactable = false;
			Debug.LogWarning("HomeScene is not enabled in Build Settings.", this);
			return;
		}
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
				if (type == PauseConfirmType.Replay) _controller.TryReplayLevel();
				else _controller.TryReturnHome();
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
	private void OnDestroy()
	{
		CancelSettings();
		if (_closeBtn != null) _closeBtn.onClick.RemoveListener(Resume);
		if (_continueBtn != null) _continueBtn.onClick.RemoveListener(Resume);
		if (_retryBtn != null) _retryBtn.onClick.RemoveListener(Replay);
		if (_homeBtn != null) _homeBtn.onClick.RemoveListener(ReturnHome);
		if (_settingBtn != null) _settingBtn.onClick.RemoveListener(ToggleSettings);
		if (_musicTgl != null) _musicTgl.onValueChanged.RemoveListener(SetMusic);
		if (_audioTgl != null) _audioTgl.onValueChanged.RemoveListener(SetAudio);
		if (_vibrateTgl != null) _vibrateTgl.onValueChanged.RemoveListener(SetVibration);
	}

	private void RestoreTimeScale()
	{
		if (!_ownsPause) return;
		Time.timeScale = _timeScaleBeforePause;
		_ownsPause = false;
	}

	private System.Collections.IEnumerator CloseAfterFade()
	{
		yield return FadeCanvas(_canvasGroup.alpha, 0f, 0.12f);
		_fadeRoutine = null;
		CompleteResume();
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
		gameObject.SetActive(false);
		Destroy(gameObject);
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
	private const string MusicKey = "game_music_enabled";
	private const string EffectsKey = "game_effects_enabled";
	private static readonly Dictionary<AudioSource, bool> OriginalMusicMute = new Dictionary<AudioSource, bool>();

	public static bool MusicEnabled { get; private set; } = PlayerPrefs.GetInt(MusicKey, 1) != 0;
	public static bool EffectsEnabled { get; private set; } = PlayerPrefs.GetInt(EffectsKey, 1) != 0;

	public static void SetMusicEnabled(bool enabled)
	{
		MusicEnabled = enabled;
		PlayerPrefs.SetInt(MusicKey, enabled ? 1 : 0);
		ApplyMusicPreference();
		PlayerPrefs.Save();
	}

	public static void SetEffectsEnabled(bool enabled)
	{
		EffectsEnabled = enabled;
		PlayerPrefs.SetInt(EffectsKey, enabled ? 1 : 0);
		PlayerPrefs.Save();
	}

	public static void ApplyMusicPreference()
	{
		List<AudioSource> destroyedSources = null;
		foreach (KeyValuePair<AudioSource, bool> entry in OriginalMusicMute)
		{
			AudioSource source = entry.Key;
			if (source == null)
			{
				if (destroyedSources == null) destroyedSources = new List<AudioSource>();
				destroyedSources.Add(source);
				continue;
			}
			source.mute = entry.Value || !MusicEnabled;
		}
		if (destroyedSources == null) return;
		for (int i = 0; i < destroyedSources.Count; i++) OriginalMusicMute.Remove(destroyedSources[i]);
	}

	public static void RegisterMusicSource(AudioSource source)
	{
		if (source == null || OriginalMusicMute.ContainsKey(source)) return;
		OriginalMusicMute[source] = source.mute;
		source.mute = source.mute || !MusicEnabled;
	}

	public static void UnregisterMusicSource(AudioSource source)
	{
		if (source != null) OriginalMusicMute.Remove(source);
	}
}
