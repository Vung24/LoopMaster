using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Snui.ChampionArena
{
	public abstract class BaseChampionArenaManager : BaseEventManager
	{
		public const string DATA_FILE = GamePrefKeys.ChampionArena;
		private const int ROBOT_COUNT = 19;

		[HideInInspector]
		public UnityEvent OnEventStart = new UnityEvent();
		[HideInInspector]
		public UnityEvent OnEventEnd = new UnityEvent();

		protected ChampionArenaConfig _configWithCards;
		protected ChampionArenaConfig _configWithoutCards;
		protected ChampionArenaData _data = new ChampionArenaData();
		private bool _isEventRegistered;
		private bool _isOpponentsGenerating;

		public bool IsLocked => _data != null && _data.State == EventState.Lock;
		public bool IsWaitingStart => _data != null && _data.State == EventState.WaitingStart;
		public bool IsOngoing => _data != null && _data.State == EventState.Ongoing;
		public bool IsExpired => _data != null && _data.State == EventState.Expire;
		public bool IsClosed => _data != null && _data.State == EventState.Close;

		public long StartTime => _data != null ? _data.StartTimestamp : 0;
		public long EndTime => _data != null ? _data.EndTimestamp : 0;
		public int Round => _data != null ? _data.Round : 0;
		public int Score => _data != null ? _data.Score : 0;
		public int LastScore => _data != null ? _data.LastScore : 0;
		public int LastAddScore => _data != null ? _data.LastAddScore : 0;
		public int Rank => _data != null ? _data.Rank : 1;
		public int LastRank => _data != null ? _data.LastRank : 1;
		public List<EventRaceOpponent> Opponents => _data != null ? _data.Opponents : null;
		public bool IsFirstChampionArena => _data != null && _data.IsFirstChampionArena;
		public RaceTutorialStage TutorialStage => _data != null ? _data.TutorialStage : RaceTutorialStage.End;
		public int PlayerCount => 1 + ROBOT_COUNT;

		public long GetDuration() => EndTime - StartTime;
		public long GetRemainingTime()
		{
			long now = TimeUtils.GetUnixTimestamp();
			return now >= EndTime ? 0 : EndTime - now;
		}

		public abstract int GetTotalLevel();
		protected abstract long GetFirstStartTime();
		protected abstract void LoadConfig();

		public override void Startup()
		{
			base.Startup();
			LoadData();
			LoadConfig();
			CheckEventState();

			if (!_isEventRegistered && Managers.Event != null)
			{
				Managers.Event.RegisterEvent("OnGameWin", OnGameWin);
				_isEventRegistered = true;
			}
		}

		public override void EventBeforeEnterHome()
		{
			CheckEventState();
		}

		public override bool ShouldShowIcon()
		{
			return IsOngoing;
		}

		public override async UniTask<HomeEventBase> CreateIcon(Transform parent)
		{
			await UniTask.CompletedTask;
			return null;
		}

		protected virtual void OnGameWin(object args)
		{
			CheckEventState();
			if (IsOngoing && GameWinArgs.TryGet(args, GameWinArgs.ChampionArenaScore, out int addScore) && addScore >= 0)
			{
				_data.Round++;
				_data.Score += addScore;
				_data.LastAddScore = addScore;
				UpdateRanking();
				SaveData();
			}
		}

		public void OnOpenDialogShown()
		{
			if (_data != null)
			{
				_data.IsFirstChampionArena = false;
				SaveData();
			}
		}

		public void OnInfoDialogShown()
		{
			if (_data != null && _data.TutorialStage == RaceTutorialStage.ShowInfoDialog)
			{
				_data.TutorialStage = RaceTutorialStage.End;
				SaveData();
			}
		}

		public void ClearLastAddScore()
		{
			if (_data != null)
			{
				_data.LastAddScore = 0;
				SaveData();
			}
		}

		public void SynchronizeLastScore()
		{
			if (_data != null)
			{
				_data.LastScore = _data.Score;
				SaveData();
			}
		}

		public void SynchronizeLastRank()
		{
			if (_data != null)
			{
				_data.LastRank = _data.Rank;
				SaveData();
			}
		}

		public bool HasEnoughPuzzle() => Math.Max(GameProgressStore.NextPlayableLevel - 1,
			GameProgressStore.HighestCompletedLevel) < GetTotalLevel();

		public void CheckEventState()
		{
			if (_data == null) return;
			long now = TimeUtils.GetUnixTimestamp();
			EventState previous = _data.State;
			if ((_data.State == EventState.Lock || _data.State == EventState.Close) && !HasEnoughPuzzle())
				_data.State = EventState.WaitingStart;
			else if (_data.State == EventState.WaitingStart && HasEnoughPuzzle())
				_data.State = EventState.Close;
			if (_data.State == EventState.Ongoing && now > _data.EndTimestamp)
			{
				_data.State = EventState.Expire;
			}
			if (previous != _data.State) SaveData();
		}

		public bool CanStartEvent()
		{
			long now = TimeUtils.GetUnixTimestamp();
			return IsWaitingStart && !HasEnoughPuzzle() && now >= GetFirstStartTime() &&
			       now > EndTime && now < GetMonthEndTimestamp();
		}

		public void StartEvent()
		{
			if (!CanStartEvent()) return;
			_data.State = EventState.Ongoing;
			_data.StartTimestamp = TimeUtils.GetUnixTimestamp();
			_data.EndTimestamp = GetMonthEndTimestamp();
			_data.StartLv = GameProgressStore.NextPlayableLevel;
			_data.Round = 0;
			_data.Score = 1;
			_data.LastScore = 0;
			_data.LastAddScore = 0;
			_data.Rank = 1;
			_data.LastRank = 1;
			GenerateRandomOpponents().Forget();
			OnEventStart?.Invoke();
			SaveData();
		}

		public void CloseEvent()
		{
			if (IsExpired)
			{
				_data.State = HasEnoughPuzzle() ? EventState.Close : EventState.WaitingStart;
				SaveData();
				OnEventEnd?.Invoke();
			}
		}

		public ChampionArenaConfig GetConfig()
		{
			return _configWithoutCards ?? _configWithCards;
		}

		public ChampionArenaStage GetStage(int index)
		{
			var stages = GetStages();
			if (stages != null && index >= 0 && index < stages.Count)
			{
				return stages[index];
			}
			return null;
		}

		public List<ChampionArenaStage> GetStages()
		{
			var cfg = GetConfig();
			return cfg?.Stages ?? new List<ChampionArenaStage>();
		}

		public int GetCompletedStageCount(int score)
		{
			var stages = GetStages();
			int count = 0;
			long goal = 0;
			if (stages != null)
			{
				for (int i = 0; i < stages.Count; i++)
				{
					if (stages[i] == null) break;
					goal += stages[i].Goal;
					if (score < goal) break;
					count++;
				}
			}
			return count;
		}

		public int GetStageIndex(int score) => Math.Max(0, Math.Min(GetCompletedStageCount(score), GetStages().Count - 1));

		public (int progress, int goal) GetStageProgress(int score)
		{
			var stages = GetStages();
			if (stages == null || stages.Count == 0) return (0, 0);
			int index = GetStageIndex(score);
			long previousGoals = 0;
			for (int i = 0; i < index; i++) previousGoals += stages[i].Goal;
			int goal = stages[index].Goal;
			return ((int)Math.Max(0, Math.Min(goal, (long)score - previousGoals)), goal);
		}

		public List<RewardItem> GetClaimableStageRewards(int score, int lastScore)
		{
			var rewards = new List<RewardItem>();
			var stages = GetStages();
			int completed = GetCompletedStageCount(score);
			for (int i = GetCompletedStageCount(lastScore); i < completed; i++)
				if (stages[i].Reward != null) rewards.Add(stages[i].Reward);
			return rewards;
		}

		public RewardItem[] GetRankRewards(int rank)
		{
			var rewards = GetConfig()?.RankRewards;
			if (rank < 1 || rank > 10 || rewards == null) return null;
			int index = rank <= 3 ? rank - 1 : 3;
			return index < rewards.Count ? rewards[index] : null;
		}

		public bool TryClaimStageRewards()
		{
			if (_data == null || (!IsOngoing && !IsExpired)) return false;
			var stages = GetStages();
			int completed = GetCompletedStageCount(Score);
			for (int i = GetCompletedStageCount(LastScore); i < completed; i++)
			{
				if (stages[i].Reward == null) continue;
				if (!InventoryManager.Ensure().TryClaimRewards("ChampionArena/" + StartTime + "/" + EndTime + "/stage/" + i,
				    new[] { stages[i].Reward }, out _)) return false;
			}
			SynchronizeLastScore();
			return true;
		}

		public bool TryClaimRankRewards()
		{
			CheckEventState();
			if (!IsExpired || !TryClaimStageRewards()) return false;
			var rewards = GetRankRewards(Rank);
			if (rewards != null && rewards.Length > 0 && !InventoryManager.Ensure().TryClaimRewards(
			    "ChampionArena/" + StartTime + "/" + EndTime + "/rank", rewards, out _)) return false;
			SynchronizeLastRank();
			CloseEvent();
			return true;
		}

		private long GetMonthEndTimestamp()
		{
			DateTime date = DateTime.UtcNow.AddHours(-8);
			DateTime nextMonth = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddHours(8);
			return new DateTimeOffset(nextMonth).ToUnixTimeSeconds();
		}

		public void UpdateRanking()
		{
			if (_data == null || _data.Opponents == null) return;
			var self = _data.Opponents.Find(o => o.IsSelf);
			if (self != null) self.Score = _data.Score;

			_data.Opponents.Sort((a, b) => b.Score.CompareTo(a.Score));
			for (int i = 0; i < _data.Opponents.Count; i++)
			{
				_data.Opponents[i].Rank = i + 1;
			}
			if (self != null)
			{
				_data.Rank = self.Rank;
			}
		}

		private async UniTaskVoid GenerateRandomOpponents()
		{
			if (_isOpponentsGenerating) return;
			_isOpponentsGenerating = true;
			try
			{
				_data.Opponents = new List<EventRaceOpponent>();
				var self = EventRaceOpponent.GetSelf();
				self.Score = _data.Score;
				self.Rank = 1;
				_data.Opponents.Add(self);

				var skills = new Dictionary<int, int> { { 1, ROBOT_COUNT } };
				var bots = await RobotService.Instance.LoadRacePlayers(skills);
				for (int i = 0; i < bots.Count && _data.Opponents.Count < PlayerCount; i++)
				{
					var opp = new EventRaceOpponent(bots[i].Name, 1);
					opp.Avatar = bots[i].Avatar;
					opp.AvatarFrame = bots[i].AvatarFrame;
					opp.Score = 0;
					opp.Rank = _data.Opponents.Count + 1;
					_data.Opponents.Add(opp);
				}
				UpdateRanking();
				SaveData();
			}
			finally
			{
				_isOpponentsGenerating = false;
			}
		}

		private void LoadData()
		{
			string json = GamePrefs.GetString(DATA_FILE, string.Empty);
			if (!string.IsNullOrEmpty(json))
			{
				try
				{
					_data = JsonUtility.FromJson<ChampionArenaData>(json) ?? new ChampionArenaData();
				}
				catch
				{
					_data = new ChampionArenaData();
				}
			}
			else
			{
				_data = new ChampionArenaData();
			}
		}

		private void SaveData()
		{
			if (_data != null)
			{
				GamePrefs.SetString(DATA_FILE, JsonUtility.ToJson(_data));
				GamePrefs.Save();
			}
		}
	}
}
