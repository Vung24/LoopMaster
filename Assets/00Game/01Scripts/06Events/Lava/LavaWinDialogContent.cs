using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Snui.Lava
{
	public class LavaWinDialogContent : DialogContent
	{
		[SerializeField] private AudioClip _winClip;
		[SerializeField] private TMP_Text _coinCountText;
		[SerializeField] private RewardCell _rewardCell;
		[SerializeField] private GameObject _flyCoinNode;
		[SerializeField] private RectTransform _coinNode;
		[SerializeField] private ParticleSystem _backLight;
		[SerializeField] private ParticleSystem _starShine;
		[SerializeField] private List<LavaPlayer> _players = new List<LavaPlayer>();
		[SerializeField] private int _sharedCoinCount;
		[SerializeField] private int _remainingPlayers;
		[SerializeField] private Transform _titleNode;
		[SerializeField] private Transform _desc2;
		[SerializeField] private Transform _chest;
		[SerializeField] private Transform _coinBadge;
		[SerializeField] private Transform _playerContainer;
		[SerializeField] private Transform _claimBtnNode;
		[SerializeField] private Snui_FullButton _fullButton;

		public static UniTask<LavaWinDialogContent> Show(Transform parent) => EventPrefabUI.Show<LavaWinDialogContent>(parent, "LavaWinDialog");

		public static async UniTask ShowWithDialog(Transform parent)
		{
			var dialog = await Show(parent);
			await UniTask.WaitUntil(() => dialog == null || dialog.Result != RESULT_UNKNOWN);
		}

		protected override void Awake()
		{
			base.Awake();
			if (Managers.Lava != null)
			{
				_sharedCoinCount = Managers.Lava.GetSharedCoinRewardCount();
				_remainingPlayers = Mathf.Max(1, Managers.Lava.GetRemainingPlayers() - Managers.Lava.GetTempLosePlayers());
				if (_rewardCell != null) _rewardCell.Init(RewardItem.Coin(_sharedCoinCount), false, true).Forget();
			}

			if (_desc2 == null)
			{
				_desc2 = HierarchyReferenceCache.Child(transform, "Desc2");
			}
			if (_desc2 != null)
			{
				TMP_Text descText = _desc2.GetComponent<TMP_Text>();
				if (descText != null) descText.text = $"Your share of the prize with {_remainingPlayers} surviving players.";
			}

			Transform coinCountNode = HierarchyReferenceCache.Child(transform, "Coin/Count");
			if (coinCountNode != null && _coinCountText == null)
			{
				_coinCountText = coinCountNode.GetComponent<TMP_Text>();
			}

			if (_fullButton == null)
			{
				_fullButton = HierarchyReferenceCache.ChildComponent<Snui_FullButton>(transform, "FullButton");
			}
			if (_fullButton != null)
				foreach (var graphic in GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
					graphic.raycastTarget = graphic.transform.IsChildOf(_fullButton.transform);

			Transform flyNode = HierarchyReferenceCache.Child(transform, "FlyCoinNode");
			if (flyNode != null && _flyCoinNode == null)
			{
				_flyCoinNode = flyNode.gameObject;
			}

			Transform coinNodeTrans = HierarchyReferenceCache.Child(transform, "CoinNode");
			if (coinNodeTrans != null && _coinNode == null)
			{
				_coinNode = coinNodeTrans.GetComponent<RectTransform>();
			}

			Transform backLightTrans = HierarchyReferenceCache.Child(transform, "BackLightMask/BackLight");
			if (backLightTrans != null && _backLight == null)
			{
				_backLight = backLightTrans.GetComponent<ParticleSystem>();
			}

			Transform starShineTrans = HierarchyReferenceCache.Child(transform, "CoinLarge/StarShine");
			if (starShineTrans != null && _starShine == null)
			{
				_starShine = starShineTrans.GetComponent<ParticleSystem>();
			}

			if (_coinCountText != null)
			{
				_coinCountText.text = "0";
			}
			if (_flyCoinNode != null)
			{
				_flyCoinNode.SetActive(false);
			}
			if (_claimBtnNode != null)
			{
				_claimBtnNode.gameObject.SetActive(false);
			}
			if (_backLight != null)
			{
				_backLight.gameObject.SetActive(false);
			}
			if (_starShine != null)
			{
				_starShine.gameObject.SetActive(false);
			}

			CreatePlayers().Forget();
		}

		protected override void Start()
		{
			base.Start();

			if (_winClip != null && GameAudioPlayer.Current != null)
			{
				GameAudioPlayer.Current.PlayOneShot(_winClip);
			}

			if (_fullButton != null)
			{
				_fullButton.DisableButton();
				Transform content = _fullButton.Content;
				if (content != null)
				{
					content.localScale = Vector3.zero;
				}
				_fullButton.Button?.onClick.AddListener(OnClickFullBtn);
			}

			SetUp();
			Play();
			PlayAnimation().Forget();
		}

		private void SetUp()
		{
			if (_titleNode != null) _titleNode.localScale = Vector3.zero;
			if (_desc2 != null) _desc2.localScale = Vector3.zero;
			if (_chest != null) _chest.localScale = Vector3.zero;
			if (_coinBadge != null) _coinBadge.localScale = Vector3.zero;
			if (_playerContainer != null) _playerContainer.localScale = Vector3.zero;
			if (_claimBtnNode != null) _claimBtnNode.localScale = Vector3.zero;
		}

		private Sequence Play()
		{
			Sequence seq = DOTween.Sequence();
			if (_titleNode != null)
			{
				seq.Append(_titleNode.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
			}
			if (_desc2 != null)
			{
				seq.Append(_desc2.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
			}
			if (_chest != null)
			{
				seq.Append(_chest.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
			}
			if (_coinBadge != null)
			{
				seq.Append(_coinBadge.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
			}
			if (_playerContainer != null)
			{
				seq.Append(_playerContainer.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
			}
			if (_claimBtnNode != null)
			{
				_claimBtnNode.gameObject.SetActive(true);
				seq.Append(_claimBtnNode.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
			}
			seq.SetLink(gameObject);
			return seq;
		}

		private async UniTaskVoid PlayAnimation()
		{
			await CreatePlayers();
			await ShowPlayers();
			await PlayFlyingCoin();

			if (_backLight != null)
			{
				_backLight.gameObject.SetActive(true);
				_backLight.Play();
			}
			if (_starShine != null)
			{
				_starShine.gameObject.SetActive(true);
				_starShine.Play();
			}

			if (_fullButton != null)
			{
				Transform content = _fullButton.Content;
				if (content != null)
				{
					content.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
					await UniTask.Delay(300, cancellationToken: this.GetCancellationTokenOnDestroy());
				}
				_fullButton.EnableButton();
			}
		}

		private async UniTask CreatePlayers()
		{
			Transform template = HierarchyReferenceCache.Child(transform, "Player");
			if (_playerContainer != null && template != null && _players.Count == 0)
			{
				var layout = _playerContainer.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
				if (layout != null) layout.enabled = false;
				if (_playerContainer is RectTransform row)
					row.anchoredPosition = new Vector2(row.anchoredPosition.x, -460f);
				int count = Mathf.Min(5, Mathf.Max(0, _remainingPlayers - 1));
				float spacing = count > 1 ? Mathf.Min(120f, 560f / (count - 1)) : 0f;
				for (int i = 0; i < count; i++)
				{
					var node = Instantiate(template.gameObject, _playerContainer, false);
					var player = node.GetOrAddComponent<LavaPlayer>();
					player.IsUser = false;
					LavaPlayer.SetDisplayAvatar(player.transform, i + 1);
					player.Avatar?.SetAvatar(new Snui.Profile.PlayerInfo { Avatar = "avatar_" + (i + 2), AvatarFrame = "default" });
					node.transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * spacing, 0f, 0f);
					node.SetActive(false);
					_players.Add(player);
				}
				LavaPlayer.SetDisplayAvatar(template, 0);
				template.gameObject.SetActive(true);
			}
			await UniTask.CompletedTask;
		}

		private async UniTask ShowPlayers()
		{
			for (int i = 0; i < _players.Count; i++)
			{
				LavaPlayer p = _players[i];
				if (p != null)
				{
					p.gameObject.SetActive(true);
					p.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetLink(p.gameObject);
					await UniTask.Delay(100, cancellationToken: this.GetCancellationTokenOnDestroy());
				}
			}
		}

		private async UniTask PlayFlyingCoin()
		{
			if (_flyCoinNode != null)
			{
				_flyCoinNode.SetActive(true);
			}

			int current = 0;
			DOTween.To(() => current, x =>
			{
				current = x;
				if (_coinCountText != null)
				{
					_coinCountText.text = current.ToString();
				}
			}, _sharedCoinCount, 0.8f).SetLink(gameObject);

			await UniTask.Delay(800, cancellationToken: this.GetCancellationTokenOnDestroy());
		}

		private void DestroyAllParticles()
		{
			if (_backLight != null)
			{
				Destroy(_backLight.gameObject);
			}
			if (_starShine != null)
			{
				Destroy(_starShine.gameObject);
			}
		}

		private bool _claiming;
		private void OnClickFullBtn()
		{
			if (_claiming) return;
			_claiming = true;
			ClaimAsync().Forget();
		}

		private async UniTaskVoid ClaimAsync()
		{
			if (Managers.Lava == null || !Managers.Lava.TryClaimRewards())
			{
				_claiming = false;
				return;
			}
			DOHideButton();
			DestroyAllParticles();
			await UniTask.Delay(300, cancellationToken: this.GetCancellationTokenOnDestroy());
			Dismiss();
		}

		private void DOHideButton()
		{
			if (_fullButton != null)
			{
				_fullButton.DisableButton();
				Transform content = _fullButton.Content;
				if (content != null)
				{
					content.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetLink(gameObject);
				}
			}
		}
	}
}
