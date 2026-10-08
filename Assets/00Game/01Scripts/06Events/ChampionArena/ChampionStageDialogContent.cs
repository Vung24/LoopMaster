using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sugar.UI;
using UnityEngine;

namespace Snui.ChampionArena
{
	public class ChampionStageDialogContent : DialogContent, ITableViewDataSource
	{
		private const string CELL_ID = "champion_stage_cell";

		[SerializeField]
		private GameObject _cellPrefab;
		[SerializeField]
		private Sprite _cellLineBottom;
		[SerializeField]
		private Sprite _cellLineMiddle;
		[SerializeField]
		private Sprite _cellLineTop;
		[SerializeField]
		private Sprite _cellLabelNormal;
		[SerializeField]
		private Sprite _cellLabelDone;

		private TableView _tableView;
		private List<ChampionArenaStage> _stages = new List<ChampionArenaStage>();

		public static UniTask<ChampionStageDialogContent> Show(Transform parent) => EventPrefabUI.Show<ChampionStageDialogContent>(parent, "ChampionStageDialog");

		protected override void Awake()
		{
			base.Awake();
			_tableView = GetComponentInChildren<TableView>(true);
			if (_tableView != null)
			{
				_tableView.DataSource = this;
			}
		}

		protected override void Start()
		{
			if (Managers.ChampionArena != null)
			{
				_stages = Managers.ChampionArena.GetStages();
			}
			if (_tableView != null)
			{
				_tableView.ReloadData();
			}
		}

		public int GetNumOfSections(TableView tableView) => 1;

		public int GetNumOfCellsInSection(TableView tableView, int section)
		{
			return _stages != null ? _stages.Count : 0;
		}

		public float GetHeightOfCell(TableView tableView, int section, int row) => 130f;

		public TableViewCell GetTableViewCell(TableView tableView, int section, int row)
		{
			var cell = tableView.GetReusableCell(CELL_ID) as ChampionStageCell;
			if (cell == null && _cellPrefab != null)
			{
				var go = Instantiate(_cellPrefab);
				cell = go.GetComponent<ChampionStageCell>();
				if (cell != null) cell.TypeIdentifier = CELL_ID;
			}
			if (cell != null && _stages != null && row < _stages.Count)
			{
				int completedCount = Managers.ChampionArena != null ? Managers.ChampionArena.GetCompletedStageCount(Managers.ChampionArena.Score) : 0;
				bool isCompleted = row < completedCount;
				bool isCurrent = row == completedCount;
				cell.Init(_stages[row], row, isCompleted, isCurrent);

				Sprite line = row == 0 ? _cellLineBottom : (row == _stages.Count - 1 ? _cellLineTop : _cellLineMiddle);
				Sprite label = isCompleted ? _cellLabelDone : _cellLabelNormal;
				cell.UpdateImages(line, label);
			}
			return cell;
		}
	}
}
