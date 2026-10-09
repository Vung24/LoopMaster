using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class DailyGiftCardCell : MonoBehaviour
	{
		[SerializeField] private TMP_Text _title;
		[SerializeField] private SkeletonGraphic _tick;
		[SerializeField] private SkeletonGraphic _bgLight;
		[SerializeField] private Image _bg;
		[SerializeField] private Transform _rewardNode;
		[SerializeField] private Button _button;
		[SerializeField] private Sprite _claimedBgSprite;
		[SerializeField] private TMP_Text _stateText;

		private List<RewardItem> _rewardData;
		private int _dayIndex;
		private readonly List<RewardCell> _rewardCells = new List<RewardCell>();
		private CancellationToken _cancellationTokenOnDestroy;

		private void Awake()
		{
			_cancellationTokenOnDestroy = this.GetCancellationTokenOnDestroy();

			if (_title == null)
			{
				_title = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Title");
			}

			if (_tick == null)
			{
				_tick = HierarchyReferenceCache.ChildComponent<SkeletonGraphic>(transform, "Tick");
			}

			if (_bgLight == null)
			{
				_bgLight = HierarchyReferenceCache.ChildComponent<SkeletonGraphic>(transform, "BgLight");
			}

			if (_bg == null)
			{
				_bg = HierarchyReferenceCache.ChildComponent<Image>(transform, "Bg");
			}

			if (_rewardNode == null)
			{
				_rewardNode = HierarchyReferenceCache.Child(transform, "RewardNode");
			}

			if (_button == null)
			{
				_button = GetComponent<Button>();
			}
		}

		public void Init(List<RewardItem> rewardData, int currentSignWeekday, int dayIndex)
		{
			_dayIndex = dayIndex;
			_rewardData = rewardData;

			SetTitle();
			bool ready = dayIndex == currentSignWeekday && Managers.DailyGiftManager != null && Managers.DailyGiftManager.CanClaimToday();
			if (_button != null) _button.interactable = ready;
			if (_stateText != null) _stateText.text = dayIndex < currentSignWeekday ? "COLLECTED" : ready ? "COLLECT" : "LOCKED";

			if (dayIndex < currentSignWeekday)
			{
				if (_bg != null && _claimedBgSprite != null)
				{
					_bg.sprite = _claimedBgSprite;
				}

				if (_tick != null)
				{
					_tick.gameObject.SetActive(true);
				}
			}
			else if (dayIndex == currentSignWeekday)
			{
				if (Managers.DailyGiftManager != null && Managers.DailyGiftManager.CanClaimToday())
				{
					ShowLightAnim(dayIndex).Forget();
				}
			}

			InitReward(dayIndex < currentSignWeekday).Forget();
		}

		public void SetTitle()
		{
			if (_title != null)
			{
				_title.text = "DAY " + (_dayIndex + 1);
			}
		}

		public void SetRewardClaimed()
		{
			for (int i = 0; i < _rewardCells.Count; i++)
			{
				if (_rewardCells[i] != null)
				{
					_rewardCells[i].SetTranslucence(0.5f);
				}
			}
		}

		public async UniTask ShowLightAnim(int dayIndex)
		{
			await UniTask.WaitForSeconds(0.5f, cancellationToken: _cancellationTokenOnDestroy);

			if (_bgLight != null)
			{
				_bgLight.gameObject.SetActive(true);
				string animName = dayIndex == 6 ? "idle2" : "idle";
				if (_bgLight.AnimationState != null)
				{
					_bgLight.AnimationState.SetAnimation(0, animName, true);
				}
			}
		}

		public async UniTaskVoid InitReward(bool claimed)
		{
			if (_rewardData == null || _rewardData.Count == 0 || _rewardNode == null)
			{
				if (claimed) SetRewardClaimed();
				return;
			}

			var rectTransform = _rewardNode.GetComponent<RectTransform>();
			float nodeWidth = rectTransform != null ? rectTransform.rect.width : 100f;
			int count = _rewardData.Count;
			float itemWidth = nodeWidth / count;
			float startX = (count - 1) * itemWidth * -0.5f;

			for (int i = 0; i < count; i++)
			{
				var cell = await RewardCell.Show(_rewardData[i], _rewardNode, false, false);
				if (cell != null)
				{
					cell.transform.localScale = Vector3.one * 0.7f;
					cell.transform.localPosition = new Vector3(startX + i * itemWidth, 0f, 0f);
					_rewardCells.Add(cell);
				}
			}

			if (claimed)
			{
				SetRewardClaimed();
			}
		}

		public async UniTaskVoid ClaimReward()
		{
			if (_stateText != null) _stateText.text = "COLLECTED";
			if (_button != null)
			{
				_button.enabled = false;
			}

			if (_tick != null)
			{
				_tick.gameObject.SetActive(true);
				if (_tick.AnimationState != null)
				{
					_tick.AnimationState.SetAnimation(0, "tick_done", false);
				}
			}

			await UniTask.WaitForSeconds(0.35f, cancellationToken: _cancellationTokenOnDestroy);

			var canvas = Utils.GetCurrentUiCanvas();
			Transform canvasTransform = canvas != null ? canvas.transform : null;
			Vector3 nodePos = _rewardNode != null ? _rewardNode.position : transform.position;
			Vector3 localPos = canvasTransform != null ? canvasTransform.InverseTransformPoint(nodePos) : nodePos;

			ClaimRewardsToast.Show(_rewardData, canvasTransform, localPos.x, localPos.y).Forget();

			await UniTask.WaitForSeconds(0.15f, cancellationToken: _cancellationTokenOnDestroy);

			if (_bg != null && _claimedBgSprite != null)
			{
				_bg.sprite = _claimedBgSprite;
			}

			SetRewardClaimed();

			if (_bgLight != null)
			{
				_bgLight.gameObject.SetActive(false);
			}
		}
	}
}
