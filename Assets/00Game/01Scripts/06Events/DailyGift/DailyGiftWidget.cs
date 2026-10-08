using System.Threading;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;

namespace Snui
{
	public class DailyGiftWidget : HomeEventBase
	{
		private SkeletonGraphic _spineGraphic;
		private CancellationToken _cancellationTokenOnDestroy;

		protected override void Awake()
		{
			base.Awake();
			_cancellationTokenOnDestroy = this.GetCancellationTokenOnDestroy();

			if (_spineGraphic == null)
			{
				_spineGraphic = HierarchyReferenceCache.ChildComponent<SkeletonGraphic>(transform, "Spine");
			}
		}

		protected virtual void Start()
		{
			SetBgColor(BgColor.Green);
			RefreshIcon();

			if (_textBar != null)
			{
				_textBar.AddOnTimerStopListener(RefreshIcon);
			}

			if (Managers.DailyGiftManager != null)
			{
				Managers.DailyGiftManager.OnSignClaimTimeChanged.AddListener(RefreshIcon);
			}
		}

		protected override void OnClick()
		{
			base.OnClick();

			var canvas = SnuiManager.GetCurrentUiCanvas();
			DailyGiftDialog.Show(canvas != null ? canvas.transform : null).Forget();
		}

		public void RefreshIcon()
		{
			bool canClaim = Managers.DailyGiftManager != null && Managers.DailyGiftManager.CanClaimToday();

			if (!canClaim)
			{
				if (_textBar != null)
				{
					_textBar.SetStyle(HomeIconTextBar.Style.Timer);
				}

				long expireAt = TimeUtils.GetUnixTimestamp() + TimeUtils.GetSecondsUntilEndOfDay();
				SetExpireAt(expireAt);
			}
			else
			{
				if (_textBar != null)
				{
					_textBar.SetStyle(HomeIconTextBar.Style.Claim);
					_textBar.SetText("COLLECT");
				}
			}

			ShowExclamationMark(canClaim);
		}

		public override bool PlayIdleAnimation()
		{
			if (_spineGraphic != null)
			{
				_spineGraphic.Initialize(true);
				if (_spineGraphic.AnimationState != null)
				{
					_spineGraphic.AnimationState.SetAnimation(0, "idle", false);
				}
			}
			return true;
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			if (_textBar != null)
			{
				_textBar.RemoveOnTimerStopListener(RefreshIcon);
			}

			if (Managers.DailyGiftManager != null)
			{
				Managers.DailyGiftManager.OnSignClaimTimeChanged.RemoveListener(RefreshIcon);
			}
		}
	}
}
