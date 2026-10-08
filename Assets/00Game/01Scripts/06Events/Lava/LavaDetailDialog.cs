using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Lava
{
	public class LavaDetailDialog : MonoBehaviour
	{
		public bool IsPlayAnimation { get; protected set; }
		public bool IsEventDone { get; protected set; }

		[SerializeField] protected List<LavaPlane> _planes = new List<LavaPlane>();
		[SerializeField] protected List<LavaPlayer> _players = new List<LavaPlayer>();
		[SerializeField] protected LavaPlayer _userPlayer;
		[SerializeField] protected GameObject _playerPrefab;
		[SerializeField] protected Transform _playerNode;
		[SerializeField] protected Button _tipBtn;
		[SerializeField] protected Button _closeBtn;
		[SerializeField] protected Button _continueBtn;
		[SerializeField] protected Timer _timer;
		[SerializeField] protected TMP_Text _playerCountText;
		[SerializeField] protected TMP_Text _levelCountText;
		[SerializeField] protected TMP_Text _descriptionText;
		[SerializeField] protected Transform _topNode;
		[SerializeField] protected Transform _failNode;
		[SerializeField] protected Transform _tutorialNode;
		[SerializeField] protected TMP_Text _titleText;

		protected UniTaskCompletionSource _closeTcs;

		public static UniTask<LavaDetailDialog> Show(Transform parent) => EventPrefabUI.Show<LavaDetailDialog>(parent, "LavaDetailDialog");



		public async UniTask WaitForCloseAsync()
		{
			_closeTcs = new UniTaskCompletionSource();
			await _closeTcs.Task;
		}

		protected virtual void Awake()
		{
			if (_planes.Count == 0)
			{
				GetComponentsInChildren(true, _planes);
			}
			if (_players.Count == 0)
			{
				GetComponentsInChildren(true, _players);
			}

			if (_tipBtn == null)
			{
				_tipBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "TipButton");
			}
			if (_closeBtn == null)
			{
				_closeBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "CloseButton");
			}
			if (_continueBtn == null)
			{
				_continueBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "ContinueButton");
			}
			if (_timer == null)
			{
				_timer = HierarchyReferenceCache.ChildComponent<Timer>(transform, "Timer");
			}
			if (_playerCountText == null)
			{
				_playerCountText = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "PlayerCount");
			}
			if (_topNode == null)
			{
				_topNode = HierarchyReferenceCache.Child(transform, "Top");
			}
			if (_failNode == null)
			{
				_failNode = HierarchyReferenceCache.Child(transform, "FailNode");
			}
			if (_tutorialNode == null)
			{
				_tutorialNode = HierarchyReferenceCache.Child(transform, "Tutorial");
			}
			if (_titleText == null)
			{
				_titleText = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Title");
			}

			if (_tipBtn != null) _tipBtn.onClick.AddListener(OnClickTipButton);
			if (_closeBtn != null) _closeBtn.onClick.AddListener(OnClickCloseButton);
			if (_continueBtn != null) _continueBtn.onClick.AddListener(OnClickContinueButton);
		}

		protected virtual void Start()
		{
			UpdateInfo();
			InitPlane();
			CreatePlayers();
			StartAsync().Forget();
		}

		public void UpdateInfo()
		{
			if (Managers.Lava == null) return;

			if (_titleText != null)
			{
				I18nString title = Managers.Lava.GetTitleI18n();
				_titleText.text = "LAVA";
			}

			if (_playerCountText != null)
			{
				_playerCountText.text = Managers.Lava.GetRemainingPlayers().ToString();
			}
			if (_levelCountText != null) _levelCountText.text = $"{Managers.Lava.GetRecord()}/{Managers.Lava.GetLevelGoal()}";
			if (_descriptionText != null) _descriptionText.text = $"Win {Managers.Lava.GetLevelGoal()} levels to complete the challenge!";

			if (_timer != null)
			{
				CountdownHelper.CountdownFixTime(_timer, Managers.Lava.GetRemainTime());
			}
		}

		public void InitPlane()
		{
			int record = Managers.Lava != null ? Managers.Lava.GetRecord() : 0;
			for (int i = 0; i < _planes.Count; i++)
			{
				LavaPlane plane = _planes[i];
				if (plane != null)
				{
					plane.gameObject.SetActive(i >= record);
					plane.Idle(i * 0.15f);
				}
			}
		}

		public void CreatePlayers()
		{
			if (Managers.Lava == null) return;
			int record = Managers.Lava.GetRecord();
			if (_players.Count == 0 && _playerPrefab != null && _playerNode != null)
			{
				_playerNode.gameObject.SetActive(true);
				// Native CreatePlayers uses the authored player prefab: self plus at most nine opponents.
				int count = Mathf.Min(10, Managers.Lava.GetRemainingPlayers());
				for (int i = 0; i < count; i++)
				{
					var player = Instantiate(_playerPrefab, _playerNode, false).GetComponent<LavaPlayer>();
					player.IsUser = i == 0;
					player.transform.localScale = Vector3.one * (i == 0 ? 1.1f : 0.9f);
					if (i == 0) _userPlayer = player;
					else player.transform.SetAsFirstSibling();
					_players.Add(player);
				}
			}

			for (int i = 0; i < _players.Count; i++)
			{
				LavaPlayer player = _players[i];
				if (player != null)
				{
					Vector3 pos = i == 0 ? Vector3.zero : Managers.Lava.GetAvatarPosition(i - 1);
					if (record < _planes.Count && _planes[record] != null)
					{
						Transform top = _planes[record].GetTopNode() ?? _planes[record].GetPlaneNode();
						if (top != null)
						{
							pos += player.transform.parent.InverseTransformPoint(top.position);
						}
					}
					player.transform.localPosition = pos;
				}
			}
		}

		public virtual async UniTaskVoid StartAsync()
		{
			IsPlayAnimation = true;

			if (Managers.Lava != null)
			{
				if (Managers.Lava.ShouldPlayStartAnim())
				{
					await LavaStartDialogContent.Show(transform.parent);
				}

				if (Managers.Lava.ShouldPlayJumpAnim())
				{
					await PlayPlayersJumpAnimation();
					await PlayFallAnimation();
					await ShowRecordAddAnim();
					Managers.Lava.SynchronizeTempPlayer();
				}

				if (Managers.Lava.ShouldClaimRewards())
				{
					await LavaWinDialogContent.Show(transform.parent);
					IsEventDone = true;
					ExitDialog();
					return;
				}

				if (Managers.Lava.ShouldShowFailDialog())
				{
					await ShowFailNode();
					Managers.Lava.OnFailedShown();
					IsEventDone = true;
					ExitDialog();
					return;
				}

				await ShowTutorialIfNeeded();
			}

			ShowContinue();
			IsPlayAnimation = false;
		}

		public void ShowContinue()
		{
			if (_continueBtn != null)
			{
				_continueBtn.gameObject.SetActive(true);
				BreathAnim breath = _continueBtn.gameObject.GetOrAddComponent<BreathAnim>();
				breath?.Play(0f, 3f);
			}
		}

		public virtual async UniTask PlayPlayersJumpAnimation()
		{
			int currIndex = GetCurrPlaneIndex();
			int nextIndex = GetNextPlaneIndex();

			if (nextIndex < _planes.Count && _planes[nextIndex] != null)
			{
				Transform topNode = _planes[nextIndex].GetTopNode() ?? _planes[nextIndex].GetPlaneNode() ?? _planes[nextIndex].transform;
				Vector3 targetBase = _playerNode != null ? _playerNode.InverseTransformPoint(topNode.position) : topNode.localPosition;

				for (int i = 0; i < _players.Count; i++)
				{
					LavaPlayer player = _players[i];
					if (player != null && player.gameObject.activeSelf)
					{
						Vector3 avatarOffset = i > 0 && Managers.Lava != null ? Managers.Lava.GetAvatarPosition(i - 1) : Vector3.zero;
						Vector3 targetPos = targetBase + avatarOffset;
						player.DOJump(targetPos, 80f, 1, 0.45f).SetLink(player.gameObject);
					}
				}
				await UniTask.Delay(500);
				_planes[nextIndex].Hit();
			}
		}

		public virtual async UniTask PlayFallAnimation()
		{
			await UniTask.CompletedTask;
		}

		public virtual async UniTask ShowRecordAddAnim()
		{
			if (_playerCountText != null && Managers.Lava != null)
			{
				_playerCountText.text = Managers.Lava.GetRemainingPlayers().ToString();
			}
			await UniTask.CompletedTask;
		}

		public virtual async UniTask ShowFailNode()
		{
			if (_failNode != null)
			{
				_failNode.gameObject.SetActive(true);
				_failNode.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(_failNode.gameObject);
				await UniTask.Delay(1500);
			}
		}

		public async UniTask ShowTutorialIfNeeded()
		{
			if (Managers.Lava != null && !Managers.Lava.HasShownTutorial())
			{
				await ShowTutorial();
				Managers.Lava.OnTutorialShown();
			}
		}

		public async UniTask ShowTutorial()
		{
			if (_tutorialNode != null)
			{
				_tutorialNode.gameObject.SetActive(true);
				await UniTask.Delay(1000);
				_tutorialNode.gameObject.SetActive(false);
			}
		}

		public int GetCurrPlaneIndex() => Managers.Lava != null ? Managers.Lava.GetRecord() : 0;
		public int GetNextPlaneIndex() => GetCurrPlaneIndex() + 1;
		public LavaPlane GetCurrPlane() => GetCurrPlaneIndex() < _planes.Count ? _planes[GetCurrPlaneIndex()] : null;
		public LavaPlane GetNextPlane() => GetNextPlaneIndex() < _planes.Count ? _planes[GetNextPlaneIndex()] : null;

		private void OnClickTipButton()
		{
			LavaTipDialogContent.Show(transform.parent).Forget();
		}

		private void OnClickCloseButton()
		{
			ExitDialog();
		}

		private void OnClickContinueButton()
		{
			ExitDialog();
		}

		public void ExitDialog()
		{
			_closeTcs?.TrySetResult();
			Destroy(gameObject);
		}
	}
}
