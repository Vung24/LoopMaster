using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;

public class PassEndDialogContent : DialogContent
{
    private bool _busy;
    public static UniTask<PassEndDialogContent> Show(Transform parent) => EventPrefabUI.Show<PassEndDialogContent>(parent, Managers.Pass.ThemeAddress("PassEndDialogContent"));
    protected override void Awake()
    {
        _closeBtn = PassUI.Node<UnityEngine.UI.Button>(transform, "CloseButton");
        base.Awake();
        PassUI.Bind(transform, "ContinueBtn", () => Settle().Forget());
        Snui_Text.EnsureFontAssets(transform);
    }
    protected override void Start()
    {
        base.Start(); PassUI.Animate(transform);
        PassUI.Text(transform, "Content", "The season has ended. Collect your unlocked rewards and start the new season.");
    }
    private async UniTask Settle()
    {
        if (_busy || Managers.Pass == null || !Managers.Pass.IsFinished()) return;
        _busy = true;
        try
        {
            var manager = Managers.Pass;
            var rewards = manager.ClaimAllCurrentRewards();
            var coins = manager.ClaimFinalCoins();
            if (coins != null) rewards.AddRange(coins);
            // Failed grants retain the old season for retry.
            if (!manager.TryStartNextSession())
            { PassUI.Text(transform, "Content", "Rewards could not be saved. Please try again."); return; }
            await PassUI.PresentRewards(rewards, transform);
            Dismiss(1);
        }
        finally { _busy = false; }
    }
}
