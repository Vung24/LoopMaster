using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.SpecialCol
{
	public class SpecialProgress : MonoBehaviour
	{
		[SerializeField]
		private Image _icon;
		[SerializeField]
		private ProgressBar _progressBar;
		[SerializeField]
		private Timer _timer;
		[SerializeField]
		private TMP_Text _unlockText;
		[SerializeField]
		private RewardCell _rewardCell;
		[SerializeField]
		private GameObject _lockNode;
		[SerializeField]
		private GameObject _completedNode;
		[SerializeField]
		private Button _button;
		[SerializeField]
		private ParticleSystem _sparkParticle;

		public static SpecialProgress Create(Transform parent)
		{
			var prefab = EventPrefabUI.Load("SpecialProgress");
			if (prefab == null) throw new InvalidOperationException("SpecialProgress prefab is missing.");
			return Instantiate(prefab, parent, false).GetComponent<SpecialProgress>();
		}

		private void Awake()
		{
			if (_button == null)
			{
				_button = GetComponent<Button>();
				if (_button == null)
				{
					_button = gameObject.AddComponent<Button>();
				}
			}
		}

		private void Start()
		{
			if (_icon != null && Managers.SpecialCol != null)
			{
				_icon.sprite = Managers.SpecialCol.GetCollectibleSprite();
			}

			if (_button != null)
			{
				_button.onClick.AddListener(OnClickButton);
			}

			Init();
		}

		public void Init()
		{
			if (Managers.SpecialCol == null) return;

			if (Managers.SpecialCol.IsCompleted())
			{
				if (_rewardCell != null) _rewardCell.gameObject.SetActive(false);
				if (_timer != null) _timer.CountdownHelper?.StopTimer();
				if (_completedNode != null) _completedNode.SetActive(true);
				if (_progressBar != null) _progressBar.SetPercent(1f);
				return;
			}

			if (_completedNode != null) _completedNode.SetActive(false);

			int currentLevel = GameProgressStore.NextPlayableLevel;
			if (currentLevel < Managers.SpecialCol.GetUnlockLevel())
			{
				ShowLock();
				return;
			}

			UpdateProgress();
			UpdateTimer();
		}

		public void UpdateProgress()
		{
			if (Managers.SpecialCol == null) return;

			var currCell = Managers.SpecialCol.GetCurrSpecialColCell();
			if (currCell != null)
			{
				if (_progressBar != null)
				{
					_progressBar.SetProgress(Managers.SpecialCol.CurrCount, currCell.Count);
				}
				if (_rewardCell != null && currCell.Reward != null)
				{
					_rewardCell.Init(currCell.Reward, false, true).Forget();
					_rewardCell.gameObject.SetActive(true);
				}
			}

			if (_lockNode != null) _lockNode.SetActive(false);
		}

		public void ShowLock()
		{
			if (_progressBar != null) _progressBar.SetPercent(0f);
			if (_lockNode != null) _lockNode.SetActive(true);
			if (_unlockText != null && Managers.SpecialCol != null)
			{
				_unlockText.text = $"Unlock at Lv.{Managers.SpecialCol.GetUnlockLevel()}";
			}
			if (_rewardCell != null) _rewardCell.gameObject.SetActive(false);
			if (_timer != null) _timer.gameObject.SetActive(false);
		}

		public void UpdateTimer()
		{
			if (_timer != null && Managers.SpecialCol != null && Managers.SpecialCol.IsOngoing)
			{
				_timer.gameObject.SetActive(true);
				_timer.CountdownUntil(Managers.SpecialCol.EndTime);
			}
		}

		public async UniTask ShowUnlockAnimation()
		{
			if (_lockNode != null)
			{
				await _lockNode.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).SetLink(gameObject).ToUniTask();
				_lockNode.SetActive(false);
			}
			Init();
			transform.localScale = Vector3.zero;
			await transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetLink(gameObject).ToUniTask();
		}

		public async UniTask PlayAddAnimation()
		{
			if (Managers.SpecialCol == null) return;
			PlayItemShakeAnimation();
			UpdateProgress();
			await UniTask.Delay(300);
		}

		public async UniTask PlayCollectAnimation()
		{
			PlaySparkParticle();
			PlayShakeAnimation();
			await UniTask.Delay(500);
		}

		public void PlaySparkParticle()
		{
			if (_sparkParticle != null)
			{
				_sparkParticle.Play();
			}
		}

		public void PlayShakeAnimation()
		{
			transform.DOComplete();
			transform.DOShakeScale(0.35f, 0.2f, 10, 90f).SetLink(gameObject);
		}

		public void PlayItemShakeAnimation()
		{
			if (_icon != null)
			{
				_icon.transform.DOComplete();
				_icon.transform.DOShakeScale(0.3f, 0.25f, 10, 90f).SetLink(gameObject);
			}
		}

		private void OnClickButton()
		{
			ShowDetailDialog().Forget();
		}

		public async UniTaskVoid ShowDetailDialog()
		{
			if (Managers.SpecialCol == null || Managers.SpecialCol.IsLock) return;
			await SpecialDetailDialogContent.Show(GetComponentInParent<Canvas>()?.transform ?? transform.parent);
		}
	}
}