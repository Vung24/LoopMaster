using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui.StreakLine
{
	public class BaseStreakLineManager : BaseEventManager
	{
		public const int UNLOCK_LEVEL = 26;
		private const string SAVE_KEY = GamePrefKeys.StreakLine;

		[SerializeField]
		private StreakLineConfig _config = new StreakLineConfig();

		private StreakLineData _data = new StreakLineData();
		private StreakLineAssetsCache _assetsCache;
		private bool _isEventRegistered;

		public StreakLineData Data => _data;
		public StreakLineAssetsCache AssetsCache
		{
			get
			{
				if (_assetsCache == null)
				{
					var prefab = EventPrefabUI.Load("StreakLineAssetsCache");
					if (prefab != null) _assetsCache = Instantiate(prefab, transform).GetComponent<StreakLineAssetsCache>();
				}
				return _assetsCache;
			}
		}
		public StreakLineConfig CurrentConfig => GetCurrentConfig();

		public bool IsLock => _data == null || _data.State == EventState.Lock;
		public bool IsOngoing => _data != null && _data.State == EventState.Ongoing;
		public bool IsClosed => _data != null && _data.State == (EventState)7;
		public long EndTime => _data != null ? _data.EndTime : 0;
		public bool IsExpired => TimeUtils.GetUnixTimestamp() >= EndTime;

		public virtual int GetUnlockLevel() => UNLOCK_LEVEL;
		public bool HasJoinedStreakLine() => _data != null && _data.HasJoined;
		public bool HasPlayedEnterAnim() => _data != null && _data.HasPlayedEnterAnim;
		public bool HasPlayedTutorial() => _data != null && _data.HasPlayedTutorial;
		public void SetPlayedEnterAnim() { if (_data != null) { _data.HasPlayedEnterAnim = true; SaveData(); } }
		public void OnTutorialShown() { if (_data != null) { _data.HasPlayedTutorial = true; SaveData(); } }

		public override void Startup()
		{
			base.Startup();
			if (_config == null || _config.Chests == null || _config.Chests.Count == 0)
			{
				var settings = StreakLineSettings.Instance;
				if (settings != null && settings.Config != null)
				{
					_config = settings.Config;
				}
			}

			LoadData();

			if (!_isEventRegistered && Managers.Event != null)
			{
				Managers.Event.RegisterEvent("OnGameWin", OnGameWonAction);
				Managers.Event.RegisterEvent("OnGameFail", OnGameFailedAction);
				_isEventRegistered = true;
			}
		}

		private void OnGameWonAction(object args) => OnGameWon();
		private void OnGameFailedAction(object args) => OnGameFailed();

		public long GetRemainingTime()
		{
			if (_data == null) return 0;
			return Math.Max(0, _data.EndTime - TimeUtils.GetUnixTimestamp());
		}

		public virtual long GetMinimumStartTime() => 900;

		public bool ShouldShowExclamation()
		{
			return CanShowClaimButton();
		}

		public bool CanShowClaimButton()
		{
			return GetClaimableChestCount() > 0 && !IsExpired;
		}

		public bool ShouldShowRewardMilestonePopup()
		{
			if (!IsOngoing || IsExpired || GetTempViewLevel() <= 0) return false;
			long currentLevel = (long)GetCurrLevel() + GetTempLevel();
			long previousLevel = Math.Max(0L, currentLevel - GetTempViewLevel());
			foreach (var chest in GetChests())
				if (chest != null && chest.Level > previousLevel && chest.Level <= currentLevel &&
					!IsChestClaimed(chest.Level)) return true;
			return false;
		}

		public bool CanClaimChest(int level)
		{
			if (_data == null || !IsOngoing || IsExpired || GetChest(level) == null) return false;
			return _data.CurrLevel >= level && !IsChestClaimed(level);
		}

		public bool IsChestClaimed(int level)
		{
			return _data != null && _data.ClaimedChests != null && _data.ClaimedChests.Contains(level);
		}

		public bool IsClaimedAllRewards()
		{
			var config = GetCurrentConfig();
			if (config == null || config.Chests == null || _data == null || _data.ClaimedChests == null) return false;
			if (config.Chests.Count == 0) return false;
			foreach (var chest in config.Chests)
				if (chest == null || !IsChestClaimed(chest.Level)) return false;
			return true;
		}

		public int GetCurrLevel() => _data != null ? _data.CurrLevel : 0;
		public int GetTempLevel() => _data != null ? _data.TempLevel : 0;
		public int GetTempViewLevel() => _data != null ? _data.TempViewLevel : 0;
		public int GetPrevViewLevel() => _data != null ? Math.Max(0, _data.CurrLevel - _data.TempViewLevel) : 0;
		public int GetMaxReachLevel() => _data != null ? _data.MaxReachLevel : 0;

		public List<StreakLineChest> GetChests()
		{
			var config = GetCurrentConfig();
			return config != null ? config.Chests : new List<StreakLineChest>();
		}

		public StreakLineChest GetChest(int level)
		{
			var chests = GetChests();
			if (chests != null)
			{
				foreach (var c in chests)
				{
					if (c != null && c.Level == level) return c;
				}
			}
			return null;
		}

		public StreakLineConfig GetCurrentConfig()
		{
			return _config;
		}

		public override void EventBeforeEnterHome()
		{
			CheckEventState();
		}

		public virtual void CheckEventState()
		{
			if (_data == null) return;
			if (_data.State == EventState.Lock)
			{
				int level = GameProgressStore.NextPlayableLevel;
				if (level >= GetUnlockLevel() && CanStartEvent())
				{
					StartEvent();
				}
				return;
			}

			if (_data.State == EventState.Ongoing)
			{
				if (IsExpired)
				{
					_data.State = (EventState)7;
					SaveData();
				}
			}
			else if (_data.State == (EventState)7)
			{
				if (CanStartEvent())
				{
					StartEvent();
				}
			}
		}

		public override bool ShouldShowIcon()
		{
			if (IsOngoing) return true;
			if (_data == null || IsClosed) return false;
			var timer = GetWeeklyTimeSlot();
			return timer != null && timer.IsOngoing() &&
			       (GameProgressStore.NextPlayableLevel < GetUnlockLevel() || timer.GetRemainingTime() > GetMinimumStartTime());
		}

		public override UniTask<HomeEventBase> CreateIcon(Transform parent)
		{
			var prefab = EventPrefabUI.Load("HomeEventStreakLine");
			return UniTask.FromResult(prefab != null ? Instantiate(prefab, parent, false).GetComponent<HomeEventBase>() : null);
		}

		public virtual void OnGameWon()
		{
			if (_data == null || _data.State != EventState.Ongoing || IsExpired ||
			    _data.CurrLevel + _data.TempLevel >= GetFinalGoal()) return;
			_data.HasJoined = true;
			_data.TempLevel++;
			_data.TempViewLevel++;
			_data.MaxReachLevel = Math.Max(_data.MaxReachLevel, _data.CurrLevel + _data.TempLevel);
			SaveData();
		}

		public virtual void OnGameFailed()
		{
			if (_data == null || _data.State != EventState.Ongoing || _data.CurrLevel >= GetFinalGoal()) return;
			int checkpoint = 0;
			foreach (var chest in GetChests())
				if (chest != null && chest.Level <= _data.CurrLevel)
					checkpoint = Math.Max(checkpoint, chest.Level);
			_data.CurrLevel = checkpoint;
			_data.TempLevel = 0;
			_data.TempViewLevel = 0;
			SaveData();
		}

		public virtual void OnDetailShown()
		{
			SynchronizeTempViewLevel();
		}

		public int GetOpenedBoxCount()
		{
			return _data != null && _data.ClaimedChests != null ? _data.ClaimedChests.Count : 0;
		}

		public List<StreakLineChest> GetAllClaimableRewards()
		{
			var result = new List<StreakLineChest>();
			var chests = GetChests();
			if (chests != null && _data != null)
			{
				foreach (var c in chests)
				{
					if (c != null && CanClaimChest(c.Level))
					{
						result.Add(c);
					}
				}
			}
			return result;
		}

		public int GetClaimableChestCount()
		{
			return GetAllClaimableRewards().Count;
		}

		public virtual void ClaimRewards(int level)
		{
			if (_data == null || !CanClaimChest(level)) return;
			var chest = GetChest(level);
			if (chest != null && chest.Items().Count > 0)
			{
				if (!InventoryManager.Ensure().TryClaimRewards("StreakLine/" + EndTime + "/" + level, chest.Items(), out _)) return;
				_data.ClaimedChests.Add(level);
				SaveData();
				if (IsClaimedAllRewards()) CloseEvent();
			}
		}

		public virtual void ClaimAllRewards()
		{
			var claimable = GetAllClaimableRewards();
			foreach (var c in claimable)
			{
				ClaimRewards(c.Level);
			}
		}

		public virtual int GetFinalGoal()
		{
			var config = GetCurrentConfig();
			if (config != null && config.Chests != null && config.Chests.Count > 0)
			{
				return config.Chests[config.Chests.Count - 1].Level;
			}
			return 10;
		}

		public virtual bool CanStartEvent()
		{
			if (_data != null && !IsExpired) return false;
			var timer = GetWeeklyTimeSlot();
			return timer != null && timer.IsOngoing() && timer.GetRemainingTime() > GetMinimumStartTime();
		}

		public virtual EventWeeklyTimer GetWeeklyTimeSlot()
		{
			return new EventWeeklyTimer(DayOfWeek.Monday, DayOfWeek.Thursday);
		}

		public virtual void StartEvent()
		{
			if (!CanStartEvent()) return;
			if (_data == null) _data = new StreakLineData();
			_data.State = EventState.Ongoing;
			var timer = GetWeeklyTimeSlot();
			if (timer != null)
			{
				_data.EndTime = new DateTimeOffset(timer.EndTime).ToUnixTimeSeconds();
			}
			_data.CurrLevel = 0;
			_data.TempLevel = 0;
			_data.TempViewLevel = 0;
			_data.MaxReachLevel = 0;
			_data.ClaimedChests.Clear();
			_data.HasJoined = false;
			_data.HasPlayedEnterAnim = false;
			SaveData();
		}

		public virtual bool CanCloseEvent()
		{
			return IsExpired || IsClaimedAllRewards();
		}

		public virtual void CloseEvent()
		{
			if (_data != null)
			{
				_data.State = (EventState)7;
				SaveData();
			}
		}

		public virtual void SynchronizeTempLevel()
		{
			if (_data == null) return;
			_data.CurrLevel += _data.TempLevel;
			_data.TempLevel = 0;
			SaveData();
		}

		public virtual void SynchronizeTempViewLevel()
		{
			if (_data == null) return;
			_data.TempViewLevel = 0;
			SaveData();
		}

		public virtual string GetTitleI18n() => "Streak Line";

		public virtual void LoadData()
		{
			try
			{
				if (GamePrefs.HasKey(SAVE_KEY))
				{
					string json = GamePrefs.GetString(SAVE_KEY, string.Empty);
					if (!string.IsNullOrEmpty(json))
					{
						_data = JsonUtility.FromJson<StreakLineData>(json);
					}
				}
			}
			catch (Exception)
			{
				_data = new StreakLineData();
			}

			if (_data == null) _data = new StreakLineData();
			if (_data.ClaimedChests == null) _data.ClaimedChests = new List<int>();
		}

		public virtual void SaveData()
		{
			if (_data == null) return;
			try
			{
				string json = JsonUtility.ToJson(_data);
				GamePrefs.SetString(SAVE_KEY, json);
				GamePrefs.Save();
			}
			catch (Exception)
			{
			}
		}

		public override void ClearData()
		{
			_data = new StreakLineData();
			GamePrefs.DeleteKey(SAVE_KEY);
			GamePrefs.Save();
		}

		public virtual UniTask PreloadAssets() => UniTask.CompletedTask;
		public virtual void ReleaseAssets() => _assetsCache?.Clear();
	}
}
