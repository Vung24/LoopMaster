using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class DailyGiftBox : MonoBehaviour
	{
		[SerializeField] private AudioClip _audioClip;
		[SerializeField] private int _targetDays;
		[SerializeField] private int _boxIndex;
		[SerializeField] private SkeletonGraphic _spine;
		[SerializeField] private TMP_Text _dayText;
		[SerializeField] private GameObject _tick;
		[SerializeField] private Button _button;

		private List<RewardItem> _rewardData;
		private CancellationToken _cancellationTokenOnDestroy;

		public int TargetDays => _targetDays;

		private void Awake()
		{
			_cancellationTokenOnDestroy = this.GetCancellationTokenOnDestroy();

			if (_spine == null)
			{
				_spine = HierarchyReferenceCache.ChildComponent<SkeletonGraphic>(transform, "Spine");
			}

			if (_dayText == null)
			{
				_dayText = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Day");
			}

			if (_tick == null)
			{
				Transform tickTransform = HierarchyReferenceCache.Child(transform, "Tick");
				if (tickTransform != null)
				{
					_tick = tickTransform.gameObject;
				}
			}

			if (_button == null)
			{
				_button = GetComponent<Button>();
			}
		}

		private void Start()
		{
			if (_button != null)
			{
				_button.onClick.AddListener(OnClickBox);
			}
		}

		public void Init(List<RewardItem> rewardData, int signDays, int targetDays, int boxIndex = 0)
		{
			_rewardData = rewardData;
			_targetDays = targetDays;
			_boxIndex = boxIndex;

			int currentMonthSignDays = signDays % 30;
			int monthStage = signDays / 30;

			if (_spine != null && _spine.Skeleton != null)
			{
				string attachmentName = string.Format("img_gift_{0:D2}", _boxIndex + 1);
				_spine.Skeleton.SetAttachment("box_top01_color", attachmentName);

				bool isClaimed = currentMonthSignDays >= targetDays;
				Color tintColor = Color.white;
				if (isClaimed)
				{
					ColorUtils.FromString(out tintColor, "#9da6ac");
				}
				_spine.color = tintColor;
			}

			bool reachedCondition = currentMonthSignDays >= targetDays;
			if (_tick != null)
			{
				_tick.SetActive(reachedCondition);
			}

			if (_dayText != null)
			{
				int displayDay = monthStage * 30 + _targetDays;
				_dayText.SetText(string.Format("{0}", displayDay));
			}
		}

		public async UniTask PlayClaimAnim()
		{
			PlayParticleAnim();

			if (_audioClip != null && AudioManager.Current != null)
			{
				AudioManager.Current.PlayOneShot(_audioClip, gameObject);
			}

			await UniTask.WaitForSeconds(0.4f, cancellationToken: _cancellationTokenOnDestroy);
			await PlayBoxJumpAnim(_rewardData);
		}

		public async UniTask PlayBoxJumpAnim(List<RewardItem> rewards)
		{
			const string title = "DailyCheckIn-Title";
			var dialog = await RewardDialog.ShowWithChest(rewards, "DailyGift", title);

			if (dialog != null)
			{
				dialog.PlayChestAnim(_spine, "idle_move").Forget();
				await UniTask.WaitUntil(() => dialog == null, cancellationToken: _cancellationTokenOnDestroy);
			}
		}

		public void PlayParticleAnim()
		{
			Transform sparkTransform = HierarchyReferenceCache.Child(transform, "SpecialSparks");
			if (sparkTransform != null)
			{
				sparkTransform.gameObject.SetActive(true);
				var ps = sparkTransform.GetComponent<ParticleSystem>();
				if (ps != null)
				{
					ps.Play();
				}
			}
		}

		public void OnClickBox()
		{
			ShowRewardDetailToast();
		}

		public void ShowRewardDetailToast()
		{
			var canvas = Utils.GetCurrentUiCanvas();
			Transform canvasTransform = canvas != null ? canvas.transform : null;
			Vector3 localPos = canvasTransform != null ? canvasTransform.InverseTransformPoint(transform.position) : transform.position;

			RewardDetailToast.Show(_rewardData, canvasTransform, localPos.x, localPos.y).Forget();
		}
	}
}
