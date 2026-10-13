using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;

public class PassFinalBundleRewardDialog : BasePopup
{
    public AudioClip ChestJumpSound, OpenChestSound;
    private bool _busy;
    public override bool IsBusy => _busy;
    public static UniTask<PassFinalBundleRewardDialog> Show(Transform parent) => EventPrefabUI.Show<PassFinalBundleRewardDialog>(parent, "PassFinalBonusRewardDialog");
    protected override void Awake()
    {
        base.Awake();
        PassUI.Bind(transform, "NextBtn", () => Claim().Forget());
        PassUI.Text(transform, "Count", (Managers.Pass?.GetCurrentFinalBundleCoin() ?? 0).ToString());
        Snui_Text.EnsureFontAssets(transform);
    }
    protected override void Start()
    {
        base.Start();
        PassUI.Sound(ChestJumpSound); PassUI.Animate(transform);
        PassUI.Spine(PassUI.Node<Spine.Unity.SkeletonGraphic>(transform, "CoinSpine"), "animation", false);
    }
    private async UniTask Claim()
    {
        if (!IsOpen || _busy) return;
        _busy = true;
        try
        {
            var rewards = Managers.Pass.ClaimFinalCoins();
            if (rewards == null && Managers.Pass.GetCurrentFinalBundleCoin() > 0) return;
            PassUI.Sound(OpenChestSound);
            await PassUI.PresentRewards(rewards, transform);
            ForceClose();
        }
        finally { _busy = false; }
    }
}
