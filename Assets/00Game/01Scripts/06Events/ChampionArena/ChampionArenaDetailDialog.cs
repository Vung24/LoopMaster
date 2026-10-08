using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sugar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.ChampionArena
{
	public class ChampionArenaDetailDialog : MonoBehaviour, ITableViewDataSource
	{
		private const string CELL_ID = "champion_arena_cell";

		[SerializeField]
		private RaceCell _cellPrefab;
		[SerializeField]
		private AudioClip _expSingleClip;
		[SerializeField]
		private AudioClip _expMaxClip;
		[SerializeField]
		private RectTransform _ranking;
		[SerializeField]
		private RectTransform _bottom;
		[SerializeField]
		private RectTransform _top;
		[SerializeField]
		private TableView _tableView;
		[SerializeField]
		private RawImage _loading;
		[SerializeField]
		private Image _topBg;
		[SerializeField]
		private Image _topBgItem;
		[SerializeField]
		private GameObject _topBgMask;
		[SerializeField]
		private GameObject _board;
		[SerializeField]
		private TMP_Text _boardTitleText;
		[SerializeField]
		private Timer _boardTimer;
		[SerializeField]
		private TMP_Text _rankText;
		[SerializeField]
		private ChampionArenaRewardCell _rankRewardCell;
		[SerializeField]
		private GameObject _rankNoReward;
		[SerializeField]
		private Image _rankItem;
		[SerializeField]
		private RectTransform _cover;
		[SerializeField]
		private GameObject _coverTitle;
		[SerializeField]
		private TMP_Text _coverTitleText;
		[SerializeField]
		private Timer _coverTimer;
		[SerializeField]
		private ProgressBar _progressBar;
		[SerializeField]
		private RewardCell _progressRewardCell;
		[SerializeField]
		private GameObject _progressTick;
		[SerializeField]
		private ParticleSystem _progressSpark;
		[SerializeField]
		private Image _progressItem;
		[SerializeField]
		private Button _infoBtn;
		[SerializeField]
		private Button _closeBtn;
		[SerializeField]
		private Button _claimBtn;
		[SerializeField]
		private Button _stageBtn;

		private int _rank = 1;
		private int _score;
		private int _lastScore;
		private float _selfThreshold;
		private RaceCell _selfCell;
		private bool _isInfoShowing;
		private List<EventRaceOpponent> _players = new List<EventRaceOpponent>();
		private readonly List<RaceCell> _cells = new List<RaceCell>();

		public static UniTask<ChampionArenaDetailDialog> Show(Transform parent) => EventPrefabUI.Show<ChampionArenaDetailDialog>(parent, "ChampionArenaDetailDialog");

		private void Awake()
		{
			if (_tableView == null) _tableView = GetComponentInChildren<TableView>(true);
			if (_tableView != null) _tableView.DataSource = this;

			if (_closeBtn == null)
			{
				var cb = HierarchyReferenceCache.Descendant(transform, "CloseBtn");
				if (cb != null) _closeBtn = cb.GetComponent<Button>();
			}
			if (_closeBtn != null) _closeBtn.onClick.AddListener(OnCloseButtonClicked);

			if (_infoBtn == null)
			{
				var ib = HierarchyReferenceCache.Descendant(transform, "InfoBtn");
				if (ib != null) _infoBtn = ib.GetComponent<Button>();
			}
			if (_infoBtn != null) _infoBtn.onClick.AddListener(OnInfoButtonClicked);

			if (_stageBtn == null)
			{
				var sb = HierarchyReferenceCache.Descendant(transform, "StageBtn");
				if (sb != null) _stageBtn = sb.GetComponent<Button>();
			}
			if (_stageBtn != null) _stageBtn.onClick.AddListener(OnStageButtonClicked);

			if (_claimBtn == null)
			{
				var clb = HierarchyReferenceCache.Descendant(transform, "ClaimBtn");
				if (clb != null) _claimBtn = clb.GetComponent<Button>();
			}
			if (_claimBtn != null) _claimBtn.onClick.AddListener(ClaimRankRewards);
		}

		private void Start()
		{
			Managers.ChampionArena?.CheckEventState();
			Managers.ChampionArena?.TryClaimStageRewards();
			RefreshData();
			UpdateUI();
		}

		public void RefreshData()
		{
			var mgr = Managers.ChampionArena;
			if (mgr != null)
			{
				_rank = mgr.Rank;
				_score = mgr.Score;
				_lastScore = mgr.LastScore;
				_players = mgr.Opponents ?? new List<EventRaceOpponent>();
			}
			if (_tableView != null)
			{
				_tableView.ReloadData();
			}
		}

		private void UpdateUI()
		{
			if (_rankText != null)
			{
				_rankText.text = "#" + _rank;
			}
			if (_rankRewardCell != null)
			{
				var rewards = Managers.ChampionArena?.GetRankRewards(_rank);
				_rankRewardCell.SetRank(_rank);
				if (rewards != null && rewards.Length > 0) _rankRewardCell.Init(rewards[0], _rank);
			}
			if (_boardTimer != null && Managers.ChampionArena != null)
			{
				_boardTimer.CountdownUntil(Managers.ChampionArena.EndTime);
			}
			if (_progressBar != null && Managers.ChampionArena != null)
			{
				var stage = Managers.ChampionArena.GetStageProgress(_score);
				_progressBar.SetProgress(stage.progress, stage.goal);
				var stages = Managers.ChampionArena.GetStages();
				int index = Managers.ChampionArena.GetCompletedStageCount(_score);
				if (_progressRewardCell != null)
				{
					_progressRewardCell.gameObject.SetActive(index < stages.Count);
					if (index < stages.Count && stages[index].Reward != null) _progressRewardCell.Init(stages[index].Reward, false, true).Forget();
				}
			}
			if (_claimBtn != null) _claimBtn.gameObject.SetActive(Managers.ChampionArena != null && Managers.ChampionArena.IsExpired);
		}

		public int GetNumOfSections(TableView tableView) => 1;

		public int GetNumOfCellsInSection(TableView tableView, int section)
		{
			return _players != null ? _players.Count : 0;
		}

		public float GetHeightOfCell(TableView tableView, int section, int row) => RaceCell.HEIGHT;

		public TableViewCell GetTableViewCell(TableView tableView, int section, int row)
		{
			var cell = tableView.GetReusableCell(CELL_ID) as RaceCell;
			if (cell == null && _cellPrefab != null)
			{
				cell = Instantiate(_cellPrefab);
				cell.TypeIdentifier = CELL_ID;
			}
			if (cell != null && _players != null && row < _players.Count)
			{
				cell.Init(_players[row]);
			}
			return cell;
		}

		private void OnCloseButtonClicked()
		{
			Destroy(gameObject);
		}

		private void OnInfoButtonClicked()
		{
			var canvas = SnuiManager.GetCurrentUiCanvas();
			if (canvas != null || transform.parent != null)
			{
				EventPrefabUI.Show<EventInfoDialog>(canvas != null ? canvas.transform : transform.parent, "ChampionInfoDialog").Forget();
			}
		}

		private void OnStageButtonClicked()
		{
			ChampionStageDialogContent.Show(transform.parent).Forget();
		}

		private void ClaimRankRewards()
		{
			var mgr = Managers.ChampionArena;
			if (mgr != null && _claimBtn != null)
			{
				_claimBtn.interactable = false;
				bool claimed = mgr.TryClaimRankRewards();
				_claimBtn.interactable = !claimed;
				RefreshData();
				UpdateUI();
			}
		}
	}
}
