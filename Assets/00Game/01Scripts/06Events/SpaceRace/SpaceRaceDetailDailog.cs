using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.SpaceRace
{
	public class SpaceRaceDetailDailog : MonoBehaviour
	{
		[SerializeField] private AudioClip _bgm;
		[SerializeField] private SpaceDetailCard _selfCardPrefab;
		[SerializeField] private SpaceDetailCard _opponentCardPrefab;
		[SerializeField] private Button _infoBtn;
		[SerializeField] private Button _closeBtn;
		[SerializeField] private Transform _cardParent;
		[SerializeField] private TMP_Text _stageValueText;
		[SerializeField] private SkeletonGraphic _catSpine;
		[SerializeField] private Timer _timer;
		[SerializeField] private Button _playBtn;
		[SerializeField] private CanvasGroup _canvasGroup;
		[SerializeField] private Transform _endNode;
		[SerializeField] private RectTransform _failNode;
		[SerializeField] private RectTransform _winNode;
		[SerializeField] private SkeletonGraphic _chestSpine;

		private CancellationToken _destroyToken;

		public static UniTask<SpaceRaceDetailDailog> Show(Transform parent = null) => EventPrefabUI.Show<SpaceRaceDetailDailog>(parent, "SpaceRaceDetailDailog");

		private void Awake()
		{
			_destroyToken = this.GetCancellationTokenOnDestroy();

			if (_cardParent == null)
			{
				_cardParent = HierarchyReferenceCache.Path(transform, "Bg", "BottomBg", "CardParent");
			}
			if (_stageValueText == null)
			{
				_stageValueText = HierarchyReferenceCache.PathComponent<TMP_Text>(transform, "SafeArea", "TitleBg", "Stage", "Value");
			}
			if (_catSpine == null)
			{
				_catSpine = HierarchyReferenceCache.PathComponent<SkeletonGraphic>(transform, "Bg", "TopBg", "Cat");
			}
			if (_timer == null)
			{
				_timer = HierarchyReferenceCache.PathComponent<Timer>(transform, "Bg", "BottomBg", "Timer");
			}
			if (_playBtn == null)
			{
				_playBtn = HierarchyReferenceCache.PathComponent<Button>(transform, "SafeArea", "PlayBtn");
			}
			if (_endNode == null)
			{
				_endNode = HierarchyReferenceCache.Path(transform, "Bg", "TopBg", "End");
			}
			if (_endNode != null)
			{
				if (_failNode == null)
				{
					_failNode = HierarchyReferenceCache.ChildComponent<RectTransform>(_endNode, "Fail");
				}
				if (_winNode == null)
				{
					_winNode = HierarchyReferenceCache.ChildComponent<RectTransform>(_endNode, "Win");
				}
				if (_winNode != null && _chestSpine == null)
				{
					_chestSpine = HierarchyReferenceCache.ChildComponent<SkeletonGraphic>(_winNode.transform, "Chest");
				}
			}
			if (_canvasGroup == null)
			{
				_canvasGroup = GetComponent<CanvasGroup>();
			}

			if (Managers.SpaceRace != null)
			{
				Managers.SpaceRace.RefreshRobots();
			}

			if (_endNode != null)
			{
				_endNode.gameObject.SetActive(false);
			}
		}

		private void Start()
		{
			if (_infoBtn == null)
			{
				_infoBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "InfoBtn");
			}
			if (_closeBtn == null)
			{
				_closeBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseBtn");
			}

			if (_infoBtn != null)
			{
				_infoBtn.onClick.AddListener(ShowInfoDialog);
			}
			if (_closeBtn != null)
			{
				_closeBtn.onClick.AddListener(() => OnCloseBtnClick(false).Forget());
			}
			if (_playBtn != null)
			{
				_playBtn.onClick.AddListener(() => OnCloseBtnClick(true).Forget());
			}

			InitUi();
			CreateCard();
			StartAsync().Forget();

			if (_bgm != null && AudioManager.Current != null)
			{
				AudioManager.Current.PlayOneShot(_bgm);
			}
		}

		public void InitUi()
		{
			if (Managers.SpaceRace == null) return;

			if (_stageValueText != null)
			{
				int stageNum = Managers.SpaceRace.WinCount + 1;
				_stageValueText.text = $"Stage {stageNum}";
			}

			if (_catSpine != null && _catSpine.AnimationState != null)
			{
				string anim = $"stage{Managers.SpaceRace.Stage + 1}";
				_catSpine.AnimationState.SetAnimation(0, anim, true);
			}

			if (_timer != null)
			{
				CountdownHelper.CountdownUntil(_timer, Managers.SpaceRace.StageEndTime);
			}
		}

		public void CreateCard()
		{
			if (Managers.SpaceRace == null || _cardParent == null) return;

			var opponents = Managers.SpaceRace.Opponents;
			int myRank = Managers.SpaceRace.Rank;
			int totalCount = (opponents != null ? opponents.Count : 0) + 1;

			int opponentIdx = 0;
			float yPos = -22f;

			for (int rank = 1; rank <= totalCount; rank++)
			{
				SpaceDetailCard card = null;
				if (rank == myRank)
				{
					if (_selfCardPrefab != null)
					{
						card = Instantiate(_selfCardPrefab, _cardParent);
						card.InitSelfCard();
					}
				}
				else if (opponents != null && opponentIdx < opponents.Count)
				{
					if (_opponentCardPrefab != null)
					{
						card = Instantiate(_opponentCardPrefab, _cardParent);
						card.InitOpponentCard(opponents[opponentIdx]);
					}
					opponentIdx++;
				}

				if (card != null)
				{
					if (card.transform is RectTransform rt)
					{
						rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f);
						rt.pivot = new Vector2(.5f, 1f);
						rt.anchoredPosition = new Vector2(0f, yPos);
					}
					card.PlayEnterAnimation(rank * 0.05f);
				}

				yPos -= 122f;
			}
		}

		public async UniTaskVoid StartAsync()
		{
			await UniTask.Yield();
			if (Managers.SpaceRace == null) return;

			if (Managers.SpaceRace.IsWin)
			{
				await InitWinUI();
			}
			else if (Managers.SpaceRace.IsFail)
			{
				await InitFailUI();
			}
			else
			{
				await ShowTutorialIfNeed();
			}
		}

		public async UniTask InitWinUI()
		{
			if (_endNode != null) _endNode.gameObject.SetActive(true);
			if (_winNode != null) _winNode.gameObject.SetActive(true);
			if (_failNode != null) _failNode.gameObject.SetActive(false);

			if (_chestSpine != null)
			{
				var slotName = SpaceDetailCard.GetChestSlot();
				if (_chestSpine.Skeleton != null)
				{
					_chestSpine.Skeleton.SetAttachment("chest", slotName);
				}
				if (_chestSpine.AnimationState != null)
				{
					_chestSpine.AnimationState.SetAnimation(0, "open", false);
				}
			}

			await PlayWinAnimation();
		}

		public async UniTask InitFailUI()
		{
			if (_endNode != null) _endNode.gameObject.SetActive(true);
			if (_winNode != null) _winNode.gameObject.SetActive(false);
			if (_failNode != null) _failNode.gameObject.SetActive(true);
			await UniTask.Yield();
		}

		public async UniTask PlayWinAnimation()
		{
			await UniTask.Delay(1000, cancellationToken: _destroyToken);
		}

		public Tweener Hide()
		{
			if (_canvasGroup != null)
			{
				return _canvasGroup.DOFade(0f, 0.2f).SetLink(gameObject);
			}
			return null;
		}

		public void ShowInfoDialog()
		{
			var canvas = Utils.GetCurrentUiCanvas();
			var prefab = SpaceRaceSettings.Instance != null ? SpaceRaceSettings.Instance.InfoDialogPrefab : null;
			if (prefab != null)
			{
				EventPrefabUI.Show<EventInfoDialog>(canvas != null ? canvas.transform : transform.parent, "SpaceInfoDialog").Forget();
			}
		}

		public async UniTask ShowTutorialIfNeed()
		{
			await UniTask.Yield();
		}

		public async UniTaskVoid OnCloseBtnClick(bool isPlay)
		{
			var tw = Hide();
			if (tw != null)
			{
				await tw.ToUniTask();
			}

			if (Managers.SpaceRace != null && (Managers.SpaceRace.IsWin || Managers.SpaceRace.IsFail))
			{
				Managers.SpaceRace.CloseRace();
			}

			Destroy(gameObject);
		}
	}
}