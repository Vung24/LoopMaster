using Cysharp.Threading.Tasks;
using Snui;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

public class HomeEventPass : HomeEventBase
{
    [SerializeField] private AudioClip _startSound;
    private SkeletonGraphic _iconSpine;
    private Image _progressBar;
    private bool _dialogOpen;
    protected override void Awake()
    {
        base.Awake();
        _iconSpine = PassUI.Node<SkeletonGraphic>(transform, "Spine");
        _progressBar = PassUI.Node<Image>(transform, "Progress");
    }
    private void Start()
    {
        Managers.Pass.OnProgressChanged.AddListener(UpdateUI);
        if (_textBar != null) _textBar.AddOnTimerStopListener(UpdateUI);
        UpdateUI(); PlayIdleAnimation();
    }
    public void UpdateUI()
    {
        var manager = Managers.Pass;
        if (manager == null) return;
        SetUnlockLevel(manager.GetUnlockLevel());
        if (manager.IsLocked()) return;
        if (_textBar != null)
        {
            _textBar.gameObject.SetActive(true);
            if (manager.IsFinished()) { _textBar.SetStyle(HomeIconTextBar.Style.Claim); _textBar.SetText("ENDED"); }
            else if (manager.ShouldShowTutorial()) { _textBar.SetStyle(HomeIconTextBar.Style.Start); _textBar.SetText("START"); }
            else { _textBar.SetStyle(HomeIconTextBar.Style.Timer); _textBar.CountdownUntil(manager.GetEndTime()); }
        }
        if (_progressBar != null) _progressBar.fillAmount = manager.IsFinalBundleFinished() ? 1 :
            Mathf.Clamp01((float)manager.GetCurrentPoints() / Mathf.Max(1, manager.GetNextRequirePoints()));
        SetNotifierCount(manager.GetUnclaimedRewardCount());
    }
    protected override void OnClick() => ShowDialog().Forget();
    public async UniTask ShowDialog()
    {
        if (_dialogOpen || Managers.Pass == null || !Managers.Pass.CheckActive()) return;
        _dialogOpen = true;
        try
        {
            if (Managers.Pass.IsFinished())
            {
                var ended = await PassEndDialogContent.Show(transform.parent);
                await PassUI.WaitForClose(ended);
            }
            else
            {
                if (Managers.Pass.ShouldShowTutorial())
                {
                    var opened = await PassOpenDialogContent.Show(transform.parent);
                    int result = opened.Result;
                    opened.Dismissed += value => result = value;
                    await PassUI.WaitForClose(opened);
                    if (result != 1) return;
                }
                var dialog = await PassDetailDialog.Show(transform.parent);
                await dialog.WaitForCloseAsync();
            }
        }
        catch (System.OperationCanceledException) { }
        finally { if (this != null) { _dialogOpen = false; UpdateUI(); } }
    }
    public override async UniTask ShowPopupDialog()
    {
        if (Managers.Pass == null || Managers.Pass.IsLocked()) return;
        if (Managers.Pass.IsFinished()) await ShowDialog();
        else if (Managers.Pass.ShouldShowTutorial() && HomeEventController.CanShowForceGuide())
        { HomeEventController.ForceGuideCount++; await ShowDialog(); }
    }
    public override bool PlayIdleAnimation() { PassUI.Spine(_iconSpine, "idle", true); return _iconSpine != null; }
    public override bool HasNonSkippableAnimation() => _dialogOpen;
    public override void CommitProgressBeforeEnterGame() => Managers.Pass?.UpdateLastStageAndPoints();
    protected override void OnDestroy()
    {
        Managers.Pass?.OnProgressChanged.RemoveListener(UpdateUI);
        base.OnDestroy();
    }
}
