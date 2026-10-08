using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Snui.Profile;

namespace Snui.SpaceRace
{
	public class SpaceDetailCard : MonoBehaviour
	{
		[SerializeField] private Sprite _rank1Sprite;
		[SerializeField] private Sprite _rank2Sprite;
		[SerializeField] private Sprite _rankOtherSprite;
		[SerializeField] private Image _rankBg;
		[SerializeField] private TMP_Text _rankText;
		[SerializeField] private PlayerInfoWidget _playerInfo;
		[SerializeField] private SkeletonGraphic _chest;
		[SerializeField] private TMP_Text _scoreText;
		[SerializeField] private Button _chestBtn;
		[SerializeField] private Transform _toastPos;
		[SerializeField] private CanvasGroup _canvasGroup;

		public SkeletonGraphic Chest => _chest;

		private void Awake()
		{
			if (_rankBg == null)
			{
				_rankBg = HierarchyReferenceCache.ChildComponent<Image>(transform, "RankBg");
			}
			if (_rankText == null)
			{
				_rankText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Rank");
			}
			if (_playerInfo == null)
			{
				_playerInfo = HierarchyReferenceCache.ChildComponent<PlayerInfoWidget>(transform, "PlayerInfo");
			}
			if (_chest == null)
			{
				_chest = HierarchyReferenceCache.ChildComponent<SkeletonGraphic>(transform, "Chest");
			}
			if (_scoreText == null)
			{
				_scoreText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Record");
			}
			if (_chestBtn == null && _chest != null)
			{
				_chestBtn = HierarchyReferenceCache.ChildComponent<Button>(_chest.transform, "Button");
			}
			if (_toastPos == null && _chest != null)
			{
				_toastPos = HierarchyReferenceCache.Child(_chest.transform, "ToastPos");
			}
			if (_canvasGroup == null)
			{
				_canvasGroup = GetComponent<CanvasGroup>();
			}
		}

		private void Start()
		{
			if (_chestBtn != null)
			{
				_chestBtn.onClick.AddListener(OnChestBtnClicked);
			}
		}

		public void InitSelfCard()
		{
			if (Managers.SpaceRace == null) return;

			int rank = Managers.SpaceRace.Rank;
			if (rank == 1)
			{
				if (_rankBg != null)
				{
					_rankBg.sprite = _rank1Sprite;
					_rankBg.transform.localScale = Vector3.one * 0.95f;
				}
				if (_chest != null)
				{
					_chest.gameObject.SetActive(true);
					var slotName = GetChestSlot();
					if (_chest.Skeleton != null)
					{
						_chest.Skeleton.SetAttachment("chest", slotName);
					}
					if (_chest.AnimationState != null)
					{
						_chest.AnimationState.SetAnimation(0, "static", false);
					}
				}
				if (_rankText != null)
				{
					_rankText.gameObject.SetActive(_rankBg == null || _rank1Sprite == null);
					_rankText.text = "1";
				}
				if (_playerInfo != null && _playerInfo.transform is RectTransform prt)
				{
					prt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 340f);
				}
			}
			else
			{
				if (_rankBg != null)
				{
					_rankBg.sprite = _rankOtherSprite;
				}
				if (_chest != null)
				{
					_chest.gameObject.SetActive(false);
				}
				if (_rankText != null)
				{
					_rankText.gameObject.SetActive(true);
					_rankText.text = rank.ToString();
				}
				if (_playerInfo != null && _playerInfo.transform is RectTransform prt)
				{
					prt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 440f);
				}
			}

			var userInfo = AccountManager.GetDisplayUserInfo();
			if (_playerInfo != null && userInfo != null)
			{
				_playerInfo.BindData(userInfo);
			}
			if (_playerInfo != null && (userInfo == null || string.IsNullOrEmpty(userInfo.Name))) _playerInfo.Init(EventRaceOpponent.GetSelf());
			if (_scoreText != null)
			{
				_scoreText.text = Managers.SpaceRace.Score.ToString();
			}
		}

		public void InitOpponentCard(EventRaceOpponent opponent)
		{
			if (opponent == null) return;

			if (opponent.Rank == 1)
			{
				if (_rankBg != null)
				{
					_rankBg.sprite = _rank1Sprite;
					_rankBg.transform.localScale = Vector3.one * 0.95f;
				}
				if (_chest != null)
				{
					_chest.gameObject.SetActive(true);
					var slotName = GetChestSlot();
					if (_chest.Skeleton != null)
					{
						_chest.Skeleton.SetAttachment("chest", slotName);
					}
					if (_chest.AnimationState != null)
					{
						_chest.AnimationState.SetAnimation(0, "static", false);
					}
				}
				if (_rankText != null)
				{
					_rankText.gameObject.SetActive(_rankBg == null || _rank1Sprite == null);
					_rankText.text = "1";
				}
				if (_playerInfo != null && _playerInfo.transform is RectTransform prt)
				{
					prt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 340f);
				}
			}
			else
			{
				if (_rankBg != null)
				{
					_rankBg.sprite = _rankOtherSprite;
				}
				if (_chest != null)
				{
					_chest.gameObject.SetActive(false);
				}
				if (_rankText != null)
				{
					_rankText.gameObject.SetActive(true);
					_rankText.text = opponent.Rank.ToString();
				}
				if (_playerInfo != null && _playerInfo.transform is RectTransform prt)
				{
					prt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 440f);
				}
			}

			if (_playerInfo != null)
			{
				_playerInfo.BindData(opponent);
			}
			if (_scoreText != null)
			{
				_scoreText.text = opponent.Score.ToString();
			}
		}

		public static string GetChestSlot()
		{
			if (Managers.SpaceRace == null) return "icon_chest_front_03";
			int stage = Managers.SpaceRace.Stage;
			if (stage == 1) return "icon_chest_front_04";
			if (stage == 2) return "icon_chest_front_05";
			return "icon_chest_front_03";
		}

		public Sequence PlayEnterAnimation(float delay)
		{
			var rt = transform as RectTransform;
			if (rt == null) return null;

			rt.anchoredPosition = new Vector2(70f, rt.anchoredPosition.y);
			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = 0f;
			}

			var seq = DOTween.Sequence();
			if (_canvasGroup != null)
			{
				seq.Append(_canvasGroup.DOFade(1f, 0.2f));
			}
			seq.Join(rt.DOAnchorPosX(-15f, 0.2f));
			seq.Append(rt.DOAnchorPosX(5f, 0.2f));
			seq.Append(rt.DOAnchorPosX(0f, 0.15f));
			seq.SetDelay(delay);
			seq.SetLink(gameObject);
			return seq;
		}

		public void OnChestBtnClicked()
		{
			if (Managers.SpaceRace == null) return;
			var rewards = Managers.SpaceRace.GetStageRewards(Managers.SpaceRace.Stage);
			var targetPos = _toastPos != null ? _toastPos.position : transform.position;
			RewardDetailToast.Show(rewards, transform.parent, targetPos).Forget();
		}
	}
}