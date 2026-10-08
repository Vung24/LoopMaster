using Cysharp.Threading.Tasks;
using Sugar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.SpecialCol
{
	public class SpecialDetailCell : TableViewCell
	{
		[SerializeField]
		private Image _icon;
		[SerializeField]
		private Image _check;
		[SerializeField]
		private Image _bg;
		[SerializeField]
		private TMP_Text _stageText;
		[SerializeField]
		private GameObject _completedObject;
		[SerializeField]
		private GameObject _uncompletedObject;
		[SerializeField]
		private RewardCell _rewardCell;

		public void Init(int index, SpecialColCell cell, bool isCompleted, Sprite s1, Sprite s2, Sprite s3)
		{
			if (_icon != null && s1 != null) _icon.sprite = s1;
			if (_check != null && s2 != null) _check.sprite = s2;
			if (_bg != null && s3 != null) _bg.sprite = s3;

			if (_stageText != null)
			{
				_stageText.SetText("Stage " + (index + 1) + (cell != null ? " • " + cell.Count + " items" : ""));
			}

			if (_completedObject != null)
			{
				_completedObject.SetActive(isCompleted);
			}

			if (_uncompletedObject != null)
			{
				_uncompletedObject.SetActive(!isCompleted);
			}

			if (_rewardCell != null && cell != null && cell.Reward != null)
			{
				_rewardCell.Init(cell.Reward, !isCompleted, true).Forget();
			}
		}
	}
}