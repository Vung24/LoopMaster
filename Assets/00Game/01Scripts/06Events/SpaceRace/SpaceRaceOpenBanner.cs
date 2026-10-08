using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.SpaceRace
{
	public class SpaceRaceOpenBanner : MonoBehaviour
	{
		[SerializeField] private Timer _timer;
		[SerializeField] private Button _startBtn;
		[SerializeField] private TMP_Text _stageText;
		[SerializeField] private CanvasGroup _canvasGroup;
		[SerializeField] private bool _shouldHideOtherContent;

		public static async UniTask<GameObject> Show(Transform parent)
		{
			await UniTask.Yield();
			var prefab = EventPrefabUI.Load("SpaceRaceOpenBanner");
			if (prefab != null && parent != null)
			{
				return Instantiate(prefab, parent);
			}
			return null;
		}

		public void SetShouldHideOtherContent(bool hide)
		{
			_shouldHideOtherContent = hide;
		}

		private void Awake()
		{
			if (_stageText == null)
			{
				_stageText = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Stage");
			}
			if (_canvasGroup == null)
			{
				_canvasGroup = GetComponent<CanvasGroup>();
			}
			if (_timer == null)
			{
				_timer = GetComponentInChildren<Timer>(true);
			}
			if (_startBtn == null)
			{
				_startBtn = GetComponentInChildren<Button>(true);
			}
		}

		private void Start()
		{
			if (Managers.SpaceRace == null) return;

			if (_timer != null)
			{
				CountdownHelper.CountdownUntil(_timer, Managers.SpaceRace.SessionEndTime);
			}

			if (_startBtn != null)
			{
				_startBtn.onClick.AddListener(OnStartClick);
			}

			if (Managers.SpaceRace.InitialRewardClaimed)
			{
				HideInitialReward();
			}

			if (_stageText != null)
			{
				int stageNum = Managers.SpaceRace.WinCount + 1;
				_stageText.text = $"Stage {stageNum}";
			}
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
				var text = HierarchyReferenceCache.PathComponent<TMP_Text>(_startBtn.transform, "Content", "Snui_Text");
				if (text != null && text.transform is RectTransform rt)
				{
					rt.anchoredPosition = Vector2.zero;
				}
			}
		}

		public void OnStartClick()
		{
			StartRaceAsync().Forget();
		}

		public async UniTaskVoid StartRaceAsync()
		{
			if (Managers.SpaceRace == null) return;

			bool success = await Managers.SpaceRace.StartRace();
			if (success)
			{
				await SpaceRaceDetailDailog.Show();
				Destroy(gameObject);
			}
		}

		public void FadeOut()
		{
			if (_canvasGroup != null)
			{
				_canvasGroup.DOFade(0f, 0.2f).SetLink(gameObject);
			}
		}

		public void FadeIn()
		{
			if (_canvasGroup != null)
			{
				_canvasGroup.DOFade(1f, 0.2f).SetLink(gameObject);
			}
		}
	}
}