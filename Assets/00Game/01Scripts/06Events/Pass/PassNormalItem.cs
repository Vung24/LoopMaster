using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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
        protected override void Awake()
        {
            base.Awake();
            _claimBtn = PassUI.Bind(transform, "ClaimBtn", Claim);
            _rewardCell = GetComponentInChildren<RewardCell>(true);
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }
        public UniTask Init(int row)
        {
            transform.localScale = Vector3.one;
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
                PassUI.Active(_rewardCell.transform, "Count", true);
                PassUI.Text(_rewardCell.transform, "Count", _rewards.Count + " rewards");
            }
            Refresh();
            return UniTask.CompletedTask;
        }
        public void ChangeToUnlockStyle() => Refresh();
        public void Refresh()
        {
            var manager = Managers.Pass;
            bool claimed = VipTrack ? manager.IsVipRewardClaimed(Stage) : manager.IsFreeRewardClaimed(Stage);
            bool unlocked = manager.IsStageUnlocked(Stage) && (!VipTrack || manager.IsVip());
            PassUI.Active(transform, "Get", claimed);
            PassUI.Active(transform, "Lock", !unlocked && !claimed);
            if (!unlocked && !claimed) PassUI.Spine(PassUI.Node<Spine.Unity.SkeletonGraphic>(transform, "Lock"), "lock", true);
            if (_claimBtn != null) { _claimBtn.gameObject.SetActive(unlocked && !claimed); _claimBtn.interactable = !_busy; }
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
                    PassUI.Sound(ClaimedSound); PassUI.Animate(transform);
                    OnClickedTutorailHandle.Invoke();
                    await PassUI.PresentRewards(rewards, transform);
                }
            }
            finally { _busy = false; if (this != null) Refresh(); }
        }
	}
}
