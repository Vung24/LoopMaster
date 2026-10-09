using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Snui.Lava
{
	[Serializable]
	public class LavaConfig
	{
		public float RefreshTime = 0f;
		public int Duration = 86400;
		public int LevelGoal = 7;
		public int CoinRewardCount = 10000;
		public int PlayerCount = 100;
		public int PromptInterval = 5;
	}

	[Serializable]
	public class LavaData
	{
		public int State = 1;
		public long StartTime;
		public long EndTime;
		public int Record;
		public int RemainingPlayers = 100;
		public int TempPlayer;
		public int SeasonIndex;
		public long RetryTime;
		public int PlayCount;
		public int PromptCount;
		public long PromptTime;
		public bool IsFailed;
		public bool IsInCd;
		public bool HasShownStartAnim;
		public bool HasClaimedRewards;
		public bool HasShownTutorial;
		public int WinCount;
		public int TotalFails;
		public string RewardClaimId;
		public bool HasPendingJump;
	}

	public class BaseLavaManager : BaseEventManager
	{
		public UnityEvent OnWinEvent = new UnityEvent();

		protected LavaData _data;
		protected LavaConfig _config;

#if UNITY_EDITOR
		public bool DebugHasClaimedRewards => _data != null && _data.HasClaimedRewards;
		public string DebugRewardClaimId => _data != null && _data.StartTime > 0 ? _data.RewardClaimId : null;
#endif

		private readonly List<(int min, int max)> _loseRangeList = new List<(int, int)>
		{
			(2, 5),
			(9, 12)
		};

		private readonly Vector3[] _avatarPositions = new Vector3[]
		{
			new Vector3(-42f, 12f, 0f),
			new Vector3(40f, 15f, 0f),
			new Vector3(29f, 34f, 0f),
			new Vector3(-25f, 44f, 0f),
			new Vector3(67f, 28f, 0f),
			new Vector3(-60f, 28f, 0f),
			new Vector3(36f, 57f, 0f),
			new Vector3(-45f, 66f, 0f),
			new Vector3(-1f, 87f, 0f)
		};

		public virtual int GetUnlockLevel() => 41;
		public virtual int GetPromptInterval() => 0;
		public virtual int GetCurSkin() => 2;
		public virtual int GetRetryInterval() => 900;

		public virtual void LoadConfig()
		{
			_config = new LavaConfig
			{
				Duration = 86400,
				LevelGoal = 7,
				CoinRewardCount = 10000,
				PlayerCount = 100,
				PromptInterval = 5
			};
		}

		public override void Startup()
		{
			base.Startup();
			LoadConfig();
			LoadData();
			MigrateData();

			if (!_isEventRegistered && Managers.Event != null)
			{
				Managers.Event.RegisterEvent("OnGameWin", OnGameWonAction);
				Managers.Event.RegisterEvent("OnGameFail", OnGameFailedAction);
				_isEventRegistered = true;
			}
		}

		private bool _isEventRegistered;
		private void OnGameWonAction(object args) => OnGameWon();
		private void OnGameFailedAction(object args) => OnGameFailed();

		public bool IsEnable() => true;

		public bool IsUnlocked()
		{
			return GameProgressStore.NextPlayableLevel >= GetUnlockLevel();
		}

		public bool IsWin()
		{
			if (_data == null || _config == null) return false;
			return _data.State == 2 && _data.Record >= _config.LevelGoal;
		}

		public bool IsFail()
		{
			if (_data == null) return false;
			return _data.State == 2 && _data.IsFailed;
		}

		public bool IsExpired()
		{
			if (_data == null || _data.State != 2) return false;
			return TimeUtils.GetUnixTimestamp() >= _data.EndTime;
		}

		public bool IsOngoing() => _data != null && _data.State == 2;
		public bool IsWaitingStart() => _data != null && _data.State == 1;
		public bool IsInCd() => _data != null && _data.State == 2 && _data.IsInCd;
		public bool HasShownTutorial() => _data != null && _data.HasShownTutorial;

		public bool ShouldPlayJumpAnim() => _data != null && (_data.TempPlayer > 0 || _data.HasPendingJump);
		public bool ShouldPlayStartAnim() => _data != null && !_data.HasShownStartAnim;
		public bool ShouldClaimRewards() => IsWin() && _data != null && !_data.HasClaimedRewards;

		public bool ShouldShowFailDialog()
		{
			if (_data == null || _data.State != 2) return false;
			return IsFail() && !IsInCd();
		}

		public bool ShouldShowDetailDialog()
		{
			if (_data == null || _data.State != 2 || IsInCd()) return false;
			return ShouldPlayJumpAnim() || ShouldClaimRewards() || IsExpired() || IsFail();
		}

		public long GetEndTime() => _data != null ? _data.EndTime : 0;
		public long GetRemainTime() => _data != null ? Math.Max(0, _data.EndTime - TimeUtils.GetUnixTimestamp()) : 0;
		public long GetRemainRetryTime() => _data != null ? Math.Max(0, (_data.RetryTime + GetRetryInterval()) - TimeUtils.GetUnixTimestamp()) : 0;
		public long GetRemainPromptTime() => _data != null ? Math.Max(0, (_data.PromptTime + GetPromptInterval()) - TimeUtils.GetUnixTimestamp()) : 0;
		public long GetNextRetryTime() => _data != null ? (_data.RetryTime + GetRetryInterval()) : 0;

		public int GetRecord() => _data != null ? _data.Record : 0;
		public int GetTempLosePlayers() => _data != null ? _data.TempPlayer : 0;
		public int GetRemainingPlayers() => _data != null ? _data.RemainingPlayers : GetPlayerCount();
		public int GetLevelGoal() => _config != null ? _config.LevelGoal : 7;
		public int GetPlayerCount() => _config != null ? _config.PlayerCount : 100;
		public int GetCoinRewardCount() => _config != null ? _config.CoinRewardCount : 10000;

		public int GetSharedCoinRewardCount()
		{
			if (_config == null || _data == null) return 0;
			int remaining = Math.Max(1, _data.RemainingPlayers - _data.TempPlayer);
			return (int)Math.Ceiling((float)_config.CoinRewardCount / remaining);
		}

		public override bool ShouldShowIcon()
		{
			if (!IsUnlocked()) return true;
			if (_data == null) return true;
			return _data.State == 1 || _data.State == 2;
		}

		public override async UniTask<HomeEventBase> CreateIcon(Transform parent)
		{
			GameObject prefab = LavaSettings.Instance != null ? LavaSettings.Instance.IconPrefab : null;
			if (prefab == null)
			{
				prefab = Resources.Load<GameObject>("Lava/Prefab/HomeEventLava");
			}

			if (prefab != null)
			{
				GameObject go = UnityEngine.Object.Instantiate(prefab, parent);
				return go.GetComponent<HomeEventBase>();
			}

			await UniTask.CompletedTask;
			return null;
		}

		public void OnClaimRewards()
		{
			TryClaimRewards();
		}

		public bool TryClaimRewards()
		{
			if (!ShouldClaimRewards()) return false;
			var inventory = InventoryManager.Ensure();
			if (inventory == null) return false;
			if (string.IsNullOrEmpty(_data.RewardClaimId))
			{
				_data.RewardClaimId = Guid.NewGuid().ToString("N");
				SaveData();
			}
			int reward = GetSharedCoinRewardCount();
			if (!inventory.TryClaimRewards("lava:" + _data.RewardClaimId,
				new[] { RewardItem.Coin(reward) }, out _)) return false;
			SynchronizeTempPlayer();
			OnClaimedRewards();
			return true;
		}

		public bool ShouldShowEventJoinPrompt()
		{
			if (!IsUnlocked() || _data == null) return false;
			if (_data.State != 1) return false;
			if (IsExpired()) return false;
			if (IsFail() && GetRemainRetryTime() > 0) return false;
			if (_data.HasShownTutorial && IsNearRefreshTime()) return false;

			if (_data.PromptCount == 0)
			{
				if (!_data.HasShownTutorial) return true;
			}
			else
			{
				if (_data.PromptCount > 2) return false;
				if (GetRemainPromptTime() > 0) return false;
			}

			return _config != null && _data.PlayCount >= _config.PromptInterval;
		}

		public override void EventBeforeEnterHome()
		{
			if (_data == null) return;
			if (_data.State != 2 && IsPromptNewSeason())
			{
				ResetPromptNewSeason();
			}

			if (_data.State == 7)
			{
				if (IsTimeReachNewSeason())
				{
					ResetNewSeason();
				}
			}
			else if (_data.State == 2)
			{
				if (IsWin()) return;
				if (!_data.IsFailed && !_data.IsInCd)
				{
					if (TimeUtils.IsExpired(_data.EndTime))
					{
						OnTimeOut();
					}
				}

				if (_data.IsInCd && GetRemainRetryTime() <= 0)
				{
					_data.State = 1;
					SaveData();
				}
			}
			else if (_data.State == 0 && IsUnlocked())
			{
				_data.State = 1;
				SaveData();
			}
		}

		public long GetTodayRefreshTimeStamp()
		{
			DateTime nowUtc = DateTime.UtcNow.AddHours(0);
			DateTime date = nowUtc.Date;
			if (_config != null)
			{
				date = date.AddSeconds(_config.RefreshTime);
				if (nowUtc.TimeOfDay.TotalSeconds < _config.RefreshTime)
				{
					date = date.AddDays(-1);
				}
			}
			DateTimeOffset dto = new DateTimeOffset(date.ToLocalTime());
			return dto.ToUnixTimeSeconds();
		}

		public bool IsNearRefreshTime()
		{
			long now = TimeUtils.GetUnixTimestamp();
			long refresh = GetTodayRefreshTimeStamp() + 86400;
			return (now + GetRetryInterval()) >= refresh;
		}

		public void CheckEventState()
		{
			EventBeforeEnterHome();
		}

		public bool IsTimeReachNewSeason()
		{
			if (_data == null) return false;
			long now = TimeUtils.GetUnixTimestamp();
			long refresh = GetTodayRefreshTimeStamp();
			return _data.StartTime < refresh && now >= refresh;
		}

		public bool IsPromptNewSeason()
		{
			if (_data == null) return false;
			long now = TimeUtils.GetUnixTimestamp();
			long refresh = GetTodayRefreshTimeStamp();
			return _data.PromptTime < refresh && now >= refresh;
		}

		public void StartEvent()
		{
			CheckEventState();
			if (!IsUnlocked() || !IsWaitingStart()) return;
			if (IsTimeReachNewSeason())
			{
				ResetNewSeason();
			}

			long now = TimeUtils.GetUnixTimestamp();
			if (_data == null) _data = new LavaData();

			_data.State = 2;
			_data.StartTime = now;
			_data.EndTime = now + (_config != null ? _config.Duration : 86400);
			_data.Record = 0;
			_data.RemainingPlayers = GetPlayerCount();
			_data.TempPlayer = 0;
			_data.SeasonIndex++;
			_data.HasPendingJump = false;
			_data.PlayCount = 0;
			_data.HasShownStartAnim = false;
			_data.HasClaimedRewards = false;
			_data.IsInCd = false;
			_data.IsFailed = false;
			_data.RewardClaimId = Guid.NewGuid().ToString("N");

			SaveData();
		}

		public void ResetNewSeason()
		{
			if (_data == null) _data = new LavaData();
			_data.State = 1;
			_data.StartTime = 0;
			_data.EndTime = 0;
			_data.Record = 0;
			_data.RemainingPlayers = GetPlayerCount();
			_data.TempPlayer = 0;
			_data.SeasonIndex = -1;
			_data.HasPendingJump = false;
			_data.RetryTime = 0;
			_data.PlayCount = 0;
			_data.PromptCount = 0;
			_data.PromptTime = TimeUtils.GetUnixTimestamp();
			_data.TotalFails = 0;
			_data.IsFailed = false;
			_data.IsInCd = false;
			_data.HasClaimedRewards = false;
			_data.HasShownStartAnim = false;
			SaveData();
		}

		public void ResetPromptNewSeason()
		{
			if (_data == null) return;
			_data.PromptCount = 0;
			_data.PlayCount = 0;
			_data.PromptTime = TimeUtils.GetUnixTimestamp();
			SaveData();
		}

		public void SynchronizeTempPlayer()
		{
			if (_data == null) return;
			_data.RemainingPlayers = Math.Max(1, _data.RemainingPlayers - _data.TempPlayer);
			_data.TempPlayer = 0;
			_data.HasPendingJump = false;
			SaveData();
		}

		public void OnClaimedRewards()
		{
			if (_data == null) return;
			_data.TempPlayer = 0;
			_data.HasClaimedRewards = true;
			_data.HasPendingJump = false;
			_data.Record = 0;
			if (!IsTimeReachNewSeason())
			{
				_data.State = 7;
			}
			else
			{
				ResetNewSeason();
			}
			SaveData();
		}

		public void OnStartAnimShown()
		{
			if (_data == null) return;
			_data.HasShownStartAnim = true;
			SaveData();
		}

		public void OnTutorialShown()
		{
			if (_data == null) return;
			_data.HasShownTutorial = true;
			SaveData();
		}

		public void OnShowPrompt()
		{
			if (_data == null) return;
			_data.PlayCount = 0;
			_data.PromptCount++;
			_data.PromptTime = TimeUtils.GetUnixTimestamp();
			SaveData();
		}

		public void OnTimeOutShown()
		{
			if (_data == null) return;
			_data.Record = 0;
			_data.TempPlayer = 0;
			_data.HasPendingJump = false;
			if (!IsTimeReachNewSeason())
			{
				_data.State = 7;
			}
			else
			{
				ResetNewSeason();
			}
			SaveData();
		}

		public void OnFailedShown()
		{
			if (_data == null) return;
			_data.Record = 0;
			_data.TempPlayer = 0;
			_data.HasPendingJump = false;
			if (!IsTimeReachNewSeason())
			{
				if (!IsNearRefreshTime())
				{
					_data.IsInCd = true;
				}
				else
				{
					_data.State = 7;
				}
			}
			else
			{
				ResetNewSeason();
			}
			SaveData();
		}

		public void OnGameWon()
		{
			if (!IsUnlocked()) return;
			CheckEventState();
			if (_data == null) return;

			if (_data.State == 2 && !_data.IsFailed && !IsExpired() && !IsWin())
			{
				int pending = _data.TempPlayer;
				_data.RemainingPlayers = Math.Max(1, _data.RemainingPlayers - pending);
				_data.Record++;
				int loseCount = CalculateLosePlayer();
				_data.RemainingPlayers += pending;
				_data.TempPlayer = pending + loseCount;
				_data.HasPendingJump = true;

				if (_config != null && _data.Record >= _config.LevelGoal)
				{
					AnalyticsManager.LogEvent("lava_race_win", new Dictionary<string, object>
					{
						{ "fails", _data.TotalFails }
					});
					_data.WinCount++;
					OnWinEvent?.Invoke();
				}
			}
			else if (_data.State == 1)
			{
				_data.PlayCount++;
			}
			SaveData();
		}

		public void OnGameFailed()
		{
			CheckEventState();
			if (_data == null || _data.State != 2 || _data.IsFailed || IsWin()) return;

			_data.IsFailed = true;
			_data.TempPlayer = 0;
			_data.HasPendingJump = false;
			_data.RetryTime = TimeUtils.GetUnixTimestamp();
			_data.TotalFails++;
			_data.PlayCount = 0;
			_data.PromptCount = 0;
			SaveData();

			AnalyticsManager.LogEvent("lava_race_failed", new Dictionary<string, object>
			{
				{ "levels", _data.Record },
				{ "fails", _data.TotalFails }
			});
		}

		public void OnTimeOut()
		{
			if (_data == null || _data.State != 2 || _data.IsFailed || IsWin()) return;

			_data.IsFailed = true;
			_data.TempPlayer = 0;
			_data.HasPendingJump = false;
			_data.RetryTime = TimeUtils.GetUnixTimestamp();
			_data.TotalFails++;
			_data.PlayCount = 0;
			_data.PromptCount = 0;
			SaveData();

			AnalyticsManager.LogEvent("lava_race_failed", new Dictionary<string, object>
			{
				{ "levels", _data.Record },
				{ "fails", _data.TotalFails }
			});
		}

		public string GetTitleKey()
		{
			int skin = GetCurSkin();
			string key;
			if (skin == 2)
			{
				key = "Lava-BouncyTitle";
			}
			else if (skin == 1)
			{
				key = "Lava-PillarsTitle";
			}
			else
			{
				key = "Lava-CubeTitle";
			}
			return key;
		}

		public int CalculateLosePlayer()
		{
			if (_config == null || _data == null) return 0;
			int goal = _config.LevelGoal;
			float rand = UnityEngine.Random.value;
			(int min, int max) range = rand <= 0.02f ? _loseRangeList[0] : _loseRangeList[1];
			int dropRandom = UnityEngine.Random.Range(range.min, range.max);

			int step = _data.Record;
			int target = (goal - step) * (_config.PlayerCount / Math.Max(1, goal)) + dropRandom;
			return Math.Max(0, Math.Min(_data.RemainingPlayers - 1, _data.RemainingPlayers - target));
		}

		public Vector3 GetAvatarPosition(int index)
		{
			if (index >= 0 && index < _avatarPositions.Length)
			{
				return _avatarPositions[index];
			}
			return Vector3.zero;
		}

		public void SaveData()
		{
			if (_data != null)
			{
				IoUtils.WriteJsonFile("lava_data", _data);
			}
		}

		public void LoadData()
		{
			_data = IoUtils.ReadJsonFile<LavaData>("lava_data");
			if (_data == null)
			{
				_data = new LavaData();
			}
		}

		public override void ClearData()
		{
			IoUtils.DeleteJsonFile("lava_data");
			LoadData();
		}

		public void MigrateData()
		{
			if (IoUtils.Exists("lava_status"))
			{
				IoUtils.DeleteJsonFile("lava_status");
				if (_data != null)
				{
					_data.HasShownTutorial = true;
					SaveData();
				}
			}
		}

		protected virtual void OnDestroy()
		{
			if (_isEventRegistered && Managers.Event != null)
			{
				Managers.Event.UnregisterEvent("OnGameWin", OnGameWonAction);
				Managers.Event.UnregisterEvent("OnGameFail", OnGameFailedAction);
				_isEventRegistered = false;
			}
		}
	}
}
