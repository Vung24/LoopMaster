using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;

public class PassFinalBundleRewardDialog : MonoBehaviour
{
    public AudioClip ChestJumpSound, OpenChestSound;
    private bool _busy;
    public static UniTask<PassFinalBundleRewardDialog> Show(Transform parent) => EventPrefabUI.Show<PassFinalBundleRewardDialog>(parent, "PassFinalBonusRewardDialog");
    private void Awake()
    {
        PassUI.Bind(transform, "NextBtn", () => Claim().Forget());
        PassUI.Text(transform, "Count", (Managers.Pass?.GetCurrentFinalBundleCoin() ?? 0).ToString());
        Snui_Text.EnsureFontAssets(transform);
    }
    private void Start()
    {
        PassUI.Sound(ChestJumpSound); PassUI.Animate(transform);
        PassUI.Spine(PassUI.Node<Spine.Unity.SkeletonGraphic>(transform, "CoinSpine"), "animation", false);
    }
    private async UniTask Claim()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var rewards = Managers.Pass.ClaimFinalCoins();
            if (rewards == null && Managers.Pass.GetCurrentFinalBundleCoin() > 0) return;
            PassUI.Sound(OpenChestSound);
            await PassUI.PresentRewards(rewards, transform);
            gameObject.SetActive(false); Destroy(gameObject);
        }
        finally { _busy = false; }
    }
}
