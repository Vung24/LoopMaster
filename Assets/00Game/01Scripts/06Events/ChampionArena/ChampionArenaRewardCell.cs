using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.ChampionArena
{
	public class ChampionArenaRewardCell : MonoBehaviour
	{
		[SerializeField]
		private Sprite[] _chestSprites;
		[SerializeField]
		private ParticleSystem _backLight;
		[SerializeField]
		private Image _chest;
		[SerializeField]
		private Image _itemIcon;
		[SerializeField]
		private TMP_Text _itemText;
		private int _rank;

		private void Awake()
		{
			if (_chest == null)
			{
				var c = HierarchyReferenceCache.Child(transform, "Chest");
				if (c != null) _chest = c.GetComponent<Image>();
			}
			if (_itemIcon == null)
			{
				var i = HierarchyReferenceCache.Child(transform, "ItemIcon");
				if (i != null) _itemIcon = i.GetComponent<Image>();
			}
			if (_itemText == null)
			{
				var t = HierarchyReferenceCache.Child(transform, "ItemText");
				if (t != null) _itemText = t.GetComponent<TMP_Text>();
				if (_itemText == null) _itemText = GetComponentInChildren<TMP_Text>(true);
			}
		}

		public void SetRank(int rank)
		{
			_rank = rank;
			if (_chestSprites != null && _chestSprites.Length > 0 && _chest != null)
			{
				int idx = Mathf.Clamp(rank - 1, 0, _chestSprites.Length - 1);
				_chest.sprite = _chestSprites[idx];
			}
			if (_itemText != null)
			{
				_itemText.text = "Rank " + rank;
			}
		}

		public void Init(RewardItem reward, int rank)
		{
			SetRank(rank);
			if (reward != null && _itemText != null)
			{
				_itemText.text = reward.Count.ToString();
			}
		}
	}
}