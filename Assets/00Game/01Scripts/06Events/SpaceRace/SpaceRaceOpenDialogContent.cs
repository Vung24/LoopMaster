using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.SpaceRace
{
	public class SpaceRaceOpenDialogContent : DialogContent
	{
		[SerializeField] private Button _startBtn;
		[SerializeField] private Timer _timer;
		[SerializeField] private AudioClip _openSound;
		[SerializeField] private SpaceOpenStage[] _stages;

		private CancellationToken _cancellationToken;

		public static UniTask<SpaceRaceOpenDialogContent> Show(Transform parent) => EventPrefabUI.Show<SpaceRaceOpenDialogContent>(parent, "SpaceRaceOpenDialogContent");

		protected override void Start()
		{
			base.Start();
			_cancellationToken = this.GetCancellationTokenOnDestroy();

			if (Managers.SpaceRace != null)
			{
				Managers.SpaceRace.OnShowPrompt();
			}

			if (_startBtn == null)
			{
				_startBtn = HierarchyReferenceCache.PathComponent<Button>(transform, "SafeArea", "PlayBtn");
			}
			if (_closeBtn == null)
			{
				_closeBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseBtn");
			}
			if (_timer == null)
			{
				_timer = GetComponentInChildren<Timer>(true);
			}

			if (_startBtn != null)
			{
				_startBtn.onClick.AddListener(OnStartClicked);
			}
			if (_closeBtn != null)
			{
				_closeBtn.onClick.AddListener(OnCloseClicked);
			}

			if (Managers.SpaceRace != null && _timer != null)
			{
				CountdownHelper.CountdownUntil(_timer, Managers.SpaceRace.SessionEndTime);
			}

			if (Managers.SpaceRace != null && Managers.SpaceRace.InitialRewardClaimed)
			{
				HideInitialReward();
			}

			if (_openSound != null && AudioManager.Current != null)
			{
				AudioManager.Current.PlayOneShot(_openSound);
			}

			if (_stages == null || _stages.Length == 0)
			{
				_stages = GetComponentsInChildren<SpaceOpenStage>(true);
			}
			if (_stages != null)
			{
				foreach (var stg in _stages)
				{
					if (stg != null) stg.Init();
				}
			}

			RebuildBtnLayout().Forget();
		}

		public void HideInitialReward()
		{
			if (_startBtn != null)
			{
				var heart = HierarchyReferenceCache.Path(_startBtn.transform, "Content", "Heart");
				if (heart != null)
				{
					heart.gameObject.SetActive(false);
				}
				var text = HierarchyReferenceCache.PathComponent<Text>(_startBtn.transform, "Content", "Snui_Text");
				if (text != null && text.transform is RectTransform rt)
				{
					rt.anchoredPosition = Vector2.zero;
				}
			}
		}

		public void OnStartClicked()
		{
			StartRaceAsync().Forget();
		}

		public async UniTaskVoid StartRaceAsync()
		{
			if (Managers.SpaceRace == null) return;

			bool success = await Managers.SpaceRace.StartRace();
			if (success)
			{
				Dismiss();
				await SpaceRaceDetailDailog.Show();
			}
		}

		public void OnCloseClicked()
		{
			Dismiss();
		}

		public async UniTaskVoid RebuildBtnLayout()
		{
			await UniTask.Yield();
			if (_startBtn != null && _startBtn.transform is RectTransform rt)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
			}
		}

		public async UniTaskVoid ShowTutorial()
		{
			await UniTask.Yield();
		}
	}
}