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

		protected readonly UniTaskCompletionSource _closeTcs = new UniTaskCompletionSource();
		protected int _record;
		protected int _remainingPlayers;
		protected int _losePlayers;
		protected bool _jumpPending;
		protected bool _failed;
		protected bool _timedOut;

		public static UniTask<LavaDetailDialog> Show(Transform parent) => EventPrefabUI.Show<LavaDetailDialog>(parent, "LavaDetailDialog");



		public async UniTask WaitForCloseAsync()
		{
			await _closeTcs.Task.AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
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
			if (Managers.Lava == null) return;
			Managers.Lava.CheckEventState();
			_record = Managers.Lava.GetRecord();
			_remainingPlayers = Managers.Lava.GetRemainingPlayers();
			_losePlayers = Managers.Lava.GetTempLosePlayers();
			_jumpPending = Managers.Lava.ShouldPlayJumpAnim();
			_timedOut = !Managers.Lava.IsWin() && Managers.Lava.IsExpired();
			_failed = Managers.Lava.IsFail() && !_timedOut;
			IsEventDone = _failed || _timedOut;
			if (IsEventDone)
			{
				var info = _topNode != null ? HierarchyReferenceCache.Child(_topNode, "InfoBg") : null;
				if (info != null) info.gameObject.SetActive(false);
				if (_timer != null) _timer.gameObject.SetActive(false);
			}
			if (_continueBtn != null) _continueBtn.gameObject.SetActive(false);
			if (_failNode != null) _failNode.gameObject.SetActive(false);
			UpdateInfo();
			InitPlane();
			CreatePlayers();
			// Cache presentation values before acknowledging the terminal state.
			if (_timedOut) Managers.Lava.OnTimeOutShown();
			else if (_failed) Managers.Lava.OnFailedShown();
			StartAsync().Forget();
		}

		public void UpdateInfo()
		{
			if (Managers.Lava == null) return;

			if (_titleText != null)
			{
				_titleText.text = Managers.Lava.GetTitleKey() == "Lava-BouncyTitle" ? "BOUNCY CHALLENGE" : "LAVA";
			}

			if (_playerCountText != null)
			{
				_playerCountText.text = _remainingPlayers.ToString();
			}
			if (_levelCountText != null) _levelCountText.text = $"{(_jumpPending ? Mathf.Max(0, _record - 1) : _record)}/{Managers.Lava.GetLevelGoal()}";
			if (_descriptionText != null) _descriptionText.text = $"Win {Managers.Lava.GetLevelGoal()} levels to complete the challenge!";

			if (_timer != null)
			{
				CountdownHelper.CountdownFixTime(_timer, Managers.Lava.GetRemainTime());
			}
		}

		public void InitPlane()
		{
			int record = GetCurrPlaneIndex();
			for (int i = 0; i < _planes.Count; i++)
			{
				LavaPlane plane = _planes[i];
				if (plane != null)
				{
					plane.gameObject.SetActive(i == 0 || i == _planes.Count - 1 || i >= record);
					if (plane.gameObject.activeSelf) plane.Idle(i * 0.5f);
				}
			}
		}

		public void CreatePlayers()
		{
			if (Managers.Lava == null) return;
			int record = GetCurrPlaneIndex();
			if (_players.Count == 0 && _playerPrefab != null && _playerNode != null)
			{
				_playerNode.gameObject.SetActive(true);
				// Native CreatePlayers uses the authored player prefab: self plus at most nine opponents.
				int count = Mathf.Min(10, _remainingPlayers);
				for (int i = 0; i < count; i++)
				{
					var player = Instantiate(_playerPrefab, _playerNode, false).GetComponent<LavaPlayer>();
					player.IsUser = i == 0;
					LavaPlayer.SetDisplayAvatar(player.transform, i);
					player.Avatar?.SetAvatar(i == 0 ? Snui.Profile.AccountManager.GetDisplayUserInfo()
						: new Snui.Profile.PlayerInfo { Avatar = "avatar_" + (i + 1), AvatarFrame = "default" });
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
						Transform top = _planes[record].GetPlaneNode() ?? _planes[record].transform;
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
			SetButtonsInteractable(false);
			try
			{
				if (!IsEventDone && Managers.Lava.ShouldPlayStartAnim())
				{
					await LavaStartDialogContent.ShowWithDialog(transform.parent)
						.AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
				}

				if (_jumpPending || _failed)
				{
					await PlayPlayersJumpAnimation();
					await PlayFallAnimation();
					if (_jumpPending)
					{
						Managers.Lava.SynchronizeTempPlayer();
						await ShowRecordAddAnim();
					}
				}

				if (Managers.Lava.ShouldClaimRewards())
				{
					await LavaWinDialogContent.ShowWithDialog(transform.parent)
						.AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
					IsEventDone = true;
					ExitDialog();
					return;
				}

				if (IsEventDone)
				{
					await ShowFailNode();
				}
				else await ShowTutorialIfNeeded();
				ShowContinue();
			}
			catch (System.OperationCanceledException) { }
			finally
			{
				if (this != null)
				{
					IsPlayAnimation = false;
					SetButtonsInteractable(true);
				}
			}
		}

		private void SetButtonsInteractable(bool enabled)
		{
			if (_tipBtn != null) _tipBtn.interactable = enabled;
			if (_closeBtn != null) _closeBtn.interactable = enabled;
			if (_continueBtn != null) _continueBtn.interactable = enabled;
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
				Transform topNode = _planes[nextIndex].GetPlaneNode() ?? _planes[nextIndex].transform;
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
				await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());
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
			if (_levelCountText != null && Managers.Lava != null)
				_levelCountText.text = $"{_record}/{Managers.Lava.GetLevelGoal()}";
			await UniTask.CompletedTask;
		}

		public virtual async UniTask ShowFailNode()
		{
			if (_failNode != null)
			{
				var fail = HierarchyReferenceCache.Child(_failNode, "FailDesc");
				var timeout = HierarchyReferenceCache.Child(_failNode, "TimeOutDesc");
				if (fail != null) fail.gameObject.SetActive(_failed);
				if (timeout != null) timeout.gameObject.SetActive(_timedOut);
				_failNode.gameObject.SetActive(true);
				var group = _failNode.GetComponent<CanvasGroup>() ?? _failNode.gameObject.AddComponent<CanvasGroup>();
				group.alpha = 0f;
				group.DOFade(1f, 0.3f).SetLink(_failNode.gameObject);
				await UniTask.Delay(300, cancellationToken: this.GetCancellationTokenOnDestroy());
				Transform description = _timedOut ? timeout : fail;
				if (description != null)
				{
					description.localScale = Vector3.zero;
					description.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
					await UniTask.Delay(300, cancellationToken: this.GetCancellationTokenOnDestroy());
				}
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
				await UniTask.Delay(1000, cancellationToken: this.GetCancellationTokenOnDestroy());
				_tutorialNode.gameObject.SetActive(false);
			}
		}

		public int GetCurrPlaneIndex() => Mathf.Clamp(_record - (_jumpPending && !_failed ? 1 : 0), 0, Mathf.Max(0, _planes.Count - 1));
		public int GetNextPlaneIndex() => Mathf.Clamp(_record + (_failed ? 1 : 0), 0, Mathf.Max(0, _planes.Count - 1));
		public LavaPlane GetCurrPlane() => _planes.Count > 0 ? _planes[GetCurrPlaneIndex()] : null;
		public LavaPlane GetNextPlane() => _planes.Count > 0 ? _planes[GetNextPlaneIndex()] : null;

		private void OnClickTipButton()
		{
			if (IsPlayAnimation) return;
			LavaTipDialogContent.Show(transform.parent).Forget();
		}

		private void OnClickCloseButton()
		{
			if (IsPlayAnimation) return;
			ExitDialog();
		}

		private void OnClickContinueButton()
		{
			if (IsPlayAnimation) return;
			ExitDialog();
		}

		public void ExitDialog()
		{
			gameObject.SetActive(false);
			_closeTcs?.TrySetResult();
			Destroy(gameObject);
		}

		protected virtual void OnDestroy()
		{
			_closeTcs.TrySetResult();
		}
	}
}
