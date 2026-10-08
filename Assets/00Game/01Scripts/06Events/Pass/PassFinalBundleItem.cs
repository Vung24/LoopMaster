using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui
{
	public class PassFinalBundleItem : PassToastItemBase
	{
        public AudioClip ShowSound, FullSound;
        public Transform CoinEndTf;
        protected override void Awake()
        {
            base.Awake();
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }
        public void Refresh()
        {
            var manager = Managers.Pass;
            var bar = GetComponentInChildren<ProgressBar>(true);
            if (bar != null) bar.SetProgress((float)manager.GetCurrentFinalBundleCoin() / manager.FinalTotalCoinCount);
            PassUI.Text(transform, "Snui_Text", manager.GetCurrentFinalBundleCoin().ToString());
            PassUI.Active(transform, "Lock", !manager.IsVip() || !manager.IsFinalRewardUnlocked());
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
