using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;

public class PassOpenDialogContent : DialogContent
{
    public static UniTask<PassOpenDialogContent> Show(Transform parent) => EventPrefabUI.Show<PassOpenDialogContent>(parent, Managers.Pass.ThemeAddress("PassOpenDialogContent"));
    protected override void Awake()
    {
        _closeBtn = PassUI.Node<UnityEngine.UI.Button>(transform, "CloseButton");
        base.Awake();
        PassUI.Bind(transform, "ContinueBtn", Continue);
        Snui_Text.EnsureFontAssets(transform);
    }
    protected override void Start()
    {
        base.Start();
        PassUI.Node<Snui.Timer>(transform, "Timer")?.CountdownUntil(Managers.Pass.GetEndTime());
        PassUI.Text(transform, "Content", "Win levels to earn stars and claim Free rewards. Activate Golden Pass for the Golden reward track and bonus chest.");
        PassUI.Animate(transform);
    }
    private void Continue() { Managers.Pass.SetTutorialShown(); Dismiss(1); }
}
