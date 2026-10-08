using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class DailyRewardsPanel : MonoBehaviour
	{
		[SerializeField] private Transform _bg;
		[SerializeField] private Button _closeButton;
		[SerializeField] private Button _claimBtn;
		[SerializeField] private DailyRewardsProgress _progress;
		private Color _bgColor = Color.white;
		private bool _isShown;
		private bool _hasStarted;
		private int _loginDays;
		private float _nextRefreshTime;
		private List<DailyRewardCell> _dailyRewardCells = new List<DailyRewardCell>();

		public void Awake()
		{
			if (_bg == null)
			{
				_bg = HierarchyReferenceCache.Child(transform, "Bg");
			}

			if (_closeButton == null)
			{
				_closeButton = HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseButton");
			}

			if (_claimBtn == null)
			{
				_claimBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "ClaimBtn");
			}

			if (_progress == null)
			{
				_progress = HierarchyReferenceCache.DescendantComponent<DailyRewardsProgress>(transform, "Progress");
			}
		}

		private void OnEnable()
		{
			if (_closeButton != null)
			{
				_closeButton.onClick.AddListener(OnClickCloseBtn);
			}

			if (_claimBtn != null)
			{
				_claimBtn.onClick.AddListener(OnClickClaimBtn);
			}

			DailyCheckInManager.Instance.OnClaimReward.AddListener(Claim);

			if (_hasStarted) Show();
		}

		private void Start()
		{
			_hasStarted = true;
			Show();
		}

		private void Show()
		{
			Init(_bgColor);
			_isShown = true;
			DailyCheckInManager.Instance.OnDailyCheckInPanelShow();
		}

		public void Init(Color bgColor)
		{
			_bgColor = bgColor;
			DailyCheckInManager.Instance.JudgeCrossDay();
			_loginDays = DailyCheckInManager.Instance.LoginDays;
			if (_bg != null)
			{
				var img = _bg.GetComponent<Image>();
				if (img != null) img.color = bgColor;
			}

			if (_progress != null)
			{
				_progress.InitBoxes();
			}

			_dailyRewardCells.Clear();
			for (int i = 0; i < DailyCheckInManager.LOGIN_DAY_COUNT; i++)
			{
				Transform cellTrans = HierarchyReferenceCache.Descendant(transform, $"DailyRewardCell{i}");
				if (cellTrans == null) continue;

				DailyRewardCell cell = cellTrans.GetComponent<DailyRewardCell>();
				if (cell != null)
				{
					var rewards = DailyCheckInManager.Instance.DataProvider?.GetDayRewardContents(i);
					cell.Init(i, rewards);
					_dailyRewardCells.Add(cell);
				}
			}

			bool canClaim = DailyCheckInManager.Instance.CheckAnyRewardCanClaim();
			if (_claimBtn != null)
			{
				_claimBtn.interactable = canClaim;
			}
		}

		private void OnClickCloseBtn()
		{
			gameObject.SetActive(false);
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

			bool canClaim = DailyCheckInManager.Instance.CheckAnyRewardCanClaim();
			if (_claimBtn != null)
			{
				_claimBtn.interactable = canClaim;
			}
		}

		private void OnDisable()
		{
			if (_closeButton != null) _closeButton.onClick.RemoveListener(OnClickCloseBtn);
			if (_claimBtn != null) _claimBtn.onClick.RemoveListener(OnClickClaimBtn);
			DailyCheckInManager.Instance.OnClaimReward.RemoveListener(Claim);
			if (_isShown)
			{
				_isShown = false;
				DailyCheckInManager.Instance.CloseDailyCheckInPanel();
			}
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus && isActiveAndEnabled) Init(_bgColor);
		}

		private void Update()
		{
			if (Time.unscaledTime < _nextRefreshTime) return;
			_nextRefreshTime = Time.unscaledTime + 1f;
			DailyCheckInManager.Instance.JudgeCrossDay();
			if (_loginDays != DailyCheckInManager.Instance.LoginDays) Init(_bgColor);
		}
	}
}
