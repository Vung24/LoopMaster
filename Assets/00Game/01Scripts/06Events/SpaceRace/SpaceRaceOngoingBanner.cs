using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui.SpaceRace
{
	public class SpaceRaceOngoingBanner : MonoBehaviour
	{
		[SerializeField] private SpaceBannerCard _selfCardPrefab;
		[SerializeField] private SpaceBannerCard _opponentCardPrefab;
		[SerializeField] private Timer _timer;
		[SerializeField] private Transform _playerParent;

		public static async UniTask<GameObject> Show(Transform parent)
		{
			await UniTask.Yield();
			var prefab = EventPrefabUI.Load("SpaceRaceOngoingBanner");
			if (prefab != null && parent != null)
			{
				return Instantiate(prefab, parent);
			}
			return null;
		}

		private void Awake()
		{
			if (_playerParent == null)
			{
				_playerParent = HierarchyReferenceCache.Child(transform, "Player");
			}
			if (_timer == null)
			{
				_timer = GetComponentInChildren<Timer>(true);
			}
			if (Managers.SpaceRace != null)
			{
				Managers.SpaceRace.RefreshRobots();
			}
		}

		private void Start()
		{
			if (Managers.SpaceRace != null && _timer != null)
			{
				CountdownHelper.CountdownUntil(_timer, Managers.SpaceRace.StageEndTime);
			}
			CreateCard().Forget();
		}

		private async UniTaskVoid CreateCard()
		{
			await UniTask.Yield();
			if (Managers.SpaceRace == null || _playerParent == null) return;

			var opponents = Managers.SpaceRace.Opponents;
			int myRank = Managers.SpaceRace.Rank;

			int opponentIdx = 0;
			int totalSlots = (opponents != null ? opponents.Count : 0) + 1;

			for (int rank = 1; rank <= totalSlots; rank++)
			{
				if (rank == myRank)
				{
					if (_selfCardPrefab != null)
					{
						var card = Instantiate(_selfCardPrefab, _playerParent);
						card.InitSelfCard();
					}
				}
				else if (opponents != null && opponentIdx < opponents.Count)
				{
					if (_opponentCardPrefab != null)
					{
						var card = Instantiate(_opponentCardPrefab, _playerParent);
						card.InitOpponentCard(opponents[opponentIdx]);
					}
					opponentIdx++;
				}
			}
		}
	}
}