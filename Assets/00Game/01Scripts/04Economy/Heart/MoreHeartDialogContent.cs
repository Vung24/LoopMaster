using System;
using Cysharp.Threading.Tasks;
using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoreHeartDialogContent : DialogContent
{
	public const string PATH = "MoreHeartDialogContent";
	public static readonly int RESULT_JUMP_HEART_BANK = 1;
	public static readonly int RESULT_ENTER_GAME = 2;
	[SerializeField] private TMP_Text _timeLabel, _heartNum, _coinCount;
	[SerializeField] private Button _refillBtn, _rvBtn;
	[SerializeField] private Snui_Button _extraBtn;
	private Dialog _overlay;
	private bool _notified;
	public event Action Closed;
	public Button CloseButton => _closeBtn;
	public Button ConfirmButton => _refillBtn;

	protected override void Awake()
	{
		base.Awake();
		if (_closeBtn == null) _closeBtn = DialogUI.ResolveButton(transform, "CloseButton");
		if (_refillBtn == null) _refillBtn = DialogUI.ResolveButton(transform, "RefillButton");
		DialogUI.Bind(_closeBtn, OnClickCloseButton);
		DialogUI.Bind(_refillBtn, OnConfirmClicked);
	}
	public static async UniTask<MoreHeartDialogContent> Show(Transform parent, JumpHeartDialogSourceScene sourceScene)
	{
		if (parent == null) return null;
		foreach (var existing in parent.GetComponentsInChildren<MoreHeartDialogContent>(true))
			if (existing.isActiveAndEnabled) return existing;
		var content = await Dialog.ShowContent<MoreHeartDialogContent>(parent, PATH);
		content._overlay = content.GetComponentInParent<Dialog>();
		RewardDialog.PrepareOverlay(content._overlay.gameObject, 1200);
		content.Init(sourceScene);
		return content;
	}
	public static UniTask<MoreHeartDialogContent> ShowInDialog(JumpHeartDialogSourceScene source)
	{
		var home = HomeController.Current;
		Transform parent = home == null ? Dialog.DefaultParent : home.HomePage.GetComponentInParent<Canvas>().rootCanvas.transform;
		if (parent == null && UICanvas.Current != null) parent = UICanvas.Current.transform;
		return Show(parent, source);
	}
	public void Init(JumpHeartDialogSourceScene scene)
	{
		var heartMgr = Managers.Heart;
		bool isInfinite = heartMgr != null && heartMgr.IsInfinite();
		DialogUI.Label(HierarchyReferenceCache.Child(transform, "Title"), "LIVES");
		if (_heartNum != null)
		{
			_heartNum.text = isInfinite ? "∞" : (heartMgr != null ? heartMgr.GetHeartCount().ToString() : "5");
			_heartNum.enableAutoSizing = true; _heartNum.fontSizeMin = 24f; _heartNum.fontSizeMax = 60f;
		}
		if (_timeLabel != null)
		{
			if (isInfinite)
			{
				_timeLabel.text = TimeFormatter.FormatTimeLeft(heartMgr.InfiniteLeftTime);
			}
			else if (heartMgr != null && !heartMgr.IsHeartFull())
			{
				_timeLabel.text = TimeFormatter.FormatSeconds(heartMgr.GetHeartRefillTime());
			}
			else
			{
				_timeLabel.text = "FULL";
			}
			_timeLabel.enableAutoSizing = true; _timeLabel.fontSizeMin = 18f; _timeLabel.fontSizeMax = 34f;
			_timeLabel.enableWordWrapping = false;
		}
		if (_rvBtn != null) _rvBtn.gameObject.SetActive(false);
		if (_extraBtn != null) _extraBtn.gameObject.SetActive(false);
		if (_coinCount != null) _coinCount.gameObject.SetActive(false);
		if (_refillBtn != null)
		{
			DialogUI.HideNamed(_refillBtn.transform, "Coin");
			var label = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Descendant(_refillBtn.transform, "Snui_Text"));
			if (label != null)
			{
				label.text = "OK";
				label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
				label.rectTransform.anchoredPosition = Vector2.zero;
			}
		}
		Snui_Text.EnsureFontAssets(transform);
	}
	private void OnConfirmClicked() => Dismiss();
	private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) OnAndroidBackButtonPressed(); }
	public void CloseOverlay()
	{
		if (_overlay != null) Destroy(_overlay.gameObject);
		else Destroy(gameObject);
	}
	private void NotifyClosed()
	{
		if (_notified) return;
		_notified = true; Closed?.Invoke(); Closed = null;
	}
	private void OnDisable() => NotifyClosed();
	private void OnDestroy()
	{
		if (_closeBtn != null) _closeBtn.onClick.RemoveListener(OnClickCloseButton);
		if (_refillBtn != null) _refillBtn.onClick.RemoveListener(OnConfirmClicked);
		NotifyClosed();
	}
}
