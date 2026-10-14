using Cysharp.Threading.Tasks;
using Sugar.UI;
using UnityEngine;

namespace Snui
{
	public class PassRewardsCell : TableViewCell
	{
        public static int Height = 250;
        [SerializeField] public Transform TutorialFingerTf;
        public int Stage;
        [SerializeField] private PassNormalItem _normal;
        [SerializeField] private PassGoldenItem _golden;
        [SerializeField] private PassPurchaseItem _purchase;
        [SerializeField] private PassLevelBtn _level;
        [SerializeField] private Transform _tutorialDesc;
        protected override void Awake()
        {
            base.Awake();
            if (_normal == null) _normal = HierarchyReferenceCache.ChildComponent<PassNormalItem>(transform, "NormalItem");
            if (_golden == null) _golden = GetComponentInChildren<PassGoldenItem>(true);
            if (_purchase == null) _purchase = GetComponentInChildren<PassPurchaseItem>(true);
            if (_level == null) _level = GetComponentInChildren<PassLevelBtn>(true);
            if (_tutorialDesc == null) _tutorialDesc = HierarchyReferenceCache.Descendant(transform, "TutorialDesc");
            if (_tutorialDesc != null) _tutorialDesc.gameObject.SetActive(false);
            if (TutorialFingerTf != null) TutorialFingerTf.gameObject.SetActive(false);
            else PassUI.Active(transform, "TutorialFinger", false);
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
