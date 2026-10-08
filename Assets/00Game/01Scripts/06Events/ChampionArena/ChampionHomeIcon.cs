using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snui.ChampionArena
{
	public class ChampionHomeIcon : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
	{
		[HideInInspector]
		public UnityEvent OnDestroyEvent = new UnityEvent();

		[SerializeField]
		private TMP_Text _rank;
		[SerializeField]
		private TMP_Text _time;
		[SerializeField]
		private SkeletonGraphic _spine;
		private Button _button;
		private CountdownHelper _countdownHelper;
		private HomeIconNotifier _notifier;
		private Transform _originParent;
		private Image _tutorialMask;
		private float _originScale = 1f;
		private Tween _clickTween;

		public static async UniTask<ChampionHomeIcon> Create(Transform parent)
		{
			GameObject prefab = EventPrefabUI.Load("ChampionHomeIcon");
			if (prefab == null)
			{
				prefab = Resources.Load<GameObject>("UI/ChampionArena/ChampionHomeIcon");
			}
			if (prefab != null)
			{
				GameObject go = Instantiate(prefab, parent);
				var icon = go.GetComponent<ChampionHomeIcon>();
				return icon;
			}
			await UniTask.CompletedTask;
			return null;
		}

		private void Awake()
		{
			_originScale = transform.localScale.x;
			if (_rank == null)
			{
				var r = HierarchyReferenceCache.Descendant(transform, "RankText");
				if (r != null) _rank = r.GetComponent<TMP_Text>();
				if (_rank == null) _rank = GetComponentInChildren<TMP_Text>(true);
			}
			if (_time == null)
			{
				var t = HierarchyReferenceCache.Descendant(transform, "TimeText");
				if (t != null) _time = t.GetComponent<TMP_Text>();
			}
			if (_spine == null)
			{
				_spine = GetComponentInChildren<SkeletonGraphic>(true);
			}
			_button = GetComponent<Button>();
			if (_button != null)
			{
				_button.onClick.AddListener(OnClick);
			}
			TryCreateCountdownHelper();
		}

		private void Start()
		{
			CreateNotifierAsync().Forget();
			UpdateUI();
		}

		private void OnClick()
		{
			ShowDetailDialog().Forget();
		}

		private void OnDestroy()
		{
			ClearClickTween();
			OnDestroyEvent?.Invoke();
		}

		public void PlayEnterAnimation()
		{
			transform.localScale = Vector3.zero;
			transform.DOScale(_originScale, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
		}

		public async UniTask PlayAddAnimation()
		{
			transform.localScale = Vector3.one * (_originScale * 1.2f);
			await transform.DOScale(_originScale, 0.25f).SetEase(Ease.OutQuad).SetLink(gameObject).ToUniTask();
		}

		public bool PlayIdleAnimation()
		{
			if (_spine != null && _spine.AnimationState != null)
			{
				_spine.AnimationState.SetAnimation(0, "idle", false);
				return true;
			}
			return false;
		}

		private async UniTask PlayGainAnimation(int score)
		{
			await PlayAddAnimation();
			UpdateUI();
		}

		private async UniTask ShowDetailDialog()
		{
			var canvas = SnuiManager.GetCurrentUiCanvas();
			var parent = canvas != null ? canvas.transform : GetComponentInParent<Canvas>()?.rootCanvas.transform;
			if (Managers.ChampionArena != null && Managers.ChampionArena.CanStartEvent())
				await ChampionArenaOpenDialogContent.Show(parent);
			else await ChampionArenaDetailDialog.Show(parent);
		}

		private void UpdateUI()
		{
			var mgr = Managers.ChampionArena;
			if (mgr != null && mgr.CanStartEvent())
			{
				if (_rank != null) _rank.text = "JOIN";
				if (_time != null) _time.text = string.Empty;
				return;
			}
			if (mgr != null && mgr.IsOngoing)
			{
				if (_rank != null)
				{
					_rank.text = "#" + mgr.Rank;
				}
				if (_countdownHelper != null && mgr.EndTime > 0)
				{
					_countdownHelper.CountdownUntil(mgr.EndTime);
				}
			}
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			ClearClickTween();
			_clickTween = transform.DOScale(_originScale * 0.92f, 0.1f).SetLink(gameObject);
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			ClearClickTween();
			_clickTween = transform.DOScale(_originScale, 0.15f).SetEase(Ease.OutBack).SetLink(gameObject);
		}

		private void ClearClickTween()
		{
			if (_clickTween != null && _clickTween.IsActive())
			{
				_clickTween.Kill();
			}
			_clickTween = null;
		}

		private void TryCreateCountdownHelper()
		{
			if (_countdownHelper == null)
			{
				_countdownHelper = GetComponent<CountdownHelper>();
				if (_countdownHelper == null)
				{
					_countdownHelper = gameObject.AddComponent<CountdownHelper>();
				}
				_countdownHelper.OnTimerTick.AddListener(OnTimeTicked);
				_countdownHelper.OnTimerStoped.AddListener(OnTimeStopped);
			}
		}

		private void OnTimeTicked(long remainingSeconds)
		{
			if (_time != null)
			{
				_time.text = TimeFormatter.FormatSeconds(remainingSeconds);
			}
		}

		private void OnTimeStopped()
		{
			if (_time != null)
			{
				_time.text = "00:00:00";
			}
			UpdateUI();
		}

		private async UniTaskVoid CreateNotifierAsync()
		{
			if (_notifier == null)
			{
				_notifier = await HomeIconNotifier.Create(transform);
			}
		}
	}
}
