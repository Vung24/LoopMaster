using Cysharp.Threading.Tasks;
using Sugar.UI;
using UnityEngine;

namespace Snui
{
	public class PassRewardsCell : TableViewCell
	{
        public static int Height = 250;
        public Transform TutorialFingerTf;
        public int Stage;
        private PassNormalItem _normal;
        private PassGoldenItem _golden;
        private PassPurchaseItem _purchase;
        private PassLevelBtn _level;
        protected override void Awake()
        {
            base.Awake();
            _normal = HierarchyReferenceCache.ChildComponent<PassNormalItem>(transform, "NormalItem");
            _golden = GetComponentInChildren<PassGoldenItem>(true);
            _purchase = GetComponentInChildren<PassPurchaseItem>(true);
            _level = GetComponentInChildren<PassLevelBtn>(true);
            PassUI.Active(transform, "TutorialDesc", false);
            PassUI.Active(transform, "TutorialFinger", false);
        }
        public void Init(int row)
        {
            Stage = row;
            if (_normal != null) _normal.Init(row).Forget();
            if (_golden != null) { _golden.gameObject.SetActive(row > 0); if (row > 0) _golden.Init(row).Forget(); }
            if (_purchase != null) { _purchase.gameObject.SetActive(row == 0); if (row == 0) _purchase.Init(row); }
            if (_level != null) _level.Init(row);
        }
        public void Refresh() { _normal?.Refresh(); _golden?.Refresh(); _purchase?.Refresh(); _level?.Init(Stage); }
        public void SetDisplayStage(int stage)
        {
            _normal?.SetDisplayStage(stage); _golden?.SetDisplayStage(stage);
            _purchase?.Refresh(); _level?.SetDisplayStage(stage);
        }
        public void ChangeLevelToUnlockStyle() => _level?.ChangeToUnLockStyle();
        public void ChangeLevelToLockStyle() => _level?.ChangeToLockStyle();
        public void ChangeItemToUnlockStyle() => Refresh();
        public UniTask PlayUnlockVipAnim() => _golden != null && Stage > 0 ? _golden.PlayUnlockAnim() : UniTask.CompletedTask;
	}
}
