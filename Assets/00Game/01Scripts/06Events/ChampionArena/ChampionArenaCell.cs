using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.ChampionArena
{
	public class ChampionArenaCell : RaceCell
	{
		[SerializeField]
		private Sprite _selfScoreSprite;
		[SerializeField]
		private Sprite _otherScoreSprite;
		[SerializeField]
		private Image _item;
		[SerializeField]
		private ChampionArenaRewardCell _rewardCell;
		[SerializeField]
		private Image _score;
		[SerializeField]
		private TMP_Text _scoreText;

		protected override void Awake()
		{
			base.Awake();
			if (_item == null)
			{
				var it = HierarchyReferenceCache.Child(transform, "Item");
				if (it != null) _item = it.GetComponent<Image>();
			}
			if (_rewardCell == null)
			{
				_rewardCell = GetComponentInChildren<ChampionArenaRewardCell>(true);
			}
			if (_score == null)
			{
				var sc = HierarchyReferenceCache.Child(transform, "Score");
				if (sc != null) _score = sc.GetComponent<Image>();
			}
			if (_scoreText == null)
			{
				var st = HierarchyReferenceCache.Child(transform, "ScoreText");
				if (st != null) _scoreText = st.GetComponent<TMP_Text>();
				if (_scoreText == null) _scoreText = GetComponentInChildren<TMP_Text>(true);
			}
		}

		public override void Init(EventRaceOpponent data)
		{
			base.Init(data);
			if (data == null) return;
			if (_score != null)
			{
				_score.sprite = data.IsSelf ? (_selfScoreSprite ?? _score.sprite) : (_otherScoreSprite ?? _score.sprite);
			}
			if (_scoreText != null)
			{
				_scoreText.text = data.Score.ToString();
			}
			if (_rewardCell != null)
			{
				_rewardCell.SetRank(data.Rank);
			}
		}

		public override Tween PlayEnterAnimation(float delay)
		{
			return base.PlayEnterAnimation(delay);
		}

		public override void CalculateSize(RectTransform parent)
		{
			base.CalculateSize(parent);
		}
	}
}