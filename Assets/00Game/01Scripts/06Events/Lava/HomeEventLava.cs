using Cysharp.Threading.Tasks;
using Spine.Unity;
using Snui.Lava;
using UnityEngine;

namespace Snui
{
	public class HomeEventLava : HomeEventBase
	{
		[SerializeField] private SkeletonGraphic _iconSpine;
		[SerializeField] private ProgressBar _progressBar;
		private bool _dialogOpen;

		protected override void Awake()
		{
			base.Awake();
			if (_iconSpine == null)
			{
				_iconSpine = HierarchyReferenceCache.ChildComponent<SkeletonGraphic>(transform, "Spine");
			}
			if (_progressBar == null)
			{
				_progressBar = HierarchyReferenceCache.ChildComponent<ProgressBar>(transform, "Bg");
			}
		}

		protected virtual void Start()
		{
			if (_textBar != null) _textBar.AddOnTimerStopListener(OnTimerStopped);
			UpdateUI();
			PlayIdleAnimation();
		}

		public void UpdateUI()
		{
			if (Managers.Lava == null) return;
			SetUnlockLevel(Managers.Lava.GetUnlockLevel());

			if (!Managers.Lava.IsUnlocked())
			{
				if (_progressBar != null)
				{
					_progressBar.SetProgress(0f);
				}
				return;
			}

			if (_textBar != null)
			{
				_textBar.gameObject.SetActive(true);
				if (Managers.Lava.IsWaitingStart())
				{
					_textBar.SetStyle(HomeIconTextBar.Style.Start);
					_textBar.SetText("START");
				}
				else if (Managers.Lava.ShouldClaimRewards())
				{
					_textBar.SetStyle(HomeIconTextBar.Style.Claim);
					_textBar.SetText("CLAIM");
				}
				else
				{
					_textBar.SetStyle(HomeIconTextBar.Style.Timer);
					_textBar.CountdownUntil(Managers.Lava.IsInCd() ? Managers.Lava.GetNextRetryTime() : Managers.Lava.GetEndTime());
				}
			}

			int record = Managers.Lava.GetRecord();
			int goal = Managers.Lava.GetLevelGoal();
			float progress = goal > 0 ? Mathf.Clamp01((float)record / goal) : 0f;

			if (_progressBar != null)
			{
				_progressBar.SetProgress(progress);
			}
		}

		public override bool PlayIdleAnimation()
		{
			if (_iconSpine != null)
			{
				_iconSpine.Initialize(false);
				_iconSpine.AnimationState?.SetAnimation(0, "idle", true);
				return true;
			}
			return false;
		}

		protected override void OnClick()
		{
			OnClickAsync().Forget();
		}

		public async UniTask OnClickAsync()
		{
			if (_dialogOpen || Managers.Lava == null || !Managers.Lava.IsUnlocked()) return;
			_dialogOpen = true;
			try
			{
				Managers.Lava.CheckEventState();
				if (Managers.Lava.IsWaitingStart()) await ShowOpenDialog();
				else if (Managers.Lava.IsInCd()) await ShowCDDialog();
				else if (Managers.Lava.IsOngoing()) await ShowDetailDialog();
			}
			catch (System.OperationCanceledException) { }
			finally
			{
				if (this != null)
				{
					_dialogOpen = false;
					RefreshAfterDialog();
				}
			}
		}

		public override async UniTask ShowPopupDialog()
		{
			if (_dialogOpen || Managers.Lava == null || !Managers.Lava.IsUnlocked()) return;
			Managers.Lava.CheckEventState();
			if (Managers.Lava.ShouldShowDetailDialog()) await OnClickAsync();
			else if (Managers.Lava.ShouldShowEventJoinPrompt())
			{
				if (!Managers.Lava.HasShownTutorial())
				{
					if (!HomeEventController.CanShowForceGuide()) return;
					HomeEventController.ForceGuideCount++;
				}
				Managers.Lava.OnShowPrompt();
				await OnClickAsync();
			}
		}

		public override bool HasNonSkippableAnimation() => _dialogOpen;
		public override void CommitProgressBeforeEnterGame() => Managers.Lava?.SynchronizeTempPlayer();

		private void RefreshAfterDialog()
		{
			if (!Managers.Lava.ShouldShowIcon()) { gameObject.SetActive(false); Destroy(gameObject); }
			else UpdateUI();
		}

		public async UniTask ShowOpenDialog()
		{
			var dialog = await LavaOpenDialogContent.Show(transform.parent);
			int result = dialog.Result;
			dialog.Dismissed += value => result = value;
			await UniTask.WaitUntil(() => dialog == null || result != DialogContent.RESULT_UNKNOWN,
				cancellationToken: this.GetCancellationTokenOnDestroy());
			if (result == 1 && Managers.Lava.IsOngoing())
			{
				// Native recruitment runs once after Join, before the progress board.
				Managers.Lava.OnStartAnimShown();
				await LavaStartDialogContent.ShowWithDialog(transform.parent)
					.AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
				await ShowDetailDialog();
			}
		}

		public async UniTask ShowDetailDialog()
		{
			var dialog = await LavaDetailDialog.Show(transform.parent);
			await dialog.WaitForCloseAsync();
		}

		public async UniTask ShowCDDialog()
		{
			var dialog = await LavaCdDialogContent.Show(transform.parent);
			await UniTask.WaitUntil(() => dialog == null || dialog.Result != DialogContent.RESULT_UNKNOWN,
				cancellationToken: this.GetCancellationTokenOnDestroy());
		}

		public void OnTimerStopped()
		{
			Managers.Lava?.CheckEventState();
			UpdateUI();
		}

		protected override void OnDestroy()
		{
			if (_textBar != null) _textBar.RemoveOnTimerStopListener(OnTimerStopped);
			base.OnDestroy();
		}
	}
}
