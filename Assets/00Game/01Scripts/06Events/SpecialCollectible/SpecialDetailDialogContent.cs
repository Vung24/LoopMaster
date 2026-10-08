using Cysharp.Threading.Tasks;
using Sugar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.SpecialCol
{
	public class SpecialDetailDialogContent : DialogContent, ITableViewDataSource
	{
		[SerializeField]
		private TMP_Text _titleText;
		[SerializeField]
		private TableView _tableView;
		[SerializeField]
		private ProgressBar _progressBar;
		[SerializeField]
		private Image _eventIcon;
		[SerializeField]
		private RewardCell _rewardCell;
		[SerializeField]
		private Timer _timer;
		[SerializeField]
		private GameObject _completedObject;
		[SerializeField]
		private Image _rewardIcon;
		[SerializeField]
		private TMP_Text _maxText;
		[SerializeField]
		private SpecialDetailCell _cellPrefab;
		[SerializeField] private Button _claimBtn;

		private SpecialColConfig _config;

		public static UniTask<SpecialDetailDialogContent> Show(Transform parent) => EventPrefabUI.Show<SpecialDetailDialogContent>(parent, "SpecialDetailDialogContent");

		protected override void Awake()
		{
			base.Awake();
			if (_claimBtn != null) _claimBtn.onClick.AddListener(ClaimAvailable);
			if (Managers.SpecialCol != null)
			{
				_config = Managers.SpecialCol.GetCurrConfig();
			}
		}

		protected override void Start()
		{
			base.Start();
			Refresh();
		}

		public void Refresh()
		{

			if (Managers.SpecialCol == null || _config == null) return;
			if (_claimBtn != null) _claimBtn.gameObject.SetActive(Managers.SpecialCol.GetClaimableRewards().Count > 0);

			if (_titleText != null)
			{
				_titleText.text = Managers.SpecialCol.GetEventName();
			}

			if (_eventIcon != null)
			{
				_eventIcon.sprite = Managers.SpecialCol.GetCollectibleSprite();
			}

			bool isCompleted = Managers.SpecialCol.IsCompleted();
			if (!isCompleted)
			{
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
					}
				}

				if (_timer != null)
				{
					_timer.CountdownUntil(Managers.SpecialCol.EndTime);
				}

				if (_completedObject != null)
				{
					_completedObject.SetActive(false);
				}
			}
			else
			{
				if (_rewardCell != null)
				{
					_rewardCell.gameObject.SetActive(false);
				}
				if (_timer != null)
				{
					_timer.CountdownHelper?.StopTimer();
				}
				if (_completedObject != null)
				{
					_completedObject.SetActive(true);
				}
				if (_progressBar != null)
				{
					_progressBar.SetPercent(1f);
				}
			}

			if (_tableView != null)
			{
				_tableView.DataSource = this;
				_tableView.ReloadData();
			}
		}

		private void ClaimAvailable()
		{
			var manager = Managers.SpecialCol;
			if (manager == null) return;
			manager.CollectRewards(manager.GetClaimableRewards());
			Refresh();
		}

		protected override bool OnAndroidBackButtonPressed()
		{
			Dismiss();
			return true;
		}

		public float GetHeightOfCell(TableView tableView, int section, int row)
		{
			return 130f;
		}

		public int GetNumOfCellsInSection(TableView tableView, int section)
		{
			return _config != null && _config.Cells != null ? _config.Cells.Count : 0;
		}

		public int GetNumOfSections(TableView tableView)
		{
			return 1;
		}

		private const string CELL_ID = "SpecialDetailCell";

		public TableViewCell GetTableViewCell(TableView tableView, int section, int row)
		{
			var cell = tableView.GetReusableCell(CELL_ID) as SpecialDetailCell;
			if (cell == null && _cellPrefab != null)
			{
				cell = Instantiate(_cellPrefab);
				cell.TypeIdentifier = CELL_ID;
			}
			if (cell != null && _config != null && row < _config.Cells.Count)
			{
				var dataCell = _config.Cells[row];
				bool completed = row < (Managers.SpecialCol != null ? Managers.SpecialCol.CompletedIndex : 0);
				cell.Init(row, dataCell, completed, null, null, null);
			}
			return cell;
		}
	}
}
