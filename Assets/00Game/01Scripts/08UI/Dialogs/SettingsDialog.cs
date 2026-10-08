using System;
using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;
using UnityEngine.UI;

public class SettingsDialog : MonoBehaviour
{
	public const string PATH = "SettingsDialog";
	[SerializeField] private Button _backBtn;
	[SerializeField] private ScrollRect _scrollView;
	private SettingButton[] _settingBtns;
	private bool _closing, _notified;
	public event Action Closed;
	public bool IsClosing => _closing;

	public static async UniTask<SettingsDialog> Show(Transform parent)
	{
		if (parent == null) return null;
		foreach (var existing in parent.GetComponentsInChildren<SettingsDialog>(true))
			if (existing.transform.parent == parent)
			{
				if (existing.gameObject.activeSelf) return existing;
				Destroy(existing.gameObject);
			}
		var settings = await PopupAssets.Instantiate<SettingsDialog>(PATH, parent, parent.GetCancellationTokenOnDestroy());
		var instance = settings.gameObject;
		instance.transform.SetAsLastSibling();
		var canvas = instance.GetComponent<Canvas>();
		if (canvas == null) canvas = instance.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		var parentCanvas = parent.GetComponentInParent<Canvas>();
		canvas.sortingOrder = Mathf.Max(1200, parentCanvas == null ? 1200 : parentCanvas.sortingOrder + 1);
		if (instance.GetComponent<GraphicRaycaster>() == null) instance.AddComponent<GraphicRaycaster>();
		Snui_Text.EnsureFontAssets(instance.transform);
		GameResultUI.EnsureEventSystem();
		instance.GetComponent<Dialog>()?.PlayEnterAnimation();
		return settings;
	}
	private void Awake()
	{
		if (_backBtn == null) _backBtn = HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Descendant(transform, "BackButton"));
		if (_scrollView == null) _scrollView = GetComponentInChildren<ScrollRect>(true);
		_settingBtns = GetComponentsInChildren<SettingButton>(true);
		foreach (var row in _settingBtns)
		{
			row.OnClickCellButton += OnClickButton;
			row.gameObject.SetActive(row.IsOfflineToggle);
		}
		// Native group dividers refer to the hidden service rows.
		if (_scrollView != null && _scrollView.content != null)
			foreach (Transform node in _scrollView.content)
				if (node.GetComponent<SettingButton>() == null) node.gameObject.SetActive(false);
		if (_backBtn != null) _backBtn.onClick.AddListener(Close);
		RefreshToggles();
	}
	private void OnEnable() { _closing = false; _notified = false; RefreshToggles(); }
	public void RefreshToggles() { if (_settingBtns != null) foreach (var row in _settingBtns) row.RefreshToggle(); }
	private void OnClickButton(SettingButton.Type type)
	{
		if (_closing || !isActiveAndEnabled) return;
		switch (type)
		{
			case SettingButton.Type.Music: GameAudioSettings.SetMusicEnabled(!GameAudioSettings.MusicEnabled); break;
			case SettingButton.Type.Sound: GameAudioSettings.SetEffectsEnabled(!GameAudioSettings.EffectsEnabled); break;
			case SettingButton.Type.Vibration: GameVibrationHelper.SetEnabled(!GameVibrationHelper.Enabled); break;
		}
		RefreshToggles();
	}
	private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) OnAndroidBackButtonPressed(); }
	public bool OnAndroidBackButtonPressed() { if (!isActiveAndEnabled) return false; Close(); return true; }
	public void Close()
	{
		if (_closing) return;
		_closing = true;
		var panel = GetComponent<Snui_Panel>();
		if (panel != null) panel.PlayExitAnimation();
		else { gameObject.SetActive(false); Destroy(gameObject); }
	}
	private void NotifyClosed() { if (_notified) return; _notified = true; Closed?.Invoke(); Closed = null; }
	private void OnDisable() => NotifyClosed();
	private void OnDestroy()
	{
		if (_backBtn != null) _backBtn.onClick.RemoveListener(Close);
		if (_settingBtns != null) foreach (var row in _settingBtns) if (row != null) row.OnClickCellButton -= OnClickButton;
		NotifyClosed();
	}
}
