using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Snui.SpaceRace
{
	[Serializable]
	public class SpaceRaceData
	{
		public EventState State = EventState.Lock;
		public long SessionEndTime;
		public long StageEndTime;
		public int Stage;
		public int StageFailCount;
		public int Levels;
		public int WinCount;
		public int Score;
		public int TempScore;
		public int LastScore;
		public int Rank = 1;
		public bool InitialRewardClaimed;
		public long LastPromptTime;
		public List<EventRaceOpponent> Opponents = new List<EventRaceOpponent>();
		public int TutorialStage;
		public int Result;
		public int EnterLevel;
	}

	public class BaseSpaceRaceManager : BaseEventManager
	{
		private const string SAVE_KEY = "space_race";
		private const int UNLOCK_LEVEL = 30;

		[SerializeField] private SpaceRaceSettings _settings;
		[SerializeField] private UnityEvent _onWinEvent = new UnityEvent();

		protected SpaceRaceData _data = new SpaceRaceData();

		public SpaceRaceSettings Settings => _settings != null ? _settings : SpaceRaceSettings.Instance;
		public EventState State => _data != null ? _data.State : EventState.Lock;
		public bool IsLocked => State == EventState.Lock;

		public bool IsWaitingStart
		{
			get
			{
				if (_data != null && _data.State == EventState.WaitingStart)
				{
					CheckStateUpdate();
				}
				return _data != null && _data.State == EventState.WaitingStart;
			}
		}

		public bool IsOngoing => _data != null && _data.State == EventState.Ongoing;
		public bool IsWin => _data != null && _data.Result == 1;
		public bool IsFail => _data != null && _data.Result == 2;
		public bool IsClosed => _data != null && _data.State == EventState.Close;

		public long SessionEndTime => _data != null ? _data.SessionEndTime : 0;
		public long StageEndTime => _data != null ? _data.StageEndTime : 0;
		public bool InitialRewardClaimed => _data != null && _data.InitialRewardClaimed;
		public int Rank => _data != null ? _data.Rank : 1;
		public int Stage => _data != null ? _data.Stage : 0;
		public int StageFailCount => _data != null ? _data.StageFailCount : 0;
		public int Score => _data != null ? _data.Score : 0;
		public int TotalScore => (_data != null ? _data.Score : 0) + (_data != null ? _data.TempScore : 0);
		public int TempScore => _data != null ? _data.TempScore : 0;
		public int LastScore => _data != null ? _data.LastScore : 0;
		public List<EventRaceOpponent> Opponents => _data != null ? _data.Opponents : null;
		public int WinCount => _data != null ? _data.WinCount : 0;

		public int TutorialStage
		{
			get => _data != null ? _data.TutorialStage : 0;
			set
			{
				if (_data != null)
				{
					_data.TutorialStage = value;
					SaveData();
				}
			}
		}

		public UnityEvent OnWinEvent => _onWinEvent;

		public override void Startup()
		{
			base.Startup();
			LoadData();
			if (Managers.Event != null)
			{
				Managers.Event.RegisterEvent("OnGameWin", OnGameWin);
				Managers.Event.RegisterEvent("OnEnterGame", OnEnterGame);
			}
		}

		public override void EventBeforeEnterHome()
		{
			if (_data == null) return;

			if (_data.State == EventState.Ongoing)
			{
				if (_data.Result == 0)
				{
					UpdateRanking();
					if (TimeUtils.IsExpired(_data.StageEndTime))
					{
						if (_data.Rank == 1)
						{
							_onWinEvent?.Invoke();
							_data.Result = 1;
						}
						else
						{
							_data.Result = 2;
						}
						SaveData();
					}
				}
				return;
			}

			if (_data.State == EventState.WaitingStart)
			{
				if (TimeUtils.IsExpired(_data.SessionEndTime))
				{
					if (!IsInTimeSlot())
					{
						_data.State = EventState.Close;
						SaveData();
						return;
					}
				}
			}
			else if (_data.State == EventState.Lock)
			{
				if (GameProgressStore.NextPlayableLevel < GetUnlockLevel())
				{
					return;
				}
				if (!IsInTimeSlot())
				{
					return;
				}
			}
			else if (_data.State == EventState.Close)
			{
				if (!IsInTimeSlot())
				{
					return;
				}
			}

			_data.State = EventState.WaitingStart;
			var timeSlot = GetWeeklyTimeSlot();
			if (timeSlot != null)
			{
				_data.SessionEndTime = new DateTimeOffset(timeSlot.EndTime).ToUnixTimeSeconds();
			}
			_data.Stage = 0;
			_data.StageFailCount = 0;
			_data.Levels = 0;
			_data.InitialRewardClaimed = false;
			_data.WinCount = 0;
			SaveData();
		}

		public override bool ShouldShowIcon()
		{
			if (_data == null) return false;
			if (_data.State == EventState.Ongoing) return true;

			var slot = GetWeeklyTimeSlot();
			if (slot == null) return false;

			long duration = GetStageDuration(_data.Stage);
			long now = TimeUtils.GetUnixTimestamp();
			long start = new DateTimeOffset(slot.StartTime).ToUnixTimeSeconds();
			long end = new DateTimeOffset(slot.EndTime).ToUnixTimeSeconds();

			return now >= start && now < (end - duration);
		}

		public virtual int GetUnlockLevel() => UNLOCK_LEVEL;

		public override async UniTask<HomeEventBase> CreateIcon(Transform parent)
		{
			await UniTask.Yield();
			var prefab = EventPrefabUI.Load("HomeEventSpaceRace");
			if (prefab != null && parent != null)
			{
				var go = Instantiate(prefab, parent);
				return go.GetComponent<HomeEventBase>();
			}
			return null;
		}

		public bool ShouldPromptStart()
		{
			if (_data == null || _data.State != EventState.WaitingStart) return false;
			if (_data.TutorialStage == 0) return true;

			if (!TimeUtils.IsToday(_data.LastPromptTime))
			{
				return true;
			}
			return false;
		}

		public void OnShowPrompt()
		{
			if (_data == null) return;
			if (_data.TutorialStage == 0)
			{
				_data.TutorialStage = 1;
			}
			_data.LastPromptTime = TimeUtils.GetUnixTimestamp();
			SaveData();
		}

		public long GetStageDuration(int stageIndex)
		{
			if (Settings != null && Settings.StageDurations != null && stageIndex < Settings.StageDurations.Count)
			{
				return Settings.StageDurations[stageIndex];
			}
			return 86400;
		}

		public List<RewardItem> GetStageRewards(int stageIndex)
		{
			if (Settings == null) return new List<RewardItem>();
			if (_data != null && _data.WinCount + 1 < 4 && Settings.CardPackRewards != null && Settings.CardPackRewards.Count > stageIndex)
			{
				return Settings.CardPackRewards[stageIndex];
			}
			if (Settings.StageRewards != null && stageIndex < Settings.StageRewards.Count)
			{
				return Settings.StageRewards[stageIndex];
			}
			return new List<RewardItem>();
		}

		public async UniTask<bool> StartRace()
		{
			var searchingDialog = await SearchingDialog.Show();
			var opponents = await GenerateRandomOpponent();

			if (searchingDialog != null)
			{
				searchingDialog.Dismiss();
			}

			if (opponents == null || opponents.Count == 0)
			{
				return false;
			}

			_data.Opponents = opponents;
			_data.State = EventState.Ongoing;
			_data.StageEndTime = TimeUtils.GetUnixTimestamp() + GetStageDuration(_data.Stage);
			_data.Score = 0;
			_data.TempScore = 0;
			_data.LastScore = 0;
			_data.Result = 0;
			_data.Rank = 1;

			UpdateRanking();
			SaveData();
			return true;
		}

		public void ResetLastScore()
		{
			if (_data == null) return;
			_data.LastScore = _data.Score;
			SaveData();
		}

		public bool HasEnoughTimeForNextRace()
		{
			if (_data == null) return false;
			long duration = GetStageDuration(_data.Stage);
			return TimeUtils.GetUnixTimestamp() + duration < _data.SessionEndTime;
		}

		public void CloseRace()
		{
			if (_data == null || (_data.Result != 1 && _data.Result != 2)) return;

			if (_data.Result == 1)
			{
				if (_data.Stage + 1 < 3)
				{
					_data.Stage++;
				}
				_data.StageFailCount = 0;
				_data.EnterLevel = 0;
				_data.WinCount++;
			}
			else if (_data.Result == 2)
			{
				_data.StageFailCount++;
				_data.EnterLevel = 0;
			}

			bool hasTime = HasEnoughTimeForNextRace();
			_data.State = hasTime ? EventState.WaitingStart : EventState.Close;
			_data.Result = 0;
			_data.Levels = 0;
			SaveData();
		}

		public void RefreshRobots()
		{
			if (_data == null || _data.State != EventState.Ongoing) return;
			if (_data.Result != 0) return;

			UpdateRanking();
			if (TimeUtils.IsExpired(_data.StageEndTime))
			{
				if (_data.Rank == 1)
				{
					_onWinEvent?.Invoke();
					_data.Result = 1;
				}
				else
				{
					_data.Result = 2;
				}
				SaveData();
			}
		}

		public void ClearTempScore()
		{
			if (_data == null) return;
			if (_data.TempScore > 0)
			{
				_data.Score += _data.TempScore;
				_data.TempScore = 0;
				RefreshRobots();
				SaveData();
			}
		}

		public void CheckStateUpdate()
		{
			EventBeforeEnterHome();
		}

		public bool IsInTimeSlot()
		{
			var timer = GetWeeklyTimeSlot();
			if (timer == null) return false;
			long now = TimeUtils.GetUnixTimestamp();
			long start = new DateTimeOffset(timer.StartTime).ToUnixTimeSeconds();
			long end = new DateTimeOffset(timer.EndTime).ToUnixTimeSeconds();
			return now >= start && now < end;
		}

		public EventWeeklyTimer GetWeeklyTimeSlot()
		{
			return new EventWeeklyTimer(DayOfWeek.Friday, DayOfWeek.Monday);
		}

		public void StartNewSession()
		{
			if (_data == null) return;
			_data.State = EventState.WaitingStart;
			var timeSlot = GetWeeklyTimeSlot();
			if (timeSlot != null)
			{
				_data.SessionEndTime = new DateTimeOffset(timeSlot.EndTime).ToUnixTimeSeconds();
			}
			_data.Stage = 0;
			_data.StageFailCount = 0;
			_data.Levels = 0;
			_data.InitialRewardClaimed = false;
			_data.WinCount = 0;
			SaveData();
		}

		public void OnGameWin(object args)
		{
			if (GameWinArgs.TryGet(args, "spaceRaceStar", out int starCount))
			{
				CheckStateUpdate();
				if (_data != null && _data.State == EventState.Ongoing && _data.Result == 0)
				{
					_data.TempScore += starCount;
					_data.Levels++;
					SaveData();
				}
			}
		}

		public void OnEnterGame()
		{
			if (_data != null && _data.State == EventState.Ongoing)
			{
				_data.EnterLevel++;
				SaveData();
			}
		}

		public async UniTask<List<EventRaceOpponent>> GenerateRandomOpponent()
		{
			var skills = new Dictionary<int, int> { { 1, 4 } };
			var racePlayers = await RobotService.Instance.LoadRacePlayers(skills);

			var opponents = new List<EventRaceOpponent>();
			var rng = new System.Random();

			for (int i = 0; i < 4; i++)
			{
				var opponent = new EventRaceOpponent();
				if (racePlayers != null && i < racePlayers.Count)
				{
					opponent.Name = racePlayers[i].Name;
					opponent.Avatar = racePlayers[i].Avatar;
				}
				else
				{
					opponent.Name = "Pilot" + rng.Next(100, 999);
					opponent.Avatar = "avatar_" + rng.Next(1, 10);
				}
				opponent.Score = 0;
				opponent.Rank = i + 2;
				opponent.Speed = rng.Next(1, 4);
				opponents.Add(opponent);
			}

			return opponents;
		}

		public void UpdateRanking()
		{
			if (_data == null || _data.Opponents == null) return;

			_data.Opponents = _data.Opponents.OrderByDescending(o => o.Score).ToList();

			int totalScore = TotalScore;
			if (totalScore == 0)
			{
				_data.Rank = _data.Opponents.Count + 1;
			}
			else
			{
				_data.Rank = _data.Opponents.FindAll(o => o.Score > totalScore).Count + 1;
			}

			int currentRank = 1;
			foreach (var opponent in _data.Opponents)
			{
				if (currentRank == _data.Rank)
				{
					currentRank++;
				}
				opponent.Rank = currentRank;
				currentRank++;
			}

			SaveData();
		}

		public void LoadData()
		{
			_data = IoUtils.ReadJsonFile<SpaceRaceData>(SAVE_KEY);
			if (_data == null)
			{
				_data = new SpaceRaceData();
			}
			if (_data.WinCount < _data.Stage)
			{
				_data.WinCount = _data.Stage;
			}
		}

		public void SaveData()
		{
			if (_data != null)
			{
				IoUtils.WriteJsonFileAsync(SAVE_KEY, _data).Forget();
			}
		}

		public override void ClearData()
		{
			_data = new SpaceRaceData();
			SaveData();
		}
	}
}
