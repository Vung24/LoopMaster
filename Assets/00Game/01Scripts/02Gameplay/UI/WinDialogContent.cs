using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Snui;

public class WinDialogContent : DialogContent
{
	[SerializeField] private Transform _artTitle;
	[SerializeField] private Button _claimButtonCenter;
	[SerializeField] private Button _claimButton;
	[SerializeField] private Button _adButton;
	[SerializeField] private Transform _coinsNode;
	[SerializeField] private Transform _coinsBgLight;
	[SerializeField] private ParticleSystem _coinShineEffect;
	[SerializeField] private ParticleSystem _coinSparkEffect;
	[SerializeField] private Component _coinsSpine;
	[SerializeField] private Transform _featureProgressNode;
	[SerializeField] private Component _featureProgressBar;
	[SerializeField] private Image _featureProgressIcon;
	[SerializeField] private ParticleSystem _featureProgressEffect;
	[SerializeField] private Transform _featureProgressIconBackLight;
	[SerializeField] private AudioClip _showAudioClip;
	[SerializeField] private AudioClip _coinAudioClip;
	[SerializeField] private AudioClip _coinCollectAudioClip;
	[SerializeField] private GameObject _flyCoinAsset;

	private Button _fallbackNextButton;
	private Button _fallbackReplayButton;
	private Text _fallbackTitle;
	private GameController _controller;
	private bool _canAdvance;
	private GameData _offeredData;
	private Coroutine _claimRoutine;
	private ClaimCoinAnim _coinAnimation;
	private bool _isClaiming;
	private bool _isNavigating;
	private Sequence _enterAnimation;
	private readonly Dictionary<Transform, Vector3> _authoredScales = new Dictionary<Transform, Vector3>();

	public override void Show()
	{
		CancelClaimAnimation();
		_offeredData = _controller == null ? null : _controller.Data;
		_isNavigating = false;
		CancelEnterAnimation();
		base.Show();
		RefreshRewardButtons();
		PlayCoinSpine("open");
		foreach (ParticleSystem particle in new[] { _coinShineEffect, _coinSparkEffect })
			if (particle != null) { var main = particle.main; main.useUnscaledTime = true; particle.Play(true); }
		if (AudioManager.Current != null) AudioManager.Current.PlayOneShot(_showAudioClip, gameObject);
		_enterAnimation = DOTween.Sequence().SetUpdate(true);
		PopNode(_artTitle, 0f);
		PopNode(_coinsNode, 0f);
		PopButton(_claimButtonCenter, 0.06f);
		PopButton(_claimButton, 0.06f);
		PopButton(_adButton, 0.06f);
		// The full WinDialog prefab has an opaque mask above ordinary canvas siblings.
		HudContainer.TrySetTop();
	}

	private void PopButton(Button button, float delay)
	{
		if (button != null && button.gameObject.activeSelf) PopNode(button.transform, delay);
	}

	private void PopNode(Transform node, float delay)
	{
		if (node == null || !node.gameObject.activeSelf) return;
		if (!_authoredScales.TryGetValue(node, out Vector3 scale))
		{
			scale = node.localScale;
			_authoredScales.Add(node, scale);
		}
		CanvasGroup group = node.GetComponent<CanvasGroup>();
		if (group == null) group = node.gameObject.AddComponent<CanvasGroup>();
		group.alpha = 0f;
		_enterAnimation.Insert(delay, group.DOFade(1f, 0.16f).SetEase(Ease.OutSine));
		// Use From so cancellation restores the authored scale through Complete.
		// Native literal pool: 0.88 start scale and 1.04 end scale.
		_enterAnimation.Insert(delay, node.DOScale(scale * 1.04f, 0.18f).From(scale * 0.88f).SetEase(Ease.OutBack, 1.2f));
	}

	public void OnSkipAnims() { CancelEnterAnimation(); }

	public override void Hide()
	{
		CancelClaimAnimation();
		CancelEnterAnimation();
		base.Hide();
	}

	private void OnDisable() { CancelEnterAnimation(); CancelClaimAnimation(); }

	private void CancelClaimAnimation()
	{
		if (_claimRoutine != null) StopCoroutine(_claimRoutine);
		_claimRoutine = null;
		_coinAnimation?.Cancel();
		_isClaiming = false;
		HudContainer.TryRevertToOriginLayer();
		if (AudioManager.Current != null) AudioManager.Current.StopAudio(gameObject);
	}

	private float PlayCoinSpine(string name)
	{
		SkeletonGraphic graphic = _coinsSpine as SkeletonGraphic;
		if (graphic == null || graphic.skeletonDataAsset == null) return 0f;
		graphic.gameObject.SetActive(true);
		graphic.UnscaledTime = true;
		graphic.raycastTarget = false;
		graphic.Initialize(false);
		var data = graphic.skeletonDataAsset.GetSkeletonData(false);
		var animation = data == null ? null : data.FindAnimation(name);
		if (animation == null || graphic.AnimationState == null) return 0f;
		graphic.Skeleton.SetToSetupPose();
		graphic.AnimationState.ClearTracks();
		graphic.AnimationState.SetAnimation(0, name, false);
		return animation.Duration;
	}

	private void OnLevelStarted(int level) { Hide(); _offeredData = null; }
	private void OnDestroy()
	{
		if (_controller != null) _controller.LevelStarted -= OnLevelStarted;
		CancelClaimAnimation();
		CancelEnterAnimation();
	}

	private void CancelEnterAnimation()
	{
		if (_enterAnimation != null)
		{
			_enterAnimation.Complete(true);
			_enterAnimation.Kill();
		}
		_enterAnimation = null;
	}

	public void Bind(GameController controller)
	{
		if (_controller != controller) CancelClaimAnimation();
		if (_controller != null) _controller.LevelStarted -= OnLevelStarted;
		_controller = controller;
		if (_controller != null) _controller.LevelStarted += OnLevelStarted;
		if (_artTitle != null) _artTitle.gameObject.SetActive(true);
		if (_coinsNode != null) _coinsNode.gameObject.SetActive(true);
		if (_featureProgressNode != null) _featureProgressNode.gameObject.SetActive(false);
		BindButton(_claimButtonCenter, OnClickNextLevel);
		BindButton(_claimButton, OnClickNextLevel);
		BindButton(_adButton, OnClickDoubleReward);
		if (_claimButtonCenter != null) _claimButtonCenter.gameObject.SetActive(false);
		if (_claimButton != null) _claimButton.gameObject.SetActive(true);
		if (_adButton != null) _adButton.gameObject.SetActive(true);
		RestoreRewardIcons(_claimButton);
		RestoreRewardIcons(_adButton);
		BindButton(_fallbackNextButton, OnClickNextLevel);
		BindButton(_fallbackReplayButton, OnClickDoubleReward);
		RefreshRewardButtons();
	}

	public void Bind(GameController controller, Text title, Button nextButton, Button replayButton)
	{
		_fallbackTitle = title;
		_fallbackNextButton = nextButton;
		_fallbackReplayButton = replayButton;
		Bind(controller);
	}

	public void SetLevel(int level, bool canLoadNext)
	{
		_canAdvance = canLoadNext;
		string title = canLoadNext ? "LEVEL " + level + " COMPLETE" : GameProgressStore.IsCampaignComplete ? "ALL LEVELS COMPLETE" : "NEXT LEVEL DATA MISSING";
		SetArtTitle(title);
		if (_fallbackTitle != null)
			_fallbackTitle.text = title;
		RefreshRewardButtons();
	}

	public void SetNoMoreLevels()
	{
		_canAdvance = false;
		string title = GameProgressStore.IsCampaignComplete ? "ALL LEVELS COMPLETE" : "NEXT LEVEL DATA MISSING";
		SetArtTitle(title);
		if (_fallbackTitle != null) _fallbackTitle.text = title;
		RefreshRewardButtons();
	}

	public void OnClickNextLevel()
	{
		ClaimReward(false);
	}
	public void OnClickDoubleReward() { ClaimReward(true); }

	private bool OfferValid => isActiveAndEnabled && _controller != null && _offeredData != null &&
		_controller.Data == _offeredData && _controller.EndState == GameEndState.Won;

	private void ClaimReward(bool doubled)
	{
		if (!OfferValid || _isClaiming || _isNavigating) return;
		if (_controller.HasClaimedWinReward(_offeredData)) { AdvanceAfterClaim(); return; }
		_isClaiming = true;
		RefreshRewardButtons();
		if (!_controller.TryClaimWinReward(_offeredData, doubled, out int coins))
		{
			_isClaiming = false;
			SetArtTitle("COULD NOT ADD COINS — TRY AGAIN");
			if (_fallbackTitle != null) _fallbackTitle.text = "COULD NOT ADD COINS — TRY AGAIN";
			RefreshRewardButtons();
			return;
		}
		if (!OfferValid) { CancelClaimAnimation(); return; }
		_claimRoutine = StartCoroutine(CollectAndAdvance(coins, doubled));
	}

	private IEnumerator CollectAndAdvance(int coins, bool doubled)
	{
		yield return null;
		if (!OfferValid) { CancelClaimAnimation(); yield break; }
		float spineDuration = PlayCoinSpine(doubled ? "claim_double" : "claim_direct");
		if (doubled && spineDuration > 0f) yield return new WaitForSecondsRealtime(spineDuration);
		if (!OfferValid) { CancelClaimAnimation(); yield break; }
		Canvas canvas = GetComponentInParent<Canvas>();
		Transform parent = canvas == null ? transform.parent : canvas.rootCanvas.transform;
		if (_coinAnimation == null) _coinAnimation = GetComponent<ClaimCoinAnim>();
		if (_coinAnimation == null) _coinAnimation = gameObject.AddComponent<ClaimCoinAnim>();
		_coinAnimation.ShowSound = _coinAudioClip;
		_coinAnimation.CollectSound = _coinCollectAudioClip;
		HudContainer.TrySetTop();
		Snui_CoinHud hud = HudContainer.Current == null ? Snui_CoinHud.Current : HudContainer.Current.CoinHud;
		Vector3 start = _coinsSpine != null ? _coinsSpine.transform.position : transform.position;
		yield return _coinAnimation.Play(coins, parent, start, hud, _flyCoinAsset);
		_claimRoutine = null;
		_isClaiming = false;
		HudContainer.TryRevertToOriginLayer();
		if (OfferValid) AdvanceAfterClaim();
	}

	private void AdvanceAfterClaim()
	{
		if (!OfferValid || _isNavigating) return;
		_isNavigating = true;
		bool changed = _canAdvance && _controller.TryLoadNextLevel();
		if (!changed && OfferValid) changed = _controller.TryReturnHome();
		if (!changed) { _isNavigating = false; RefreshRewardButtons(); }
	}

	public void OnClickReplayLevel() { if (OfferValid && !_isClaiming && !_isNavigating) _controller.TryReplayLevel(); }

	private void RefreshRewardButtons()
	{
		GameData data = _offeredData ?? (_controller == null ? null : _controller.Data);
		bool claimed = _controller != null && _controller.HasClaimedWinReward(data);
		int coins = _controller == null ? 0 : _controller.GetWinRewardCoins(data);
		string normal = claimed ? (_canAdvance ? "NEXT LEVEL" : "HOME") : "CLAIM " + coins;
		string doubled = "FREE x2 (OFFLINE)\n" + (coins * 2);
		foreach (Button button in new[] { _claimButton, _claimButtonCenter, _fallbackNextButton })
		{
			SetButtonLabel(button, normal);
			if (button != null) button.interactable = !_isClaiming && !_isNavigating;
		}
		foreach (Button button in new[] { _adButton, _fallbackReplayButton })
		{
			SetButtonLabel(button, doubled);
			if (button != null) { button.gameObject.SetActive(!claimed); button.interactable = !_isClaiming && !_isNavigating; }
		}
	}

	private void SetArtTitle(string label)
	{
		if (_artTitle == null) return;
		Snui_Text[] titleLabels = _artTitle.GetComponentsInChildren<Snui_Text>(true);
		for (int i = 0; i < titleLabels.Length; i++)
			if (titleLabels[i] != null) titleLabels[i].SetText(label);
		TMP_Text[] tmpLabels = _artTitle.GetComponentsInChildren<TMP_Text>(true);
		for (int i = 0; i < tmpLabels.Length; i++)
			if (tmpLabels[i] != null) tmpLabels[i].text = label;
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
		Snui_Text[] labels = button.GetComponentsInChildren<Snui_Text>(true);
		for (int i = 0; i < labels.Length; i++)
			if (labels[i] != null) labels[i].SetText(label);
		TMP_Text[] tmpLabels = button.GetComponentsInChildren<TMP_Text>(true);
		for (int i = 0; i < tmpLabels.Length; i++)
			if (tmpLabels[i] != null)
			{
				tmpLabels[i].text = label;
				tmpLabels[i].enableAutoSizing = true;
				tmpLabels[i].fontSizeMin = 16f;
				tmpLabels[i].fontSizeMax = label.Contains("OFFLINE") ? 28f : 36f;
				// The authored horizontal layout sizes text from preferred width.
				// Reserve a bounded column so the longer offline label stays inside the button.
				LayoutElement column = tmpLabels[i].GetComponent<LayoutElement>();
				if (column == null) column = tmpLabels[i].gameObject.AddComponent<LayoutElement>();
				column.minWidth = 0f;
				column.preferredWidth = label.Contains("OFFLINE") ? 220f : 180f;
				column.flexibleWidth = 0f;
				column.minHeight = 72f; column.preferredHeight = 72f;
			}
		foreach (Text text in button.GetComponentsInChildren<Text>(true)) text.text = label;
	}

	private static void RestoreRewardIcons(Button button)
	{
		if (button == null) return;
		Transform content = button.transform.Find("Content");
		Transform[] nodes = button.GetComponentsInChildren<Transform>(true);
		for (int i = 1; i < nodes.Length; i++)
			if (nodes[i].name == "Coin")
			{
				nodes[i].gameObject.SetActive(true);
				if (content == null || nodes[i].parent != content) continue;
				// The embedded content in WinDialog lacks the layout component present
				// on the standalone WinDialogContent prefab. Use the same bounded row.
				HorizontalLayoutGroup row = content.GetComponent<HorizontalLayoutGroup>();
				if (row == null) row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
				row.childAlignment = TextAnchor.MiddleCenter;
				row.childControlWidth = true; row.childControlHeight = true;
				row.childForceExpandWidth = false; row.childForceExpandHeight = false;
				row.spacing = 12f;
				nodes[i].SetAsFirstSibling();
				LayoutElement icon = nodes[i].GetComponent<LayoutElement>();
				if (icon == null) icon = nodes[i].gameObject.AddComponent<LayoutElement>();
				icon.minWidth = 72f; icon.preferredWidth = 72f; icon.flexibleWidth = 0f;
				icon.minHeight = 72f; icon.preferredHeight = 72f;
			}
			else if (nodes[i].name == "Ad") nodes[i].gameObject.SetActive(false);
	}
}
