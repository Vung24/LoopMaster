using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Snui.StreakLine
{
	public class HomeEventStreakLine : HomeEventBase
	{
		protected override void Awake()
		{
			base.Awake();
		}

		private void Start()
		{
			UpdateUI();
		}

		public void UpdateUI()
		{
			if (Managers.StreakLine == null) return;

			if (Managers.StreakLine.IsLock)
			{
				SetUnlockLevel(Managers.StreakLine.GetUnlockLevel());
				return;
			}

			if (_lock != null) _lock.gameObject.SetActive(false);

			if (_textBar != null && Managers.StreakLine.IsOngoing)
			{
				_textBar.CountdownUntil(Managers.StreakLine.EndTime);
			}

			if (_notifier != null)
			{
				_notifier.gameObject.SetActive(Managers.StreakLine.ShouldShowExclamation());
			}
		}

		public override bool HasNonSkippableAnimation()
		{
			return Managers.StreakLine != null && Managers.StreakLine.ShouldShowRewardMilestonePopup();
		}

		public override async UniTask PlayAddAnimation()
		{
			if (Managers.StreakLine == null) return;
			transform.localScale = Vector3.one * 1.2f;
			await transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutQuad).SetLink(gameObject).ToUniTask();
			UpdateUI();
		}

		public override async UniTask ShowPopupDialog()
		{
			if (Managers.StreakLine != null && Managers.StreakLine.IsOngoing &&
				(!Managers.StreakLine.HasPlayedTutorial() || Managers.StreakLine.ShouldShowRewardMilestonePopup()))
			{
				var dialog = await StreakLineDialog.Show(transform.root);
				if (dialog != null)
					await UniTask.WaitUntil(() => dialog == null || !dialog.isActiveAndEnabled,
						cancellationToken: this.GetCancellationTokenOnDestroy());
			}
		}

		public async UniTask ShowDialog()
		{
			if (Managers.StreakLine == null || Managers.StreakLine.IsLock) return;
			await StreakLineDialog.Show(transform.root);
		}

		protected override void OnClick()
		{
			base.OnClick();
			ShowDialog().Forget();
		}
	}
}
