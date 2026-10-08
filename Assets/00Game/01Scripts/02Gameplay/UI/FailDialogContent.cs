using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Snui;

public class FailDialogContent : DialogContent
{
	[SerializeField] private Button _adBtn;
	[SerializeField] private Button _coinBtn;
	[SerializeField] private Button _coinOnlyBtn;
	[SerializeField] private Button _freeBtn;
	[SerializeField] private Transform _addPathNode;
	[SerializeField] private Transform _safeArea;
	[SerializeField] private LoseContainer _loseContainer;
	[SerializeField] private Text _coinNumberText;
	[SerializeField] private TMP_Text _coinNumberTmpText;
	public string ReviveRoute;

	private GameController _controller;
	private Text _fallbackTitle;
	private Text _fallbackReason;
	private Button _fallbackReplayButton;
	private GameData _offeredData;
	private bool _isResolved;
	private System.Action _declined;
	private bool _isFreeRevive;
	public const int RevivePrice = 900;
	public Transform FreeButtonNode => _freeBtn == null ? null : _freeBtn.transform;
	public Transform SafeArea => _safeArea != null ? _safeArea : (_safeArea = HierarchyReferenceCache.Child(transform, "SafeArea"));

	protected override void Awake()
	{
		base.Awake();
		if (_safeArea == null) _safeArea = HierarchyReferenceCache.Child(transform, "SafeArea");
		_adBtn = _adBtn != null ? _adBtn : DialogUI.ResolveButton(transform, "AdButton");
		_coinBtn = _coinBtn != null ? _coinBtn : DialogUI.ResolveButton(transform, "CoinButton");
		_coinOnlyBtn = _coinOnlyBtn != null ? _coinOnlyBtn : DialogUI.ResolveButton(transform, "CoinOnlyButton");
		_freeBtn = _freeBtn != null ? _freeBtn : DialogUI.ResolveButton(transform, "FreeButton");
		_closeBtn = _closeBtn != null ? _closeBtn : DialogUI.ResolveButton(transform, "CloseButton");
		if (_coinNumberTmpText == null && _coinNumberText == null && _coinBtn != null)
		{
			Transform numNode = HierarchyReferenceCache.Descendant(_coinBtn.transform, "Number");
			if (numNode != null)
			{
				_coinNumberTmpText = numNode.GetComponent<TMP_Text>();
				if (_coinNumberTmpText == null) _coinNumberText = numNode.GetComponent<Text>();
			}
		}
		Bind(_controller != null ? _controller : GameController.Current);
	}

	public void Bind(GameController controller)
	{
		_controller = controller;
		BindButton(_closeBtn, OnClickCloseButton);
		BindButton(_freeBtn, OnClickFreeButton);
		BindButton(_adBtn, OnClickOfflineFreeButton);
		BindButton(_coinBtn, OnClickCoinButton);
		BindButton(_coinOnlyBtn, OnClickCoinButton);
		BindButton(_fallbackReplayButton, OnClickReplayLevel);

		UpdateButtons();
	}

	public void Configure(bool freeRevive, System.Action declined = null)
	{
		_isFreeRevive = freeRevive;
		_declined = declined;
		UpdateButtons();
	}
	public void BindFallbackOffers()
	{
		_adBtn = _adBtn != null ? _adBtn : DialogUI.ResolveButton(transform, "AdButton");
		_coinBtn = _coinBtn != null ? _coinBtn : DialogUI.ResolveButton(transform, "CoinButton");
		DialogUI.Bind(_adBtn, OnClickOfflineFreeButton);
		DialogUI.Bind(_coinBtn, OnClickCoinButton);
	}
	public override void Show()
	{
		_offeredData = _controller == null ? null : _controller.Data;
		_isResolved = false;
		// Native Start 0x15BDC0C hides the loss mascot while offering extra capacity/revive.
		if (_loseContainer != null) _loseContainer.gameObject.SetActive(false);
		base.Show();
		UpdateButtons();
	}
	public override void Hide()
	{
		// Return highlighted capacity before deactivating its temporary tutorial parent.
		foreach (NewPlayerTutorial tutorial in GetComponentsInChildren<NewPlayerTutorial>(true))
			if (tutorial != null) tutorial.CancelAndDismiss();
		base.Hide();
	}
	private bool OfferValid => !_isResolved && isActiveAndEnabled && _controller != null &&
		_controller.Data == _offeredData && _controller.CanRevive;
	public void OnClickCoinButton() => TryRevive(true, "coin");
	public void OnClickOfflineFreeButton() => TryRevive(false, "offline_free");
	public void OnClickFreeButton()
	{
		if (_isFreeRevive) TryRevive(false, "free");
	}
	private void TryRevive(bool useCoins, string way)
	{
		if (!OfferValid) return;
		_isResolved = true;
		bool success = useCoins ? InventoryManager.Ensure().TrySpendCoins(RevivePrice,
			() => _controller.TryRevive(way, ReviveRoute)) : _controller.TryRevive(way, ReviveRoute);
		if (!success)
		{
			_isResolved = false;
			SetTitle(useCoins ? "NEED 900 COINS" : "TRY AGAIN");
			return;
		}
		if (_isFreeRevive) { PlayerPrefs.SetInt("offline_free_revive_used", 1); PlayerPrefs.Save(); }
		Dismiss(way == "coin" ? 1 : way == "free" ? 3 : 2);
	}
	private void UpdateButtons()
	{
		if (_closeBtn != null) _closeBtn.interactable = true;
		if (_freeBtn != null)
		{
			_freeBtn.gameObject.SetActive(_isFreeRevive);
			DialogUI.HideNamed(_freeBtn.transform, "TipCoin");
			DialogUI.HideNamed(_freeBtn.transform, "TipAd");
			SetButtonLabel(_freeBtn, "FREE");
		}
		if (_adBtn != null)
		{
			_adBtn.gameObject.SetActive(!_isFreeRevive || _freeBtn == null);
			DialogUI.HideNamed(_adBtn.transform, "Ad");
			DialogUI.HideNamed(_adBtn.transform, "Image");
			DialogUI.HideNamed(_adBtn.transform, "TipAd");
			SetButtonLabel(_adBtn, "FREE");
		}
		if (_coinBtn != null)
		{
			_coinBtn.gameObject.SetActive(!_isFreeRevive || _freeBtn == null);
			if (_coinNumberTmpText != null) _coinNumberTmpText.text = RevivePrice.ToString();
			else if (_coinNumberText != null) _coinNumberText.text = RevivePrice.ToString();
			else DialogUI.Label(HierarchyReferenceCache.Descendant(_coinBtn.transform, "Number"), RevivePrice.ToString());
		}
		SetUnavailable(_coinOnlyBtn);
		if (_addPathNode != null) _addPathNode.gameObject.SetActive(true);
	}

	public void Bind(GameController controller, Text title, Text reason, Button replayButton)
	{
		_fallbackTitle = title;
		_fallbackReason = reason;
		_fallbackReplayButton = replayButton;
		Bind(controller);
	}

	public void SetLevel(int level, string failReason)
	{
		string title = "LEVEL " + level + " FAILED";
		string reason = failReason == "roadsfull" || failReason == "road_full" ? "THE ROAD IS FULL" : "NO AVAILABLE MOVE";
		SetTitle("CONTINUE?");
		if (_fallbackTitle != null) _fallbackTitle.text = title;
		if (_fallbackReason != null) _fallbackReason.text = reason;
	}

	public void OnClickReplayLevel()
	{
		if (_isResolved || !isActiveAndEnabled || _controller == null || _controller.Data != _offeredData) return;
		_isResolved = true;
		if (!_controller.TryReplayLevel()) _isResolved = false;
	}

	public void OnClickRetryLevel() { OnClickReplayLevel(); }

	protected override void OnClickCloseButton()
	{
		if (!OfferValid) return;
		if (_declined == null) { OnClickReplayLevel(); return; }
		_isResolved = true;
		Dismiss(RESULT_CLOSE);
		_declined.Invoke();
	}

	private static void SetUnavailable(Button button)
	{
		if (button != null) button.gameObject.SetActive(false);
	}

	private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
	{
		if (button == null) return;
		button.onClick.RemoveListener(callback);
		button.onClick.AddListener(callback);
	}

	private static void SetButtonLabel(Button button, string label)
	{
		if (button == null) return;
		Transform content = HierarchyReferenceCache.Child(button.transform, "Content") ?? button.transform;
		DialogUI.Label(content, label);
		foreach (TMP_Text text in content.GetComponentsInChildren<TMP_Text>(true))
		{
			text.enableAutoSizing = true;
			text.fontSizeMin = 20f;
			text.fontSizeMax = 40f;
		}
	}
}
