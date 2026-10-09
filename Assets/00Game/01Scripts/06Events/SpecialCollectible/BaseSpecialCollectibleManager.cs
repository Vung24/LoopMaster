using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui.SpecialCol
{
	public class BaseSpecialCollectibleManager : BaseManager
	{
		public const int UNLOCK_LEVEL = 6;
		private const string SAVE_KEY = "special_collectible_data";
		private const string PREF_TUTORIAL_SHOWN = "special_col_tutorial_shown";
		private const string PREF_GAME_TUTORIAL_SHOWN = "special_col_game_tutorial_shown";

		[SerializeField]
		private List<SpecialColConfig> _configs = new List<SpecialColConfig>();

		private SpecialColData _data = new SpecialColData();
		private bool _isEventRegistered;

		public SpecialColData Data => _data;
		public List<SpecialColConfig> Configs => _configs;

		public bool IsLock => _data == null || _data.State == EventState.Lock;
		public bool IsOngoing => _data != null && _data.State == EventState.Ongoing;
		public bool IsClosed => _data != null && _data.State == (EventState)7;
		public long EndTime => _data != null ? _data.EndTime : 0;
		public bool IsExpired => TimeUtils.GetUnixTimestamp() >= EndTime;
		public bool IsDouble => _data != null && _data.IsDouble;
		public int CurrCount => _data != null ? _data.CurrCount : 0;
		public int TempCount => _data != null ? _data.TempCount : 0;
		public int CompletedIndex => _data != null ? _data.CompletedIndex : 0;
		public int CollectibleIndex => GetCurrConfig() != null ? GetCurrConfig().CollectibleIndex : 0;
		public bool ShouldPlayAnimation => _data != null && _data.TempCount > 0;

		public virtual int GetUnlockLevel() => UNLOCK_LEVEL;

		public override void Startup()
		{
			base.Startup();
			if (_configs == null || _configs.Count == 0)
			{
				var settings = SpecialCollectibleSettings.Instance;
				if (settings != null && settings.Configs != null)
				{
					_configs = settings.Configs;
				}
			}

			LoadData();

			if (!_isEventRegistered && Managers.Event != null)
			{
				Managers.Event.RegisterEvent("OnGameWin", OnGameWonAction);
				_isEventRegistered = true;
			}
		}

		private void OnGameWonAction(object args)
		{
			OnGameWon(args);
		}

		public long GetRemainingTime()
		{
			if (_data == null) return 0;
			return Math.Max(0, _data.EndTime - TimeUtils.GetUnixTimestamp());
		}

		public virtual long GetMinimumStartTime() => 900;

		public long GetDoubleRemainTime()
		{
			long expireTime = PlayerPrefs.GetInt("doubleSpecial_expire", 0);
			return Math.Max(0, expireTime - TimeUtils.GetUnixTimestamp());
		}

		public bool IsCompleted()
		{
			var config = GetCurrConfig();
			if (config == null || config.Cells == null || config.Cells.Count == 0) return false;
			return _data != null && _data.CompletedIndex >= config.Cells.Count;
		}

		public SpecialColConfig GetCurrConfig()
		{
			if (_configs == null || _configs.Count == 0) return null;
			int index = _data != null ? _data.ConfigIndex : 0;
			if (index < 0 || index >= _configs.Count) return _configs[0];
			return _configs[index];
		}

		public virtual void EventBeforeEnterHome()
		{
			CheckEventState();
			CheckDoubleState();
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
				else
				{
					_data.ConfigIndex = GetConfigIndex();
				}
				return;
			}

			if (_data.State == EventState.Ongoing)
			{
				if (IsExpired || IsCompleted())
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

		public virtual void CheckDoubleState()
		{
			if (_data != null)
			{
				_data.IsDouble = GetDoubleRemainTime() > 0;
			}
		}

		public virtual bool ShouldShowProgress()
		{
			if (_data == null) return false;
			return _data.State == EventState.Lock || _data.State == EventState.Ongoing;
		}

		public virtual Sprite GetCollectibleSprite()
		{
			var settings = SpecialCollectibleSettings.Instance;
			if (settings == null || settings.CollectibleSprites == null || settings.CollectibleSprites.Count == 0) return null;
			int idx = CollectibleIndex;
			if (idx >= 0 && idx < settings.CollectibleSprites.Count)
			{
				return settings.CollectibleSprites[idx];
			}
			return settings.CollectibleSprites[0];
		}

		public virtual string GetEventName()
		{
			var settings = SpecialCollectibleSettings.Instance;
			if (settings == null || settings.EventNames == null || settings.EventNames.Count == 0) return "Special Collectible";
			int idx = CollectibleIndex;
			if (idx >= 0 && idx < settings.EventNames.Count)
			{
				return settings.EventNames[idx];
			}
			return settings.EventNames[0];
		}

		public virtual string GetEventDesc()
		{
			var settings = SpecialCollectibleSettings.Instance;
			return settings != null ? settings.EventDesc : string.Empty;
		}

		public virtual bool CanStartEvent()
		{
			if (_data != null && !IsExpired) return false;

			var timer1 = GetWeeklyTimeSlot1();
			if (timer1 != null && timer1.IsOngoing())
			{
				if (timer1.GetRemainingTime() > GetMinimumStartTime()) return true;
			}

			var timer2 = GetWeeklyTimerSlot2();
			if (timer2 != null && timer2.IsOngoing())
			{
				if (timer2.GetRemainingTime() > GetMinimumStartTime()) return true;
			}

			return false;
		}

		public virtual EventWeeklyTimer GetWeeklyTimeSlot1()
		{
			return new EventWeeklyTimer(DayOfWeek.Friday, DayOfWeek.Monday);
		}

		public virtual EventWeeklyTimer GetWeeklyTimerSlot2()
		{
			return new EventWeeklyTimer(DayOfWeek.Monday, DayOfWeek.Friday);
		}

		public virtual int GetConfigIndex()
		{
			var timer1 = GetWeeklyTimeSlot1();
			return (timer1 != null && timer1.IsOngoing()) ? 0 : 1;
		}

		public virtual void StartEvent()
		{
			if (_data == null) _data = new SpecialColData();
			_data.State = EventState.Ongoing;

			var timer1 = GetWeeklyTimeSlot1();
			if (timer1 != null && timer1.IsOngoing())
			{
				_data.EndTime = new DateTimeOffset(timer1.EndTime).ToUnixTimeSeconds();
			}
			else
			{
				var timer2 = GetWeeklyTimerSlot2();
				if (timer2 != null)
				{
					_data.EndTime = new DateTimeOffset(timer2.EndTime).ToUnixTimeSeconds();
				}
			}

			_data.ConfigIndex = GetConfigIndex();
			_data.CurrCount = 0;
			_data.TempCount = 0;
			_data.CompletedIndex = 0;
			int currentLevel = GameProgressStore.NextPlayableLevel;
			_data.StartLevel = currentLevel;
			_data.LastLevel = currentLevel;

			SaveData();
		}

		public virtual bool CanCloseEvent()
		{
			return IsExpired || IsCompleted();
		}

		public virtual void CloseEvent()
		{
			if (_data != null)
			{
				_data.State = (EventState)7;
				SaveData();
			}
		}

		public virtual void OnGameWon(object args)
		{
			if (_data == null || _data.State != EventState.Ongoing) return;
			if (ShouldShowTutorial()) return;

			if (!GameWinArgs.TryGet(args, GameWinArgs.SpecialCollectible, out int added) || added < 0) return;

			if (_data.IsDouble)
			{
				added *= 2;
			}

			_data.TempCount += added;
			_data.CollectibleIndex = CollectibleIndex;
			SaveData();
		}

		public virtual void SynchronizeTempLevel()
		{
			if (_data == null) return;
			_data.CurrCount += _data.TempCount;
			_data.TempCount = 0;
			CheckDoubleState();
			SaveData();
		}

		public virtual bool ShouldShowTutorial()
		{
			int shown = PlayerPrefs.GetInt(PREF_TUTORIAL_SHOWN, 0);
			int level = GameProgressStore.NextPlayableLevel;
			return shown == 0 && (level + 1 == GetUnlockLevel());
		}

		public virtual void SetTutorialShown()
		{
			PlayerPrefs.SetInt(PREF_TUTORIAL_SHOWN, 1);
			PlayerPrefs.Save();
		}

		public virtual bool ShouldShowGameTutorial()
		{
			return PlayerPrefs.GetInt(PREF_GAME_TUTORIAL_SHOWN, 0) == 0;
		}

		public virtual void SetGameTutorialShown()
		{
			PlayerPrefs.SetInt(PREF_GAME_TUTORIAL_SHOWN, 1);
			PlayerPrefs.Save();
		}

		public virtual SpecialColCell GetCurrSpecialColCell()
		{
			var config = GetCurrConfig();
			if (config == null || config.Cells == null || config.Cells.Count == 0) return null;
			int idx = _data != null ? _data.CompletedIndex : 0;
			if (idx >= 0 && idx < config.Cells.Count)
			{
				return config.Cells[idx];
			}
			return null;
		}

		public virtual List<SpecialColCell> GetClaimableRewards()
		{
			var result = new List<SpecialColCell>();
			var config = GetCurrConfig();
			if (config == null || config.Cells == null || _data == null) return result;

			int completedIdx = _data.CompletedIndex;
			int curr = _data.CurrCount;

			while (completedIdx < config.Cells.Count)
			{
				var cell = config.Cells[completedIdx];
				if (cell == null || curr < cell.Count) break;
				result.Add(cell);
				completedIdx++;
				curr -= cell.Count;
			}

			return result;
		}

		public virtual void CollectRewards(List<SpecialColCell> rewards)
		{
			if (rewards == null || rewards.Count == 0 || _data == null) return;
			foreach (var cell in rewards)
			{
				if (cell == null || !ReferenceEquals(cell, GetCurrSpecialColCell()) || _data.CurrCount < cell.Count) break;
				if (cell.Reward != null)
				{
					if (!InventoryManager.Ensure().TryClaimRewards("SpecialCollectible/" + EndTime + "/" + _data.ConfigIndex + "/" + _data.CompletedIndex,
					    new[] { cell.Reward }, out _)) break;
				}
				_data.CurrCount = Math.Max(0, _data.CurrCount - cell.Count);
				_data.CompletedIndex++;
				SaveData();
			}
			int currentLevel = GameProgressStore.NextPlayableLevel;
			_data.LastLevel = currentLevel;
			SaveData();
		}

		public virtual void OnInventoryChanged(string item, int count)
		{
			if (item == "doubleSpecial")
			{
				CheckDoubleState();
			}
		}

		public virtual void LoadData()
		{
			try
			{
				if (PlayerPrefs.HasKey(SAVE_KEY))
				{
					string json = PlayerPrefs.GetString(SAVE_KEY, string.Empty);
					if (!string.IsNullOrEmpty(json))
					{
						_data = JsonUtility.FromJson<SpecialColData>(json);
					}
				}
			}
			catch (Exception)
			{
				_data = new SpecialColData();
			}

			if (_data == null)
			{
				_data = new SpecialColData();
			}
		}

		public virtual void SaveData()
		{
			if (_data == null) return;
			try
			{
				string json = JsonUtility.ToJson(_data);
				PlayerPrefs.SetString(SAVE_KEY, json);
				PlayerPrefs.Save();
			}
			catch (Exception)
			{
			}
		}

		public override void ClearData()
		{
			_data = new SpecialColData();
			PlayerPrefs.DeleteKey(SAVE_KEY);
			PlayerPrefs.Save();
		}
	}
}
