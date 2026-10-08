using Sugar.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.ChampionArena
{
	public class ChampionStageCell : TableViewCell
	{
		[SerializeField]
		private Image _lineBg;
		[SerializeField]
		private Image _labelBg;
		[SerializeField]
		private Image _itemBg;
		[SerializeField]
		private TMP_Text _indexText;
		[SerializeField]
		private GameObject _tick;
		[SerializeField]
		private GameObject _lock;
		[SerializeField] private RewardCell _rewardCell;
		[SerializeField] private TMP_Text _goalText;

		protected override void Awake()
		{
			base.Awake();
			if (_lineBg == null)
			{
				var l = HierarchyReferenceCache.Child(transform, "LineBg");
				if (l != null) _lineBg = l.GetComponent<Image>();
			}
			if (_labelBg == null)
			{
				var lb = HierarchyReferenceCache.Child(transform, "LabelBg");
				if (lb != null) _labelBg = lb.GetComponent<Image>();
			}
			if (_itemBg == null)
			{
				var ib = HierarchyReferenceCache.Child(transform, "ItemBg");
				if (ib != null) _itemBg = ib.GetComponent<Image>();
			}
			if (_indexText == null)
			{
				var it = HierarchyReferenceCache.Child(transform, "IndexText");
				if (it != null) _indexText = it.GetComponent<TMP_Text>();
				if (_indexText == null) _indexText = GetComponentInChildren<TMP_Text>(true);
			}
			if (_tick == null)
			{
				var t = HierarchyReferenceCache.Child(transform, "Tick");
				if (t != null) _tick = t.gameObject;
			}
			if (_lock == null)
			{
				var lk = HierarchyReferenceCache.Child(transform, "Lock");
				if (lk != null) _lock = lk.gameObject;
			}
		}

		public void Init(ChampionArenaStage stage, int index, bool isCompleted, bool isCurrent)
		{
			if (_goalText != null) _goalText.text = stage != null ? "+" + stage.Goal + " score" : "";
			if (_rewardCell != null && stage?.Reward != null) _rewardCell.Init(stage.Reward, !isCompleted, true).Forget();
			if (_indexText != null)
			{
				_indexText.text = (index + 1).ToString();
			}
			if (_tick != null)
			{
				_tick.SetActive(isCompleted);
			}
			if (_lock != null)
			{
				_lock.SetActive(!isCompleted && !isCurrent);
			}
		}

		public void UpdateImages(Sprite lineSprite, Sprite labelSprite)
		{
			if (_lineBg != null && lineSprite != null)
			{
				_lineBg.sprite = lineSprite;
			}
			if (_labelBg != null && labelSprite != null)
			{
				_labelBg.sprite = labelSprite;
			}
		}
	}
}
