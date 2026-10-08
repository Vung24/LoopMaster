using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Snui.Lava
{
	public class LavaStartDialogContent : DialogContent
	{
		[SerializeField] private Transform _titleNode;
		[SerializeField] private Transform _treasureNode;
		[SerializeField] private Transform _contentNode;
		[SerializeField] private Transform _playerNode;
		[SerializeField] private ParticleSystem _lavaSpark;
		[SerializeField] private TMP_Text _playerCountText;
		[SerializeField] private Snui_FullButton _fullButton;

		public static UniTask<LavaStartDialogContent> Show(Transform parent) => EventPrefabUI.Show<LavaStartDialogContent>(parent, "LavaStartDialog");



		public static async UniTask ShowWithDialog(Transform parent)
		{
			await Show(parent);
		}

		protected override void Awake()
		{
			base.Awake();
			if (_titleNode == null)
			{
				_titleNode = HierarchyReferenceCache.Child(transform, "Title");
			}
			if (_treasureNode == null)
			{
				_treasureNode = HierarchyReferenceCache.Child(transform, "TreasureNode");
			}
			if (_contentNode == null)
			{
				_contentNode = HierarchyReferenceCache.Child(transform, "ContentNode");
			}
			if (_playerNode == null)
			{
				_playerNode = HierarchyReferenceCache.Child(transform, "PlayerNode");
			}
			if (_contentNode != null)
			{
				if (_lavaSpark == null)
				{
					_lavaSpark = HierarchyReferenceCache.ChildComponent<ParticleSystem>(_contentNode, "LavaSpark");
				}
				if (_playerCountText == null)
				{
					_playerCountText = HierarchyReferenceCache.ChildComponent<TMP_Text>(_contentNode, "PlayerCount");
				}
			}
			if (_fullButton == null)
			{
				_fullButton = HierarchyReferenceCache.ChildComponent<Snui_FullButton>(transform, "FullButton");
			}
		}

		protected override void Start()
		{
			base.Start();

			if (_fullButton != null && _fullButton.Button != null)
			{
				_fullButton.Button.onClick.AddListener(() => Dismiss());
				_fullButton.DisableButton();
			}

			if (_playerCountText != null && Managers.Lava != null)
			{
				_playerCountText.text = $"0/{Managers.Lava.GetPlayerCount()}";
			}

			if (_playerNode != null)
			{
				for (int i = 0; i < _playerNode.childCount; i++)
				{
					_playerNode.GetChild(i).gameObject.SetActive(false);
				}
			}

			SetInitState();
			PlayAnimation().Forget();
		}

		private void SetInitState()
		{
			if (_titleNode != null)
			{
				_titleNode.localScale = Vector3.zero;
			}
		}

		private async UniTaskVoid PlayAnimation()
		{
			if (_titleNode != null)
			{
				_titleNode.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetLink(gameObject);
			}

			if (_lavaSpark != null)
			{
				_lavaSpark.Play();
			}

			await UniTask.Delay(300, cancellationToken: this.GetCancellationTokenOnDestroy());

			await ShowPlayers();

			ShowPlayerCountIncreaseAnim();

			if (Managers.Lava != null)
			{
				Managers.Lava.OnStartAnimShown();
			}

			if (_fullButton != null)
			{
				_fullButton.EnableButton();
			}
		}

		private async UniTask ShowPlayers()
		{
			if (_playerNode == null) return;

			int childCount = _playerNode.childCount;
			for (int i = 0; i < childCount; i++)
			{
				Transform child = _playerNode.GetChild(i);
				ShowOnePlayer(child);
				await UniTask.Delay(150, cancellationToken: this.GetCancellationTokenOnDestroy());
			}
		}

		private void ShowOnePlayer(Transform player)
		{
			if (player == null) return;
			player.gameObject.SetActive(true);
			Vector3 origScale = player.localScale;
			player.localScale = Vector3.zero;

			Sequence seq = DOTween.Sequence();
			seq.Append(player.DOScale(origScale * 1.15f, 0.15f).SetEase(Ease.OutBack));
			seq.Append(player.DOScale(origScale * 0.95f, 0.08f).SetEase(Ease.InOutSine));
			seq.Append(player.DOScale(origScale, 0.12f).SetEase(Ease.OutSine));

			Sequence moveSeq = DOTween.Sequence();
			moveSeq.Append(player.DOLocalMoveY(player.localPosition.y + 5f, 0.15f).SetEase(Ease.OutBack));
			moveSeq.Append(player.DOLocalMoveY(player.localPosition.y - 3f, 0.08f).SetEase(Ease.InOutSine));
			moveSeq.Append(player.DOLocalMoveY(player.localPosition.y, 0.12f).SetEase(Ease.OutSine));

			seq.Insert(0f, moveSeq);
			seq.SetLink(player.gameObject);
		}

		private void ShowPlayerCountIncreaseAnim()
		{
			if (_playerCountText == null || Managers.Lava == null) return;
			int targetCount = Managers.Lava.GetPlayerCount();
			int current = 0;

			DOTween.To(() => current, x =>
			{
				current = x;
				if (_playerCountText != null)
				{
					_playerCountText.text = $"{current}/{targetCount}";
				}
			}, targetCount, 0.7f).SetLink(_playerCountText.gameObject);

			Transform textTrans = _playerCountText.transform;
			Sequence punchSeq = DOTween.Sequence();
			punchSeq.Append(textTrans.DOScale(1.1f, 0.1f).SetEase(Ease.OutSine));
			punchSeq.Append(textTrans.DOScale(1f, 0.1f).SetEase(Ease.InSine));
			punchSeq.SetLoops(targetCount);
			punchSeq.SetLink(textTrans.gameObject);
		}
	}
}
