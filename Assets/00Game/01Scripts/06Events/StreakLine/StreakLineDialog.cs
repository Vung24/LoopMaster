using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;

namespace Snui.StreakLine
{
	public class StreakLineDialog : DialogContent
	{
		[SerializeField]
		private StreakLineScrollAdapter _scrollAdapter;
		[SerializeField]
		private RectTransform _balloon;
		[SerializeField]
		private Button _infoBtn;
		[SerializeField]
		private Button _claimBtn;
		[SerializeField]
		private TMP_Text _titleText;
		[SerializeField]
		private ParticleSystem _fireworks;
		private bool _started;
		protected virtual AnimationCurve RiseCurve => AnimationCurve.EaseInOut(0, 0, 1, 1);
		protected StreakLineScrollAdapter ScrollView => _scrollAdapter;
		private bool _animating, _claiming, _infoShowing, _showingRewards, _tutorialWaiting;
		private CanvasGroup _claimGroup;
		private Tween _claimFade;
		private bool _claimShown;
		private StreakLineInfoDialog _infoDialog;
		private bool Busy => _animating || _claiming || _infoShowing || _showingRewards;
		public bool CanPlayPendingProgress => _started && !Busy && isActiveAndEnabled &&
			_scrollAdapter != null && _scrollAdapter.IsInitialized;

		public static UniTask<StreakLineDialog> Show(Transform parent) => EventPrefabUI.Show<StreakLineDialog>(parent, "StreakLineDialog");

		protected override void Awake()
		{
			base.Awake();
			if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
			if (_closeBtn == null)
			{
				_closeBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseBtn")
					?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseButton");
				if (_closeBtn != null) _closeBtn.onClick.AddListener(OnClickCloseButton);
			}
			_scrollAdapter = _scrollAdapter != null ? _scrollAdapter : GetComponentInChildren<StreakLineScrollAdapter>(true);
			_balloon = _balloon != null ? _balloon : HierarchyReferenceCache.DescendantComponent<RectTransform>(transform, "Dog")
				?? HierarchyReferenceCache.DescendantComponent<RectTransform>(transform, "Balloon");
			_infoBtn = _infoBtn != null ? _infoBtn : HierarchyReferenceCache.DescendantComponent<Button>(transform, "InfoButton");
			_claimBtn = _claimBtn != null ? _claimBtn : HierarchyReferenceCache.PathComponent<Button>(transform, "SafeArea", "ClaimButton");
			if (_infoBtn != null) _infoBtn.onClick.AddListener(OnClickInfoButton);
			if (_claimBtn != null) _claimBtn.onClick.AddListener(OnClickClaimButton);
			if (_claimBtn != null) _claimGroup = _claimBtn.GetComponent<CanvasGroup>() ?? _claimBtn.gameObject.AddComponent<CanvasGroup>();
			_scrollAdapter?.Prepare(Managers.StreakLine, _balloon);
			if (_scrollAdapter != null) _scrollAdapter.Claimed += OnClaimedRewards;
		}

		protected override void Start()
		{
			base.Start();
			Canvas.ForceUpdateCanvases();
			_started = true;
			Init();
			StartAsync().Forget();
		}

		public void Init()
		{
			if (Managers.StreakLine == null) return;
			if (_titleText != null && string.IsNullOrEmpty(_titleText.text)) _titleText.text = Managers.StreakLine.GetTitleI18n();
			if (_started) _scrollAdapter?.RefreshRewards(Managers.StreakLine, true);
			HideClaimButton();
		}

		public async UniTaskVoid StartAsync()
		{
			if (Managers.StreakLine == null || _scrollAdapter == null || !_scrollAdapter.IsInitialized) return;
			_animating = true;
			UpdateInteractions();
			if (_closeBtn != null && !Managers.StreakLine.HasPlayedTutorial()) _closeBtn.gameObject.SetActive(false);
			try
			{
				_scrollAdapter.Parameters.ScrollEnabled = false;
				OnEntryStarted(!Managers.StreakLine.HasPlayedEnterAnim());
				if (!Managers.StreakLine.HasPlayedEnterAnim() && _scrollAdapter.GetItemsCount() > 0)
				{
					_scrollAdapter.SetVirtualAbstractNormalizedScrollPosition(1, true, out _);
					Managers.StreakLine.SetPlayedEnterAnim();
					var enterScroll = DOVirtual.Float(1, 0, 3f,
						position => _scrollAdapter.SetVirtualAbstractNormalizedScrollPosition(position, true, out _))
						.SetEase(Ease.InOutQuart).SetLink(gameObject);
					await UniTask.Delay(1200, cancellationToken: this.GetCancellationTokenOnDestroy());
					await UniTask.WhenAll(enterScroll.ToUniTask(), PlayBalloonEnter());
				}
				else await PlayBalloonEnter();
				if (this == null || !isActiveAndEnabled) return;
				if (!Managers.StreakLine.HasPlayedTutorial())
				{
					await ShowTutorial();
					if (this == null || !isActiveAndEnabled) return;
					Managers.StreakLine.OnTutorialShown();
					if (_closeBtn != null) _closeBtn.gameObject.SetActive(true);
				}
				if (Managers.StreakLine.GetTempViewLevel() > 0) await PlayBalloonRise();
				if (this == null || !isActiveAndEnabled) return;
				Managers.StreakLine.OnDetailShown();
				RefreshClaimButton();
			}
			finally
			{
				if (this != null) { _animating = false; UpdateInteractions(); }
			}
		}

		private void UpdateInteractions()
		{
			if (_canvasGroup != null) _canvasGroup.interactable = !Busy || (_tutorialWaiting && !_infoShowing);
			if (_closeBtn != null) _closeBtn.interactable = !Busy;
			if (_claimBtn != null) _claimBtn.interactable = !Busy;
			if (_infoBtn != null) _infoBtn.interactable = (!Busy || _tutorialWaiting) && !_infoShowing;
			if (_scrollAdapter != null) _scrollAdapter.Parameters.ScrollEnabled = !Busy;
		}

		private async UniTask ShowTutorial()
		{
			if (_infoBtn == null) return;
			var originalParent = _infoBtn.transform.parent;
			int originalSibling = _infoBtn.transform.GetSiblingIndex();
			var mask = UiUtils.CreateMask(transform);
			Finger finger = null;
			try
			{
				mask.DOFade(.98f, .1f).SetLink(gameObject);
				_infoBtn.transform.SetParent(mask.transform, true);
				_tutorialWaiting = true;
				UpdateInteractions();
				await UniTask.Delay(300, cancellationToken: this.GetCancellationTokenOnDestroy());
				if (PrefabLibrary.Current?.FingerPrefab != null)
				{
					finger = await Finger.Show(mask.transform);
					finger.ShowFinger(((RectTransform)mask.transform).InverseTransformPoint(_infoBtn.transform.position));
				}
				await UniTask.WaitUntil(() => _infoShowing, cancellationToken: this.GetCancellationTokenOnDestroy());
				_infoBtn.transform.SetParent(originalParent, true);
				_infoBtn.transform.SetSiblingIndex(originalSibling);
				Destroy(mask.gameObject);
				await UniTask.WaitUntil(() => !_infoShowing, cancellationToken: this.GetCancellationTokenOnDestroy());
			}
			finally
			{
				if (this != null)
				{
					if (_infoBtn != null && originalParent != null) { _infoBtn.transform.SetParent(originalParent, true); _infoBtn.transform.SetSiblingIndex(originalSibling); }
					if (mask != null) Destroy(mask.gameObject);
					if (finger != null) Destroy(finger.gameObject);
					_tutorialWaiting = false;
					UpdateInteractions();
				}
			}
		}

		protected virtual void OnBalloonRiseStarted() { }
		protected virtual void OnEntryStarted(bool firstEntry) { }
		protected virtual UniTask PlayBalloonRiseCompleted() => UniTask.CompletedTask;

		protected virtual UniTask PlayBalloonEnter() => UniTask.CompletedTask;

		public StreakLineProgress GetProgressByLevel(int level)
		{
			return _scrollAdapter?.GetProgressByLevel(level);
		}

		public async UniTask PlayBalloonRise()
		{
			if (Managers.StreakLine == null || _scrollAdapter == null) return;
			OnBalloonRiseStarted();
			int targetLevel = Managers.StreakLine.GetCurrLevel();
			await _scrollAdapter.IncreaseToLevel(targetLevel, RiseCurve);
			if (this == null || !isActiveAndEnabled) return;
			await PlayBalloonRiseCompleted();
		}

		public async UniTask PlayPendingProgress()
		{
			if (!CanPlayPendingProgress || Managers.StreakLine == null) return;
			_animating = true;
			UpdateInteractions();
			try
			{
				Managers.StreakLine.SynchronizeTempLevel();
				if (Managers.StreakLine.GetTempViewLevel() > 0) await PlayBalloonRise();
				if (this == null || !isActiveAndEnabled) return;
				Managers.StreakLine.OnDetailShown();
				RefreshClaimButton();
			}
			finally
			{
				if (this != null) { _animating = false; UpdateInteractions(); }
			}
		}

		protected virtual void OnClickInfoButton()
		{
			ShowInfoDialog().Forget();
		}

		public async UniTaskVoid ShowInfoDialog()
		{
			if (_infoShowing || (Busy && !_tutorialWaiting)) return;
			var settings = StreakLineSettings.Instance;
			if (settings != null)
			{
				_infoShowing = true;
				UpdateInteractions();
				try
				{
					_infoDialog = await EventPrefabUI.Show<StreakLineInfoDialog>(transform.parent, "StreakLineInfoDialog");
					await UniTask.WaitUntil(() => _infoDialog == null || !_infoDialog.isActiveAndEnabled,
						cancellationToken: this.GetCancellationTokenOnDestroy());
				}
				finally { if (this != null) { _infoDialog = null; _infoShowing = false; UpdateInteractions(); } }
			}
		}

		protected virtual void OnClickClaimButton()
		{
			ClaimAll().Forget();
		}

		private async UniTaskVoid ClaimAll()
		{
			if (Busy || Managers.StreakLine == null) return;
			_claiming = true;
			UpdateInteractions();
			var rewards = new List<RewardItem>();
			var rewardsByType = new Dictionary<RewardType, RewardItem>();
			try
			{
				foreach (var chest in Managers.StreakLine.GetAllClaimableRewards())
				{
					Managers.StreakLine.ClaimRewards(chest.Level);
					if (!Managers.StreakLine.IsChestClaimed(chest.Level)) continue;
					foreach (var item in chest.Items())
					{
						if (rewardsByType.TryGetValue(item.Type, out var merged)) merged.Count += item.Count;
						else
						{
							merged = new RewardItem(item.Type, item.Count);
							rewardsByType.Add(item.Type, merged);
							rewards.Add(merged);
						}
					}
					_scrollAdapter?.PlayClaimAnimation(chest);
				}
				RefreshClaimButton();
				if (rewards.Count > 0) await ShowClaimedRewards(rewards);
			}
			finally { if (this != null) { _claiming = false; UpdateInteractions(); } }
		}

		private void OnClaimedRewards(StreakLineProgress segment)
		{
			RefreshClaimButton();
			if (segment.Chest != null) ShowClaimedRewards(segment.Chest.Items()).Forget();
		}

		private async UniTask ShowClaimedRewards(List<RewardItem> rewards)
		{
			if (_showingRewards) return;
			var parent = Utils.GetCurrentUiCanvas()?.transform ?? GetComponentInParent<Canvas>()?.rootCanvas.transform;
			if (parent == null) return;
			_showingRewards = true;
			UpdateInteractions();
			SetAlpha(0);
			OnRewardDialogVisibilityChanged(true);
			try
			{
				var receipt = await RewardDialog.ShowWithChest(rewards, parent: parent);
				if (receipt != null)
				{
					var receiptCanvas = receipt.GetComponent<Canvas>();
					var eventCanvas = GetComponentInParent<Canvas>();
					if (receiptCanvas != null && eventCanvas != null) receiptCanvas.sortingOrder = Mathf.Max(receiptCanvas.sortingOrder, eventCanvas.sortingOrder + 1);
				}
				bool completed = Managers.StreakLine.IsClaimedAllRewards();
				if (receipt != null)
					await UniTask.WaitUntil(() => receipt == null || !receipt.isActiveAndEnabled, cancellationToken: this.GetCancellationTokenOnDestroy());
				if (this == null || !completed) return;
				await StreakLineCompleteContent.ShowWithDialog(parent);
				if (this != null) Dismiss();
			}
			finally
			{
				if (this != null && isActiveAndEnabled)
				{
					_showingRewards = false;
					SetAlpha(1);
					OnRewardDialogVisibilityChanged(false);
					UpdateInteractions();
				}
			}
		}

		protected virtual void OnRewardDialogVisibilityChanged(bool visible) { }

		private void RefreshClaimButton()
		{
			if (Managers.StreakLine.CanShowClaimButton()) ShowClaimButton(); else HideClaimButton();
		}

		public void CreateFireworks()
		{
			if (_fireworks != null)
			{
				_fireworks.Play();
			}
		}

		public void ShowClaimButton()
		{
			if (_claimBtn != null && !_claimShown)
			{
				_claimShown = true;
				_claimFade?.Kill();
				_claimBtn.gameObject.SetActive(true);
				_claimGroup.alpha = 0;
				_claimFade = _claimGroup.DOFade(1, .3f).SetLink(gameObject);
			}
		}

		public void HideClaimButton()
		{
			if (_claimBtn != null)
			{
				if (!_claimShown) { _claimBtn.gameObject.SetActive(false); return; }
				_claimShown = false;
				_claimFade?.Kill();
				_claimFade = _claimGroup.DOFade(0, .3f).SetLink(gameObject).OnComplete(() => _claimBtn.gameObject.SetActive(false));
			}
		}

		protected override void OnClickCloseButton()
		{
			if (!Busy) Dismiss();
		}

		protected virtual void OnDestroy()
		{
			if (_scrollAdapter != null) _scrollAdapter.Claimed -= OnClaimedRewards;
			_claimFade?.Kill();
			if (_infoDialog != null) _infoDialog.Dismiss();
			transform.DOKill();
		}
	}
}
