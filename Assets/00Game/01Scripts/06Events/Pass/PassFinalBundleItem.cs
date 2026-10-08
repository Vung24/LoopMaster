using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui
{
	public class PassFinalBundleItem : PassToastItemBase
	{
        public AudioClip ShowSound, FullSound;
        public Transform CoinEndTf;
        private int _lastCoins = -1;
        protected override void Awake()
        {
            base.Awake();
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }
        public void Refresh(int displayStage = int.MaxValue)
        {
            var manager = Managers.Pass;
            displayStage = Mathf.Min(displayStage, manager.GetCurrentStage());
            int coins = manager.GetCurrentFinalBundleCoin() > 0 ? Mathf.Clamp((displayStage - manager.GetNormalStageCount()) * manager.FinalStageCoin, 0, manager.FinalTotalCoinCount) : 0;
            var bar = GetComponentInChildren<ProgressBar>(true);
            if (bar != null) bar.SetProgress(coins, manager.FinalTotalCoinCount);
            PassUI.Text(transform, "Snui_Text", coins.ToString());
            bool locked = !manager.IsVip() || displayStage < manager.GetNormalStageCount();
            PassUI.Active(transform, "Lock", locked);
            if (locked) PassUI.Spine(PassUI.Node<Spine.Unity.SkeletonGraphic>(transform, "Lock"), "idle", true);
            if (_lastCoins >= 0 && coins > _lastCoins)
            {
                if (coins == manager.FinalTotalCoinCount) PassUI.Sound(FullSound);
                foreach (var particle in GetComponentsInChildren<ParticleSystem>()) particle.Play();
            }
            _lastCoins = coins;
            var desc = PassUI.Node<TMPro.TMP_Text>(transform, "Desc");
            if (desc != null) desc.text = manager.IsVip() ? "Collect stars for up to 500 bonus coins. Claim when full or when the season ends." : "Activate Golden Pass to unlock bonus coins.";
        }
        private void OnClicked()
        {
            if (!Managers.Pass.IsVip()) PassPurchaseDialogContent.Show(transform.GetComponentInParent<Canvas>()?.rootCanvas.transform, "pass_final").Forget();
            else if (Managers.Pass.GetCurrentFinalBundleCoin() > 0 && (Managers.Pass.IsFinalBundleFinished() || Managers.Pass.IsFinished()))
                PassFinalBundleRewardDialog.Show(transform.GetComponentInParent<Canvas>()?.rootCanvas.transform).Forget();
            else Preview(new System.Collections.Generic.List<RewardItem> { RewardItem.Coin(Managers.Pass.FinalTotalCoinCount) });
        }
        public UniTask PlayShowAnim() { PassUI.Animate(transform); PassUI.Sound(ShowSound); return UniTask.CompletedTask; }
	}
}
