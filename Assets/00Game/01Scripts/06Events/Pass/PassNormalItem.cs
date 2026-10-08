using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;

namespace Snui
{
	public class PassNormalItem : PassToastItemBase
	{
        public AudioClip ClaimedSound;
        public UnityEvent OnClickedTutorailHandle = new UnityEvent();
        protected virtual bool VipTrack => false;
        protected int Stage;
        private Button _claimBtn;
        private RewardCell _rewardCell;
        private List<RewardItem> _rewards;
        private bool _busy;
        private bool _wasUnlocked, _bound;
        private int _displayStage = int.MaxValue;
        protected override void Awake()
        {
            base.Awake();
            _claimBtn = PassUI.Bind(transform, "ClaimBtn", Claim);
            _rewardCell = GetComponentInChildren<RewardCell>(true);
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }
        public virtual UniTask Init(int row)
        {
            transform.localScale = Vector3.one;
            if (_claimBtn != null) _claimBtn.transform.localScale = Vector3.one;
            var claimedMark = HierarchyReferenceCache.Descendant(transform, "Get");
            if (claimedMark != null) claimedMark.localScale = Vector3.one;
            _bound = false; _displayStage = int.MaxValue;
            Stage = row;
            var config = Managers.Pass.GetStageConfigByIndex(row);
            _rewards = VipTrack ? config.VipRewards : config.FreeRewards;
            if (_rewardCell != null && _rewards.Count > 0) _rewardCell.Init(_rewards[0], false, false).Forget();
            if (_rewardCell != null && _rewards.Count > 1)
            {
                var icon = PassUI.Node<Image>(_rewardCell.transform, "Icon");
                var sprite = Managers.Pass.PassSettings?.GetResidentBoxSprite(row, VipTrack);
                if (icon != null && sprite != null) { icon.sprite = sprite; icon.enabled = true; }
                PassUI.Active(_rewardCell.transform, "Ribbon", false);
                PassUI.Active(_rewardCell.transform, "Count", false);
            }
            if (_rewardCell != null) PassUI.NativeIcon(PassUI.Node<Image>(_rewardCell.transform, "Icon"));
            Refresh();
            return UniTask.CompletedTask;
        }
        public void ChangeToUnlockStyle() => Refresh();
        public void SetDisplayStage(int stage) { _displayStage = stage; Refresh(); }
        public virtual void Refresh()
        {
            var manager = Managers.Pass;
            bool claimed = VipTrack ? manager.IsVipRewardClaimed(Stage) : manager.IsFreeRewardClaimed(Stage);
            bool unlocked = manager.IsStageUnlocked(Stage) && Stage < _displayStage && (!VipTrack || manager.IsVip());
            bool locked = VipTrack && !manager.IsVip() && !claimed;
            PassUI.Active(transform, "Get", claimed);
            PassUI.Active(transform, "Lock", locked);
            if (locked) PassUI.Spine(PassUI.Node<Spine.Unity.SkeletonGraphic>(transform, "Lock"), "idle", true);
            if (_claimBtn != null) { _claimBtn.gameObject.SetActive(unlocked && !claimed); _claimBtn.interactable = !_busy; }
            if (_bound && unlocked && !_wasUnlocked && !claimed && _claimBtn != null) PassUI.Animate(_claimBtn.transform);
            _wasUnlocked = unlocked; _bound = true;
            if (_rewardCell != null) _rewardCell.gameObject.SetActive(_rewards != null && _rewards.Count > 0);
        }
        private void OnClicked()
        {
            if (VipTrack && !Managers.Pass.IsVip()) PassPurchaseDialogContent.Show(transform.GetComponentInParent<Canvas>()?.rootCanvas.transform, "pass_locked_reward").Forget();
            else Preview(_rewards);
        }
        private void Claim() => ClaimAsync().Forget();
        private async UniTask ClaimAsync()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var rewards = VipTrack ? Managers.Pass.ClaimVipRewards(Stage) : Managers.Pass.ClaimFreeRewards(Stage);
                Refresh();
                if (rewards != null && rewards.Count > 0)
                {
                    PassUI.Sound(ClaimedSound);
                    var mark = HierarchyReferenceCache.Descendant(transform, "Get");
                    if (mark != null)
                    {
                        mark.DOKill(); mark.localScale = Vector3.zero;
                        mark.DOScale(Vector3.one, .3f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(mark.gameObject);
                    }
                    OnClickedTutorailHandle.Invoke();
                    await PassUI.PresentRewards(rewards, transform);
                }
            }
            finally { _busy = false; if (this != null) Refresh(); }
        }
	}
}
