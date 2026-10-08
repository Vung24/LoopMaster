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
            PassUI.Active(transform, "Level", row > 0);
            ApplyBackground(Managers.Pass.IsStageUnlocked(row));
        }
        public void ChangeToUnLockStyle() => ApplyBackground(true);
        public void ChangeToLockStyle() => ApplyBackground(false);
        public void SetDisplayStage(int stage) => ApplyBackground(_stage < stage);
        private void ApplyBackground(bool unlocked)
        {
            var image = GetComponent<Image>();
            if (image != null) image.overrideSprite = _stage == 0 ? PassBg : unlocked ? ClaimedBg : null;
        }
	}
}
