using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class DailyRewardsDialog : BasePopup
	{
		[SerializeField] private Transform _content;
		[SerializeField] private Button _closeButton;
		[SerializeField] private Button _claimBtn;
		[SerializeField] private GameObject _nextTimeTip;
		[SerializeField] private DailyRewardsProgress _progress;
		private Coroutine _coroutine;
		private bool _isShown;
		private bool _hasStarted;
		private List<DailyRewardCell> _dailyRewardCells = new List<DailyRewardCell>();

		protected override void Awake()
		{
			if (_content == null)
			{
				_content = HierarchyReferenceCache.Path(transform, "SafeArea", "DailyDialogContent");
				if (_content == null) _content = transform;
			}

			if (_closeButton == null)
			{
				_closeButton = HierarchyReferenceCache.DescendantComponent<Button>(_content, "CloseButton");
				if (_closeButton == null) _closeButton = HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Path(_content, "Ribbon", "CloseButton"));
			}

			if (_claimBtn == null)
			{
				_claimBtn = HierarchyReferenceCache.DescendantComponent<Button>(_content, "ClaimBtn");
			}

			if (_nextTimeTip == null)
			{
				var tipTrans = HierarchyReferenceCache.Descendant(_content, "NextTimeTip");
				if (tipTrans != null) _nextTimeTip = tipTrans.gameObject;
			}

			_closeBtn = _closeButton;
			base.Awake();

			if (_progress == null)
			{
				_progress = HierarchyReferenceCache.DescendantComponent<DailyRewardsProgress>(_content, "Progress");
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();

			if (_claimBtn != null)
			{
				_claimBtn.onClick.AddListener(OnClickClaimBtn);
			}

			DailyCheckInManager.Instance.OnClaimReward.AddListener(Claim);

			if (_hasStarted) RefreshPresentation();
		}

		protected override void Start()
		{
			base.Start();
			_hasStarted = true;
			RefreshPresentation();
		}

		private void RefreshPresentation()
		{
			Init();

			_isShown = true;
			DailyCheckInManager.Instance.OnDailyCheckInPanelShow();
		}

		public void Init()
		{
			StopCountDown();
			bool canClaim = DailyCheckInManager.Instance.CheckAnyRewardCanClaim();
			if (!canClaim)
			{
				ShowNextTimeTip();
			}
			else
			{
				if (_nextTimeTip != null) _nextTimeTip.SetActive(false);
				if (_claimBtn != null)
				{
					_claimBtn.gameObject.SetActive(true);
					_claimBtn.interactable = true;
					var breath = _claimBtn.GetComponent<BreathAnim>();
					if (breath == null) breath = _claimBtn.gameObject.AddComponent<BreathAnim>();
					if (!breath.IsPlaying()) breath.Play(0f, 3f);
				}
			}

			if (_progress != null)
			{
				_progress.InitBoxes();
			}

			_dailyRewardCells.Clear();
			for (int i = 0; i < DailyCheckInManager.LOGIN_DAY_COUNT; i++)
			{
				Transform cellTrans = HierarchyReferenceCache.Descendant(_content, $"DailyRewardCell{i}");
				if (cellTrans == null) continue;

				DailyRewardCell cell = cellTrans.GetComponent<DailyRewardCell>();
				if (cell != null)
				{
					var rewards = DailyCheckInManager.Instance.DataProvider?.GetDayRewardContents(i);
					cell.Init(i, rewards);
					_dailyRewardCells.Add(cell);
				}
			}
			if (isActiveAndEnabled) _coroutine = StartCoroutine(IEStartCountDown());
		}

		private void ShowNextTimeTip()
		{
			if (_claimBtn != null)
			{
				var breath = _claimBtn.GetComponent<BreathAnim>();
				if (breath != null) breath.Stop();
				_claimBtn.gameObject.SetActive(false);
			}

			if (_nextTimeTip != null)
			{
				_nextTimeTip.SetActive(true);
			}

		}

		private IEnumerator IEStartCountDown()
		{
			TMP_Text timeText = null;
			if (_nextTimeTip != null)
			{
				timeText = _nextTimeTip.GetComponentInChildren<TMP_Text>(true);
			}

			int loginDays = DailyCheckInManager.Instance.LoginDays;
			bool hadReward = DailyCheckInManager.Instance.HasCollectReward();
			WaitForSecondsRealtime delay = new WaitForSecondsRealtime(1f);
			while (true)
			{
				DailyCheckInManager.Instance.JudgeCrossDay();
				long remainingTime = TimeUtils.GetSecondsToNextDay();
				if (DailyCheckInManager.Instance.LoginDays != loginDays || DailyCheckInManager.Instance.HasCollectReward() != hadReward)
				{
					_coroutine = null;
					Init();
					yield break;
				}

				if (timeText != null)
				{
					timeText.text = TimeFormatter.FormatSeconds(remainingTime);
				}

				yield return delay;
			}
		}

		private void OnClickClaimBtn()
		{
			DailyCheckInManager.Instance.ShowCollectRewards();
		}

		private void Claim()
		{
			var dayStates = DailyCheckInManager.Instance.EachDayClaimState;
			foreach (var cell in _dailyRewardCells)
			{
				if (cell != null && dayStates != null && cell.Index >= 0 && cell.Index < dayStates.Count)
					cell.SetCellState(dayStates[cell.Index]);
			}

			if (_progress != null)
			{
				_progress.PlayCollectedAnim();
			}

			bool stillCanClaim = DailyCheckInManager.Instance.HasCollectReward();
			if (!stillCanClaim)
			{
				ShowNextTimeTip();
			}
		}

		private void StopCountDown()
		{
			if (_coroutine != null) StopCoroutine(_coroutine);
			_coroutine = null;
		}

		protected override void OnDisable()
		{
			StopCountDown();
			if (_claimBtn != null) _claimBtn.onClick.RemoveListener(OnClickClaimBtn);
			DailyCheckInManager.Instance.OnClaimReward.RemoveListener(Claim);
			if (_isShown)
			{
				_isShown = false;
				DailyCheckInManager.Instance.CloseDailyCheckInPanel();
			}
			base.OnDisable();
		}
		public void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus && isActiveAndEnabled)
			{
				DailyCheckInManager.Instance.JudgeCrossDay();
				Init();
			}
		}
	}
}
