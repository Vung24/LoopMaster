using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;
using UnityEngine.UI;

public class SettingsDialog : BasePopup
{
	public const string PATH = "SettingsDialog";
	[SerializeField] private Button _backBtn;
	[SerializeField] private ScrollRect _scrollView;
	private SettingButton[] _settingBtns;

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
		settings.PlayEnterAnimation();
		return settings;
	}
	protected override void Awake()
	{
		if (_backBtn == null) _backBtn = HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Descendant(transform, "BackButton"));
		if (_scrollView == null) _scrollView = GetComponentInChildren<ScrollRect>(true);
		_settingBtns = GetComponentsInChildren<SettingButton>(true);
		foreach (var row in _settingBtns)
		{
			row.OnClickCellButton += OnClickButton;
			row.gameObject.SetActive(row.IsOfflineToggle);
		}
		if (_scrollView != null && _scrollView.content != null)
			foreach (Transform node in _scrollView.content)
				if (node.GetComponent<SettingButton>() == null) node.gameObject.SetActive(false);
		_closeBtn = _backBtn;
		base.Awake();
		RefreshToggles();
	}
	protected override void OnOpened() { base.OnOpened(); RefreshToggles(); }
	public void RefreshToggles() { if (_settingBtns != null) foreach (var row in _settingBtns) row.RefreshToggle(); }
	private void OnClickButton(SettingButton.Type type)
	{
		if (!IsOpen) return;
		switch (type)
		{
			case SettingButton.Type.Music: GameAudioSettings.SetMusicEnabled(!GameAudioSettings.MusicEnabled); break;
			case SettingButton.Type.Sound: GameAudioSettings.SetEffectsEnabled(!GameAudioSettings.EffectsEnabled); break;
			case SettingButton.Type.Vibration: GameVibrationHelper.SetEnabled(!GameVibrationHelper.Enabled); break;
		}
		RefreshToggles();
	}
	public bool OnAndroidBackButtonPressed() => TryClose();
	protected override void OnDestroy()
	{
		if (_settingBtns != null) foreach (var row in _settingBtns) if (row != null) row.OnClickCellButton -= OnClickButton;
		base.OnDestroy();
	}
}
