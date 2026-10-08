using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Snui
{
	public class DailyRewardsProgress : MonoBehaviour
	{
		public Sprite BaseBoxSprite;
		public Sprite OpenedBoxSprite;

		[SerializeField] private ProgressBar _bar;
		[SerializeField] private TMP_Text _count;
		private Dictionary<int, Sequence> _rotateSequences = new Dictionary<int, Sequence>();
		private List<int> _canCollectBox = new List<int>();
		private readonly Dictionary<Button, UnityAction> _boxListeners = new Dictionary<Button, UnityAction>();

		private void Awake()
		{
			if (_count == null)
			{
				_count = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Path(transform, "Record", "Count"));
				if (_count == null) _count = GetComponentInChildren<TMP_Text>(true);
			}

			if (_bar == null)
			{
				_bar = HierarchyReferenceCache.ChildComponent<ProgressBar>(transform, "Board");
				if (_bar == null) _bar = GetComponentInChildren<ProgressBar>(true);
			}

		}

		private void OnEnable()
		{
			Init();
		}

		public void Init()
		{
			InitBoxes();
		}

		private bool HasClaimReward()
		{
			return _canCollectBox.Count > 0;
		}

		private void InitBoxState()
		{
			InitBoxes();
		}

		public void InitBoxes()
		{
			_canCollectBox.Clear();
			int boxCount = DailyCheckInManager.BOX_COUNT;
			var boxStates = DailyCheckInManager.Instance.EachBoxClaimState;

			for (int i = 0; i < boxCount; i++)
			{
				int boxIndex = i;
				Transform giftTrans = HierarchyReferenceCache.Child(transform, $"Gift{boxIndex}");
				if (giftTrans == null) continue;

				Image boxImg = HierarchyReferenceCache.ChildComponent<Image>(giftTrans, "GiftImg");
				if (boxImg == null) boxImg = giftTrans.GetComponentInChildren<Image>(true);

				Transform redDot = HierarchyReferenceCache.Path(giftTrans, "GiftImg", "RedDot");
				if (redDot == null) redDot = HierarchyReferenceCache.Child(giftTrans, "RedDot");

				TMP_Text levelText = HierarchyReferenceCache.ChildComponent<TMP_Text>(giftTrans, "LevelText");
				if (levelText != null)
				{
					int condition = DailyCheckInManager.Instance.GetBoxLevelCondition(boxIndex);
					levelText.text = $"{condition}";
				}

				Button btn = giftTrans.GetComponent<Button>();
				if (btn != null && !_boxListeners.ContainsKey(btn))
				{
					UnityAction listener = () => OnClickBox(boxIndex);
					btn.onClick.AddListener(listener);
					_boxListeners.Add(btn, listener);
				}

				RewardState state = (boxStates != null && boxIndex < boxStates.Count)
					? boxStates[boxIndex]
					: RewardState.NotMeetCondition;
				if (btn != null) btn.interactable = DailyCheckInManager.IsOpen && state == RewardState.CanClaim;

				if (_rotateSequences.TryGetValue(boxIndex, out var seq) && seq != null)
				{
					seq.Kill();
					_rotateSequences.Remove(boxIndex);
				}

				if (giftTrans != null)
				{
					giftTrans.localRotation = Quaternion.identity;
				}

				switch (state)
				{
					case RewardState.Claimed:
						if (boxImg != null && OpenedBoxSprite != null) boxImg.sprite = OpenedBoxSprite;
						if (redDot != null) redDot.gameObject.SetActive(false);
						break;

					case RewardState.CanClaim:
						if (boxImg != null && BaseBoxSprite != null) boxImg.sprite = BaseBoxSprite;
						if (redDot != null) redDot.gameObject.SetActive(true);
						_canCollectBox.Add(boxIndex);

						if (giftTrans != null && isActiveAndEnabled)
						{
							var wobble = DOTween.Sequence()
								.Append(giftTrans.DOLocalRotate(new Vector3(0, 0, 10f), 0.12f))
								.Append(giftTrans.DOLocalRotate(new Vector3(0, 0, -10f), 0.24f))
								.Append(giftTrans.DOLocalRotate(Vector3.zero, 0.12f))
								.AppendInterval(1.5f)
								.SetLoops(-1, LoopType.Restart)
								.SetUpdate(true)
								.SetLink(giftTrans.gameObject);
							_rotateSequences[boxIndex] = wobble;
						}
						break;

					case RewardState.NotMeetCondition:
					default:
						if (boxImg != null && BaseBoxSprite != null) boxImg.sprite = BaseBoxSprite;
						if (redDot != null) redDot.gameObject.SetActive(false);
						break;
				}
			}

			if (_bar != null)
			{
				_bar.SetPercent(DailyCheckInManager.Instance.GetBoxProgressPercent());
			}

			if (_count != null)
			{
				_count.text = DailyCheckInManager.Instance.LoginDays.ToString();
			}
		}

		private void OnClickBox(int boxIndex)
		{
			var boxStates = DailyCheckInManager.Instance.EachBoxClaimState;
			if (boxStates != null && boxIndex >= 0 && boxIndex < boxStates.Count)
			{
				if (boxStates[boxIndex] == RewardState.CanClaim)
				{
					DailyCheckInManager.Instance.ShowCollectRewards();
				}
			}
		}

		public void PlayCollectedAnim()
		{
			InitBoxes();
		}

		private void OnDisable()
		{
			foreach (var kvp in _rotateSequences)
			{
				if (kvp.Value != null && kvp.Value.IsActive())
				{
					kvp.Value.Kill();
				}
			}
			_rotateSequences.Clear();
			for (int i = 0; i < DailyCheckInManager.BOX_COUNT; i++)
			{
				var gift = HierarchyReferenceCache.Child(transform, $"Gift{i}");
				if (gift != null) gift.localRotation = Quaternion.identity;
			}
		}

		private void OnDestroy()
		{
			foreach (var entry in _boxListeners)
				if (entry.Key != null) entry.Key.onClick.RemoveListener(entry.Value);
			_boxListeners.Clear();
		}
	}
}
