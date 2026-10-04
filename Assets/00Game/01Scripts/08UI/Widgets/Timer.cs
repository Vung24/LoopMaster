using Spine.Unity;
using TMPro;
using UnityEngine;

namespace Snui
{
	public class Timer : MonoBehaviour
	{
		[SerializeField] private SkeletonGraphic _spine;
		[SerializeField] private TMP_Text _countdownText;
		[SerializeField] private CountdownHelper _countdownHelper;

		public CountdownHelper CountdownHelper => _countdownHelper;

		private void Awake()
		{
			if (_spine == null)
			{
				_spine = GetComponentInChildren<SkeletonGraphic>(true);
			}

			if (_countdownText == null)
			{
				_countdownText = GetComponentInChildren<TMP_Text>(true);
			}

			if (_countdownHelper == null)
			{
				_countdownHelper = GetComponent<CountdownHelper>();
				if (_countdownHelper == null)
				{
					_countdownHelper = gameObject.AddComponent<CountdownHelper>();
				}
			}

			if (_countdownHelper != null)
			{
				_countdownHelper.OnTimerStart.AddListener(OnTimerStart);
				_countdownHelper.OnTimerTick.AddListener(OnTimeTicked);
				_countdownHelper.OnTimerStoped.AddListener(OnTimeStopped);
			}
		}

		private void Start()
		{
			CheckRTL();
		}

		private void CheckRTL()
		{
			if (_countdownText != null)
			{
				_countdownText.isRightToLeftText = UiUtils.IsUiRtl();
			}
		}

		private void OnTimerStart()
		{
			if (_spine != null && _spine.skeletonDataAsset != null && _spine.AnimationState != null)
			{
				var anim = _spine.SkeletonData.FindAnimation("active") ?? _spine.SkeletonData.FindAnimation("idle");
				if (anim != null)
				{
					_spine.AnimationState.SetAnimation(0, anim, true);
				}
			}
		}

		private void OnTimeTicked(long time)
		{
			if (_countdownText != null)
			{
				_countdownText.text = TimeFormatter.FormatTimeLeft(time);
			}
		}

		private void OnTimeStopped()
		{
			if (_spine != null && _spine.skeletonDataAsset != null && _spine.AnimationState != null)
			{
				var anim = _spine.SkeletonData.FindAnimation("idle");
				if (anim != null)
				{
					_spine.AnimationState.SetAnimation(0, anim, false);
				}
			}

			if (_countdownText != null)
			{
				_countdownText.text = TimeFormatter.FormatTimeLeft(0);
			}
		}

		private void OnDestroy()
		{
			if (_countdownHelper != null)
			{
				_countdownHelper.OnTimerStart.RemoveListener(OnTimerStart);
				_countdownHelper.OnTimerTick.RemoveListener(OnTimeTicked);
				_countdownHelper.OnTimerStoped.RemoveListener(OnTimeStopped);
			}
		}
	}
}