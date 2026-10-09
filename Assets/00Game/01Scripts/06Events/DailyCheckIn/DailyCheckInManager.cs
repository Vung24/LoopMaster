using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace Snui
{
	public class DailyCheckInManager
	{
		public static readonly int LOGIN_DAY_COUNT = 7;
		public static readonly int BOX_COUNT = 5;
		public static bool IsOpen = false;

		private static DailyCheckInManager _instance;
		public IDailyDelegate Delegate;
		public IDailyDataProvider DataProvider;

		public UnityEvent OnDailyCheckInPanelOpen = new UnityEvent();
		public UnityEvent OnDailyCheckInPanelClose = new UnityEvent();
		public UnityEvent OnDailyEventDone = new UnityEvent();
		public UnityEvent OnClaimReward = new UnityEvent();

		private bool _isTodayDialogShow;
		private bool _isDialogShow;
		private bool _isBoxCanCollect;
		private bool _isCollecting;
		private DailyPrefData _data;

		public static DailyCheckInManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new DailyCheckInManager();
					_instance.ReadData();
					_instance.JudgeCrossDay();
				}
				return _instance;
			}
		}

		public int LoginDays => _data.LoginDays;
		public List<RewardState> EachDayClaimState => _data.EachDayClaimState;
		public List<RewardState> EachBoxClaimState => _data.EachBoxClaimState;
		public int Stage => _data.Stage;
		public bool IsBoxCanCollect => _isBoxCanCollect;

		public DailyCheckInManager()
		{
			DataProvider = new DefaultDailyDataProvider();
			Delegate = new DefaultDailyDelegate(this);
		}

		private void InitData()
		{
			ReadData();
			if (!IsOpen) return;

			if (TimeUtils.IsToday(_data.LastRecordDay))
			{
				return;
			}

			_data.IsTodayDialogShow = 0;
			_isTodayDialogShow = false;
			SaveData();

			_data.LastRecordDay = (int)TimeUtils.GetUnixTimestamp();
			if (_data.EachDayClaimState != null && _data.EachDayClaimState.Count > 0 &&
			    _data.EachDayClaimState[LOGIN_DAY_COUNT - 1] != RewardState.CanClaim)
			{
				_data.LoginDays++;
				DailyRewardRecordAdd();
				BoxRewardRecordAdd();
			}
			SaveData();
		}

		public void ClearData()
		{
			if (_isCollecting) return;
			PlayerPrefs.DeleteKey(DailyPrefKeys.DailyCheckInData);
			PlayerPrefs.Save();
			ResetDailyData();
		}

		private void SaveData()
		{
			_data.IsTodayDialogShow = _isTodayDialogShow ? 1 : 0;
			string json = JsonConvert.SerializeObject(_data);
			PlayerPrefs.SetString(DailyPrefKeys.DailyCheckInData, json);
			PlayerPrefs.Save();
		}

		public bool IsDialogNeedAutoShow()
		{
			return IsOpen && !_isDialogShow && !_isTodayDialogShow;
		}

		public bool CheckAnyRewardCanClaim()
		{
			if (!IsOpen) return false;
			JudgeCrossDay();
			return HasCollectReward();
		}

		public bool HasCollectReward()
		{
			bool hasDay = _data.EachDayClaimState != null && _data.EachDayClaimState.Any(s => s == RewardState.CanClaim);
			bool hasBox = _data.EachBoxClaimState != null && _data.EachBoxClaimState.Any(s => s == RewardState.CanClaim);
			return hasDay || hasBox;
		}

		public void OnDailyCheckInPanelShow()
		{
			_isTodayDialogShow = true;
			_isDialogShow = true;
			SaveData();
			OnDailyCheckInPanelOpen?.Invoke();
			Delegate?.OnDailyCheckInPanelShow();
		}

		public void CloseDailyCheckInPanel()
		{
			_isDialogShow = false;
			SaveData();
			OnDailyCheckInPanelClose?.Invoke();
			Delegate?.OnDailyCheckInPanelClose();
		}

		public void ShowCollectRewards()
		{
			if (!IsOpen || _isCollecting || Delegate == null) return;
			JudgeCrossDay();
			_isCollecting = true;
			InnerShowCollectRewards().Forget();
		}

		private async UniTask InnerShowCollectRewards()
		{
			try
			{
				await ShowDayCollectRewards();
				await ShowBoxCollectRewards();
			}
			catch (Exception error)
			{
				ReadData();
				Debug.LogWarning("DailyCheckIn reward pending/retry: " + error.Message);
			}
			finally { _isCollecting = false; }
		}

		private async UniTask ShowDayCollectRewards()
		{
			if (_data.EachDayClaimState == null) return;

			List<int> canClaimIndices = new List<int>();
			for (int i = 0; i < _data.EachDayClaimState.Count; i++)
			{
				if (_data.EachDayClaimState[i] == RewardState.CanClaim)
				{
					canClaimIndices.Add(i);
					_data.EachDayClaimState[i] = RewardState.Claimed;
				}
			}

			if (canClaimIndices.Count > 0)
			{
				bool collected = false;
				if (Delegate != null)
				{
					Delegate.ShowClaimDailyRewards(canClaimIndices, () => collected = true);
					await UniTask.WaitUntil(() => collected);
				}
				SaveData();
				OnClaimReward?.Invoke();
			}
		}

		private async UniTask ShowBoxCollectRewards()
		{
			if (_data.EachBoxClaimState == null) return;

			for (int i = 0; i < _data.EachBoxClaimState.Count; i++)
			{
				if (_data.EachBoxClaimState[i] == RewardState.CanClaim)
				{
					_isBoxCanCollect = false;
					_data.EachBoxClaimState[i] = RewardState.Claimed;

					if (Delegate != null)
					{
						Delegate.ShowClaimBoxReward(i, () => _isBoxCanCollect = true);
						await UniTask.WaitUntil(() => _isBoxCanCollect);
					}
					SaveData();
					OnClaimReward?.Invoke();
				}
			}
			OnDailyEventDone?.Invoke();
		}

		public int GetTodayIndex()
		{
			if (_data.LoginDays <= 0) return 0;
			return (_data.LoginDays - 1) % LOGIN_DAY_COUNT;
		}

		public int GetBoxLevelCondition(int boxIndex)
		{
			return _data.Interval * (boxIndex + 1) + _data.BoxStageStartDay - 1;
		}

		public float GetBoxProgressPercent()
		{
			float current = (_data.LoginDays - _data.BoxStageStartDay) + 1;
			float total = _data.Interval * BOX_COUNT;
			if (total <= 0f) return 0f;
			return Mathf.Clamp01(current / total);
		}

		public void SetLastRecordDay(long day)
		{
			_data.LastRecordDay = (int)day;
			SaveData();
		}

		public void JudgeCrossDay()
		{
			if (!IsOpen || _isCollecting) return;
			if (TimeUtils.IsToday(_data.LastRecordDay)) return;

			_data.IsTodayDialogShow = 0;
			_isTodayDialogShow = false;
			SaveData();

			_data.LastRecordDay = (int)TimeUtils.GetUnixTimestamp();
			if (_data.EachDayClaimState != null && _data.EachDayClaimState.Count > 0 &&
			    _data.EachDayClaimState[LOGIN_DAY_COUNT - 1] != RewardState.CanClaim)
			{
				_data.LoginDays++;
				DailyRewardRecordAdd();
				BoxRewardRecordAdd();
			}
			SaveData();
		}

		private void RecordDayAdd()
		{
			_data.LoginDays++;
			DailyRewardRecordAdd();
			BoxRewardRecordAdd();
			SaveData();
		}

		private void BoxRewardRecordAdd()
		{
			if (_data.EachBoxClaimState == null)
			{
				_data.EachBoxClaimState = Enumerable.Repeat(RewardState.NotMeetCondition, BOX_COUNT).ToList();
			}

			if (_data.EachBoxClaimState.All(s => s != RewardState.NotMeetCondition))
			{
				_data.EachBoxClaimState = _data.EachBoxClaimState.Select(s => RewardState.NotMeetCondition).ToList();
				_data.BoxStageStartDay = _data.LoginDays;
				_data.Interval = GetNewInterval(_data.LoginDays);
			}

			int stageDays = _data.LoginDays - _data.BoxStageStartDay + 1;
			if (stageDays >= _data.Interval && stageDays % _data.Interval == 0)
			{
				int i = stageDays / _data.Interval - 1;
				if (i >= 0 && i < BOX_COUNT && _data.EachBoxClaimState[i] == RewardState.NotMeetCondition)
				{
					_data.EachBoxClaimState[i] = RewardState.CanClaim;
				}
			}
		}

		private void DailyRewardRecordAdd()
		{
			if (_data.EachDayClaimState == null)
			{
				_data.EachDayClaimState = Enumerable.Repeat(RewardState.NotMeetCondition, LOGIN_DAY_COUNT).ToList();
			}

			if (_data.EachDayClaimState[LOGIN_DAY_COUNT - 1] == RewardState.Claimed)
			{
				_data.EachDayClaimState = _data.EachDayClaimState.Select(s => RewardState.NotMeetCondition).ToList();
			}

			int todayIndex = (_data.LoginDays - 1) % LOGIN_DAY_COUNT;
			if (todayIndex >= 0 && todayIndex < _data.EachDayClaimState.Count)
			{
				_data.EachDayClaimState[todayIndex] = RewardState.CanClaim;
			}
		}

		private void ReadData()
		{
			string json = PlayerPrefs.GetString(DailyPrefKeys.DailyCheckInData, "");
			if (string.IsNullOrEmpty(json))
			{
				ResetDailyData();
			}
			else
			{
				try
				{
					_data = JsonConvert.DeserializeObject<DailyPrefData>(json);
					_data.EachDayClaimState = RepairStates(_data.EachDayClaimState, LOGIN_DAY_COUNT);
					_data.EachBoxClaimState = RepairStates(_data.EachBoxClaimState, BOX_COUNT);
					_data.LoginDays = Math.Max(1, _data.LoginDays);
					_data.Interval = _data.Interval > 0 ? _data.Interval : GetNewInterval(_data.LoginDays);
					_data.BoxStageStartDay = Math.Max(1, Math.Min(_data.LoginDays, _data.BoxStageStartDay));
				}
				catch
				{
					ResetDailyData();
				}
			}
			_isTodayDialogShow = _data.IsTodayDialogShow == 1;
		}

		private static List<RewardState> RepairStates(List<RewardState> states, int count)
		{
			var repaired = new List<RewardState>(count);
			for (int i = 0; i < count; i++)
			{
				var state = states != null && i < states.Count ? states[i] : RewardState.NotMeetCondition;
				repaired.Add(state == RewardState.Claimed || state == RewardState.CanClaim ? state : RewardState.NotMeetCondition);
			}
			return repaired;
		}

		private void ResetDailyData()
		{
			_isTodayDialogShow = false;
			_isDialogShow = false;
			_isBoxCanCollect = false;
			_data = new DailyPrefData
			{
				LoginDays = 1,
				LastRecordDay = (int)TimeUtils.GetUnixTimestamp(),
				Interval = GetNewInterval(1),
				EachDayClaimState = Enumerable.Repeat(RewardState.NotMeetCondition, LOGIN_DAY_COUNT).ToList(),
				EachBoxClaimState = Enumerable.Repeat(RewardState.NotMeetCondition, BOX_COUNT).ToList(),
				Stage = 0,
				BoxStageStartDay = 1,
				IsTodayDialogShow = 0
			};
			_data.EachDayClaimState[0] = RewardState.CanClaim;
			SaveData();
		}

		private int GetNewInterval(int loginDays)
		{
			return 5;
		}

		private class DefaultDailyDataProvider : IDailyDataProvider
		{
			public List<RewardItem> GetDayRewardContents(int dayIndex)
			{
				switch (dayIndex)
				{
					case 0: return new List<RewardItem> { RewardItem.Coin(100) };
					case 1: return new List<RewardItem> { new RewardItem(RewardType.Undo, 1) };
					case 2: return new List<RewardItem> { RewardItem.Coin(150) };
					case 3: return new List<RewardItem> { new RewardItem(RewardType.Shuffle, 1) };
					case 4: return new List<RewardItem> { RewardItem.Heart(1) };
					case 5: return new List<RewardItem> { new RewardItem(RewardType.Path, 1) };
					case 6: return new List<RewardItem> { new RewardItem(RewardType.InfinityHeart, 1800), RewardItem.Coin(300) };
					default: return new List<RewardItem> { RewardItem.Coin(100) };
				}
			}

			public List<RewardItem> GetBoxRewardContents(int boxIndex)
			{
				switch (boxIndex)
				{
					case 0: return new List<RewardItem> { RewardItem.Coin(100) };
					case 1: return new List<RewardItem> { new RewardItem(RewardType.Undo, 1), RewardItem.Coin(150) };
					case 2: return new List<RewardItem> { new RewardItem(RewardType.InfinityHeart, 900), new RewardItem(RewardType.Shuffle, 1) };
					case 3: return new List<RewardItem> { new RewardItem(RewardType.Path, 2), RewardItem.Coin(250) };
					case 4: return new List<RewardItem> { new RewardItem(RewardType.InfinityHeart, 1800), RewardItem.Coin(500) };
					default: return new List<RewardItem> { RewardItem.Coin(100) };
				}
			}

			public DailyCheckInRewardConfig GetRewardConfig(RewardType rewardId)
			{
				return new DailyCheckInRewardConfig(rewardId, rewardId.GetIconPath());
			}

			public bool IsRewardTypeInfinite(RewardType rewardId)
			{
				return rewardId != null && rewardId.IsShowInfinityIcon();
			}
		}

		private class DefaultDailyDelegate : IDailyDelegate
		{
			private readonly DailyCheckInManager _manager;
			public DefaultDailyDelegate(DailyCheckInManager manager) { _manager = manager; }

			public void ShowClaimBoxReward(int boxIndex, Action callback)
			{
				var rewards = _manager.DataProvider?.GetBoxRewardContents(boxIndex);
				Claim("DailyCheckIn/box/" + _manager._data.BoxStageStartDay + "/" + boxIndex, rewards);
				callback?.Invoke();
			}

			public void ShowClaimDailyRewards(List<int> dayIndices, Action callback)
			{
				if (dayIndices != null)
				{
					foreach (var day in dayIndices)
					{
						var rewards = _manager.DataProvider?.GetDayRewardContents(day);
						int loginDay = _manager.LoginDays - _manager.GetTodayIndex() + day;
						Claim("DailyCheckIn/day/" + loginDay, rewards);
					}
				}
				callback?.Invoke();
			}

			private static void Claim(string receipt, List<RewardItem> rewards)
			{
				if (!InventoryManager.Ensure().TryClaimRewards(receipt, rewards, out _))
					throw new InvalidOperationException("Could not save daily reward.");
			}

			public void OnDailyCheckInPanelShow() { }
			public void OnDailyCheckInPanelClose() { }
		}
	}
}
