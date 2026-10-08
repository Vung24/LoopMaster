using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class PassLevelBtn : PassToastItemBase
	{
        public Sprite ClaimedBg, PassBg;
        private int _stage;
        protected override void Awake()
        {
            base.Awake();
            if (_button != null) _button.onClick.AddListener(() =>
                Preview(Managers.Pass.GetStageConfigByIndex(_stage).FreeRewards));
        }
        public void Init(int row)
        {
            _stage = row; PassUI.Text(transform, "Level", row.ToString());
            if (Managers.Pass.IsStageUnlocked(row)) ChangeToUnLockStyle(); else ChangeToLockStyle();
        }
        public void ChangeToUnLockStyle() { var image = GetComponent<Image>(); if (image != null && ClaimedBg != null) image.sprite = ClaimedBg; }
        public void ChangeToLockStyle() { var image = GetComponent<Image>(); if (image != null && PassBg != null) image.sprite = PassBg; }
	}
}
