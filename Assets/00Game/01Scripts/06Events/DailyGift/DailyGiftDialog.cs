using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class DailyGiftDialog : Snui_Panel
	{
		[SerializeField] private SkeletonGraphic _spineFlag;
		[SerializeField] private Button _closeBtn;
		[SerializeField] private TMP_Text _dayCountText;
		[SerializeField] private ProgressBar _progressBar;
		[SerializeField] private List<DailyGiftCardCell> _cardCells = new List<DailyGiftCardCell>();
		[SerializeField] private List<DailyGiftBox> _rewardBoxes = new List<DailyGiftBox>();
		[SerializeField] private Transform _safeArea;

		private List<List<RewardItem>> _weekdayRewards;
		private List<List<RewardItem>> _monthRewards;
		private int _weekdayIndex;
		private int _monthSignDays;
		private CancellationToken _cancellationTokenOnDestroy;

		public static async UniTask<DailyGiftDialog> Show(Transform parent = null)
		{
			if (PopupAddresses.Contains("DailyGiftDialog"))
				return await EventPrefabUI.Show<DailyGiftDialog>(parent, "DailyGiftDialog");
			var settings = DailyGiftSettings.Instance;
			if (settings == null || settings.Dialog == null || !settings.Dialog.RuntimeKeyIsValid()) return null;
			var instance = await AddressableUtils.AsyncLoadAsGameObject(settings.Dialog, parent).Task;
			return instance == null ? null : instance.GetComponent<DailyGiftDialog>();
		}

		private void Awake()
		{
			_cancellationTokenOnDestroy = this.GetCancellationTokenOnDestroy();

			if (_safeArea == null)
			{
				_safeArea = HierarchyReferenceCache.Child(transform, "SafeArea");
			}

			Transform root = _safeArea != null ? _safeArea : transform;

			if (_spineFlag == null)
			{
				_spineFlag = HierarchyReferenceCache.Component<SkeletonGraphic>(HierarchyReferenceCache.Path(root, "Top", "SpineFlag"));
			}

			if (_closeBtn == null)
			{
				_closeBtn = HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Path(root, "Top", "CloseBtn"));
			}

			if (_dayCountText == null)
			{
				_dayCountText = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Path(root, "Content", "TopReward", "DayBg", "DayCount"));
			}

			if (_progressBar == null)
			{
				_progressBar = HierarchyReferenceCache.Component<ProgressBar>(HierarchyReferenceCache.Path(root, "Content", "TopReward", "ProgressBg", "ProgressBar"));
			}

			Transform cellsParent = HierarchyReferenceCache.Path(root, "Content", "SignBg", "CardCells");
			if (cellsParent != null && _cardCells.Count == 0)
			{
				for (int i = 1; i <= 7; i++)
				{
					var cell = HierarchyReferenceCache.ChildComponent<DailyGiftCardCell>(cellsParent, string.Format("CardCell{0}", i));
					if (cell != null)
					{
						_cardCells.Add(cell);
					}
				}
			}

			Transform boxParent = HierarchyReferenceCache.Path(root, "Content", "TopReward", "RewardBox");
			if (boxParent != null && _rewardBoxes.Count == 0)
			{
				for (int i = 1; i <= 5; i++)
				{
					var box = HierarchyReferenceCache.ChildComponent<DailyGiftBox>(boxParent, string.Format("DailyGiftBox{0}", i));
					if (box != null)
					{
						_rewardBoxes.Add(box);
					}
				}
			}
		}

		protected override void Start()
		{
			base.Start();

			int signDay = Managers.DailyGiftManager != null ? Managers.DailyGiftManager.GetSignDay() : 0;
			AnalyticsManager.LogEvent("daily_gift_show", new Dictionary<string, object>
			{
				{ "days", signDay + 1 }
			});

			if (Managers.DailyGiftManager != null)
			{
				var rewards = Managers.DailyGiftManager.GetRewards();
				_weekdayRewards = rewards.WeekdayRewards;
				_monthRewards = rewards.MonthRewards;
			}

			if (_closeBtn != null)
			{
				_closeBtn.onClick.AddListener(OnClickCloseBtn);
			}

			if (_dayCountText != null)
			{
				_dayCountText.SetText(signDay.ToString());
			}

			_weekdayIndex = signDay % 7;
			_monthSignDays = signDay % 30;

			InitSignWeekday();
			InitSignMonthProgress();
			PlayEnterAnim().Forget();

			if (AudioManager.Current != null)
			{
				AudioManager.Current.PlayOneShot("click.mp3", gameObject);
			}
		}

		public void InitSignWeekday()
		{
			for (int i = 0; i < _cardCells.Count; i++)
			{
				var rewards = (_weekdayRewards != null && i < _weekdayRewards.Count) ? _weekdayRewards[i] : null;
				_cardCells[i].Init(rewards, _weekdayIndex, i);

				if (i == _weekdayIndex)
				{
					var btn = _cardCells[i].GetComponent<Button>();
					if (btn != null)
					{
						btn.onClick.AddListener(OnClickClaimBtn);
					}
				}
			}
		}

		public void InitSignMonthProgress()
		{
			if (_progressBar != null)
			{
				_progressBar.SetPercent(GetSignMonthProgress(_monthSignDays, false));
			}

			int signDay = Managers.DailyGiftManager != null ? Managers.DailyGiftManager.GetSignDay() : 0;
			var sections = Managers.DailyGiftManager != null ? Managers.DailyGiftManager.SignMonthSections : null;

			for (int i = 0; i < _rewardBoxes.Count; i++)
			{
				var rewards = (_monthRewards != null && i < _monthRewards.Count) ? _monthRewards[i] : null;
				int targetDays = (sections != null && (i + 1) < sections.Count) ? sections[i + 1] : (i + 1) * 6;
				_rewardBoxes[i].Init(rewards, signDay, targetDays, i);
			}
		}

		public float GetSignMonthProgress(int monthSignDays, bool isNext)
		{
			var sections = Managers.DailyGiftManager != null ? Managers.DailyGiftManager.SignMonthSections : null;
			if (sections == null || sections.Count <= 1) return 0f;

			int sectionCount = sections.Count - 1;
			float sectionFraction = 1f / sectionCount;
			float progress = 0f;
			int reachedCount = 0;

			for (int i = 1; i < sections.Count; i++)
			{
				int target = sections[i];
				if (target <= monthSignDays)
				{
					progress += sectionFraction;
					reachedCount++;
				}
				else
				{
					int prev = sections[i - 1];
					int diff = target - prev;
					if (diff > 0 && monthSignDays > prev)
					{
						float segmentT = (float)(monthSignDays - prev) / diff;
						progress += segmentT * sectionFraction;
					}
					break;
				}
			}

			if (isNext && reachedCount + 1 < sections.Count)
			{
				progress += sectionFraction * 0.5f;
			}

			return Mathf.Clamp01(progress);
		}

		public void OnClickClaimBtn()
		{
			TryClaimReward().Forget();
		}

		public async UniTask<bool> TryClaimReward()
		{
			if (Managers.DailyGiftManager == null || !Managers.DailyGiftManager.CanClaimToday())
			{
				return false;
			}

			int signDay = Managers.DailyGiftManager.GetSignDay();
			AnalyticsManager.LogEvent("daily_gift_claim", new Dictionary<string, object>
			{
				{ "days", signDay + 1 }
			});

			if (!Managers.DailyGiftManager.TryClaimToday()) return false;

			if (_closeBtn != null)
			{
				_closeBtn.enabled = false;
			}

			_monthSignDays++;

			if (_weekdayIndex >= 0 && _weekdayIndex < _cardCells.Count)
			{
				_cardCells[_weekdayIndex].ClaimReward().Forget();
			}

			await UniTask.WaitForSeconds(1.5f, cancellationToken: _cancellationTokenOnDestroy);
			await UpdateMonthProgress();

			bool claimedBox = await ClaimBoxReward();
			return claimedBox;
		}

		public async UniTask UpdateMonthProgress()
		{
			float progress = GetSignMonthProgress(_monthSignDays, true);
			if (_progressBar != null)
			{
				_progressBar.IncreaseTo(progress, 0.3f);
			}

			await UniTask.WaitForSeconds(0.1f, cancellationToken: _cancellationTokenOnDestroy);

			if (_dayCountText != null && Managers.DailyGiftManager != null)
			{
				_dayCountText.SetText(Managers.DailyGiftManager.GetSignDay().ToString());
			}
		}

		public async UniTask<bool> ClaimBoxReward()
		{
			var box = _rewardBoxes.FirstOrDefault(b => b != null && b.TargetDays == _monthSignDays);
			if (box != null)
			{
				HiddenDialog().Forget();
				await box.PlayClaimAnim();
				Snui_Panel.DestroyPanel(gameObject);
				return true;
			}

			await UniTask.WaitForSeconds(0.5f, cancellationToken: _cancellationTokenOnDestroy);
			Snui_Panel.DestroyPanel(gameObject);

			if (_closeBtn != null)
			{
				_closeBtn.enabled = true;
			}
			return false;
		}

		public async UniTask HiddenDialog()
		{
			await UniTask.WaitForSeconds(0.6f, cancellationToken: _cancellationTokenOnDestroy);
			gameObject.SetActive(false);
		}

		public void OnClickCloseBtn()
		{
			if (_closeBtn != null)
			{
				_closeBtn.enabled = false;
			}

			OnCloseDialog().Forget();
		}

		public async UniTaskVoid OnCloseDialog()
		{
			bool claimed = await TryClaimReward();
			if (!claimed)
			{
				Snui_Panel.DestroyPanel(gameObject);
			}
		}

		public async UniTaskVoid PlayEnterAnim()
		{
			PlayPopupEnterAnimation();

			if (_spineFlag != null && _spineFlag.AnimationState != null)
			{
				var track = _spineFlag.AnimationState.SetAnimation(0, "open", false);
				if (track != null)
				{
					await track.AsyncWaitForCompletion(_cancellationTokenOnDestroy);
				}
				_spineFlag.AnimationState.SetAnimation(0, "idle", true);
			}
		}

		public Sequence PlayPopupEnterAnimation()
		{
			Transform target = _safeArea != null ? _safeArea : transform;
			target.localScale = new Vector3(0.98f, 0.98f, 1f);

			Sequence seq = DOTween.Sequence();
			seq.Append(target.DOScale(1.01f, 0.1f));
			seq.Append(target.DOScale(1f, 0.13f));
			seq.SetLink(gameObject);
			return seq;
		}
	}
}
