using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Unicorn
{
	public class UnicornStage
	{
		public int Goal;
		public int Duration;
		public int OpponentCount;
		public List<RewardItem> Bonus;

		public UnicornStage(int goal, int duration, int opponentCount, List<RewardItem> bonus)
		{
			Goal = goal;
			Duration = duration;
			OpponentCount = opponentCount;
			Bonus = bonus;
		}
	}

	public class UnicornOpenDialogStage : MonoBehaviour
	{
		public Transform TopToastTf;
		public Transform BottomToastTf;
		[SerializeField] private Image _chestImage;
		[SerializeField] private GameObject _coinComplete;
		[SerializeField] private Button _rewardsBtn;
		[SerializeField] private Transform _stageTf;
		[SerializeField] private TMP_Text _stageLevel;
		[SerializeField] private TMP_Text _stageText;
		[SerializeField] private GameObject _mask;
		[SerializeField] private GameObject _complete;
		[SerializeField] private GameObject _lockGo;
		[SerializeField] private CanvasGroup _canvasGroup;

		public int Index { get; set; }
		public Transform ToastParent { get; set; }
		public UnicornStage StageData { get; set; }
		private bool _isCompleted;
		private Transform _toastParent;

		private void Awake()
		{
			if (_chestImage == null)
			{
				Transform t = transform.Find("ChestImage");
				if (t != null) _chestImage = t.GetComponent<Image>();
			}
			if (_coinComplete == null)
			{
				Transform t = transform.Find("CoinComplete");
				if (t != null) _coinComplete = t.gameObject;
			}
			if (_rewardsBtn == null)
			{
				Transform t = transform.Find("RewardsBtn");
				if (t != null) _rewardsBtn = t.GetComponent<Button>();
				if (_rewardsBtn == null) _rewardsBtn = GetComponentInChildren<Button>();
			}
			if (_stageTf == null)
			{
				_stageTf = transform.Find("Stage");
			}
			if (_stageLevel == null)
			{
				Transform t = _stageTf != null ? _stageTf.Find("StageLevel") : transform.Find("StageLevel");
				if (t != null) _stageLevel = t.GetComponent<TMP_Text>();
			}
			if (_stageText == null)
			{
				Transform t = _stageTf != null ? _stageTf.Find("StageText") : transform.Find("StageText");
				if (t != null) _stageText = t.GetComponent<TMP_Text>();
			}
			if (_mask == null)
			{
				Transform t = transform.Find("Mask");
				if (t != null) _mask = t.gameObject;
			}
			if (_complete == null)
			{
				Transform t = transform.Find("Complete");
				if (t != null) _complete = t.gameObject;
			}
			if (_lockGo == null)
			{
				Transform t = transform.Find("Lock");
				if (t != null) _lockGo = t.gameObject;
			}
			if (_canvasGroup == null)
			{
				_canvasGroup = GetComponent<CanvasGroup>();
			}
		}

		public void Init(int index, UnicornStage stage, Transform toastParent)
		{
			Index = index;
			StageData = stage;
			_toastParent = toastParent;
			ToastParent = toastParent;

			if (_coinComplete != null) _coinComplete.SetActive(_isCompleted);
			if (_chestImage != null)
			{
				Color c = _chestImage.color;
				c.a = _isCompleted ? 0.5f : 1f;
				_chestImage.color = c;
			}
			if (_stageText != null)
			{
				_stageText.text = $"Stage {index + 1}";
			}
			if (_stageLevel != null && stage != null)
			{
				_stageLevel.text = stage.Goal.ToString();
			}
			if (_mask != null) _mask.SetActive(false);
			if (_complete != null) _complete.SetActive(_isCompleted);
			if (_lockGo != null) _lockGo.SetActive(false);

			if (_rewardsBtn != null)
			{
				_rewardsBtn.onClick.RemoveListener(OnRewardsClicked);
				_rewardsBtn.onClick.AddListener(OnRewardsClicked);
			}
		}

		private void OnRewardsClicked()
		{
			if (_isCompleted) return;
		}
	}
}