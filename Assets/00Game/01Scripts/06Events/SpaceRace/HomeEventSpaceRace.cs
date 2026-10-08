using System;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using TMPro;
using UnityEngine;

namespace Snui.SpaceRace
{
	public class HomeEventSpaceRace : HomeEventBase
	{
		[SerializeField] private SkeletonGraphic _iconSpine;
		[SerializeField] private SkeletonGraphic _addSpine;
		[SerializeField] private TMP_Text _scoreValueText;

		protected override void Awake()
		{
			base.Awake();

			if (_iconSpine == null)
			{
				_iconSpine = HierarchyReferenceCache.ChildComponent<SkeletonGraphic>(transform, "Spine");
			}
			if (_addSpine == null)
			{
				_addSpine = HierarchyReferenceCache.ChildComponent<SkeletonGraphic>(transform, "AddSpine");
			}
			if (_scoreValueText == null)
			{
				_scoreValueText = HierarchyReferenceCache.PathComponent<TMP_Text>(transform, "Spine", "Score", "Value");
			}
		}

		private void Start()
		{
			SetBgColor(BgColor.Blue2);

			if (Managers.SpaceRace != null)
			{
				Managers.SpaceRace.OnWinEvent.AddListener(OnRaceStart);
			}

			UpdateUI();

			if (Managers.SpaceRace != null && Managers.SpaceRace.IsOngoing && !Managers.SpaceRace.IsWin && !Managers.SpaceRace.IsFail)
			{
				RefreshRank().Forget();
				if (_textBar != null)
				{
					_textBar.AddOnTimerStopListener(() =>
					{
						if (Managers.SpaceRace != null)
						{
							Managers.SpaceRace.RefreshRobots();
							UpdateUI();
						}
					});
				}
			}
		}

		public void UpdateUI()
		{
			if (Managers.SpaceRace == null) return;

			bool isWaiting = Managers.SpaceRace.IsWaitingStart;
			ShowExclamationMark(isWaiting);

			if (Managers.SpaceRace.IsLocked)
			{
				SetUnlockLevel(Managers.SpaceRace.GetUnlockLevel());
				return;
			}

			if (Managers.SpaceRace.IsOngoing)
			{
				if (_scoreValueText != null)
				{
					_scoreValueText.gameObject.SetActive(true);
					_scoreValueText.text = Managers.SpaceRace.Rank.ToString();
				}

				if (_textBar != null)
				{
					_textBar.CountdownUntil(Managers.SpaceRace.StageEndTime);
				}

				PlayIdleAnimation();
				return;
			}

			if (Managers.SpaceRace.IsClosed)
			{
				Destroy(gameObject);
				return;
			}

			if (isWaiting)
			{
				if (_textBar != null)
				{
					_textBar.CountdownUntil(Managers.SpaceRace.SessionEndTime);
				}
				PlayIdleAnimation();
			}
		}

		public override bool PlayIdleAnimation()
		{
			if (_iconSpine != null && _iconSpine.AnimationState != null)
			{
				_iconSpine.AnimationState.SetAnimation(0, "idle", true);
				return true;
			}
			return false;
		}

		public override async UniTask PlayAddAnimation()
		{
			if (_addSpine != null)
			{
				_addSpine.gameObject.SetActive(true);
				if (_addSpine.AnimationState != null)
				{
					_addSpine.AnimationState.SetAnimation(0, "add", false);
				}
			}
			await PlayAddAnimationEffect();
		}

		public async UniTask PlayAddAnimationEffect()
		{
			await UniTask.Delay(500);
			if (Managers.SpaceRace != null)
			{
				Managers.SpaceRace.ClearTempScore();
			}
			UpdateUI();
		}

		public override async UniTask ShowPopupDialog()
		{
			if (Managers.SpaceRace == null) return;

			if (Managers.SpaceRace.IsWaitingStart && Managers.SpaceRace.ShouldPromptStart())
			{
				Managers.SpaceRace.OnShowPrompt();
				await ShowStartDialog();
			}
			else if (Managers.SpaceRace.IsOngoing && (Managers.SpaceRace.IsWin || Managers.SpaceRace.IsFail))
			{
				await ShowDetailDialog();
			}
		}

		protected override void OnClick()
		{
			if (Managers.SpaceRace == null || Managers.SpaceRace.IsLocked) return;
			if (Managers.SpaceRace.IsWaitingStart) ShowStartDialog().Forget();
			else ShowDetailDialog().Forget();
		}

		public async UniTask ShowStartDialog()
		{
			var canvas = Utils.GetCurrentUiCanvas();
			await SpaceRaceOpenDialogContent.Show(canvas != null ? canvas.transform : GetComponentInParent<Canvas>()?.transform);
		}

		public async UniTask ShowDetailDialog()
		{
			await SpaceRaceDetailDailog.Show();
		}

		public void OnRaceStart()
		{
			UpdateUI();
		}

		public async UniTaskVoid RefreshRank()
		{
			await UniTask.Yield();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if (Managers.SpaceRace != null)
			{
				Managers.SpaceRace.OnWinEvent.RemoveListener(OnRaceStart);
			}
		}
	}
}
