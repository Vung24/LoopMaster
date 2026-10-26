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
    private int _lastAudioPoints;

    protected override void Awake()
    {
        base.Awake();
        _iconSpine = PassUI.Node<SkeletonGraphic>(transform, "Spine");
        _progressBar = PassUI.Node<Image>(transform, "Progress");
    }

    private void Start()
    {
        if (Managers.Pass != null)
        {
            Managers.Pass.OnProgressChanged.AddListener(UpdateUI);
        }
        if (_textBar != null)
        {
            _textBar.AddOnTimerStopListener(UpdateUI);
        }
        UpdateUI();
        PlayIdleAnimation();
    }

    public void UpdateUI()
    {
        var manager = Managers.Pass;
        if (manager == null) return;

        SetUnlockLevel(manager.GetUnlockLevel());
        if (manager.IsLocked())
        {
            if (_progressBar != null)
            {
                _progressBar.gameObject.SetActive(false);
            }
            return;
        }

        if (_progressBar != null)
        {
            _progressBar.gameObject.SetActive(true);
            _progressBar.fillAmount = manager.IsFinalBundleFinished() ? 1f :
                Mathf.Clamp01((float)manager.GetCurrentPoints() / Mathf.Max(1, manager.GetNextRequirePoints()));
        }

        if (_textBar != null)
        {
            _textBar.gameObject.SetActive(true);
            if (manager.IsFinished())
            {
                _textBar.SetStyle(HomeIconTextBar.Style.Claim);
                _textBar.SetText("ENDED");
            }
            else if (manager.ShouldShowTutorial())
            {
                _textBar.SetStyle(HomeIconTextBar.Style.Start);
                _textBar.SetText("START");
            }
            else
            {
                _textBar.SetStyle(HomeIconTextBar.Style.Timer);
                _textBar.CountdownUntil(manager.GetEndTime());
            }
        }

        SetNotifierCount(manager.GetUnclaimedRewardCount());

        int totalPoints = manager.GetTotalPoints();
        if (isActiveAndEnabled && totalPoints > _lastAudioPoints && totalPoints > manager.GetLastTotalPoints())
        {
            if (AudioManager.Current != null)
            {
                AudioManager.Current.PlayOneShot(_startSound, gameObject);
            }
        }
        if (isActiveAndEnabled)
        {
            _lastAudioPoints = totalPoints;
        }
    }

    protected override void OnClick()
    {
        base.OnClick();

        var manager = Managers.Pass;
        if (manager == null) return;

        if (manager.IsLocked())
        {
            ShowLockToast();
            return;
        }

        OnClickShowDialogAsync().Forget();
    }

    private void ShowLockToast()
    {
        PlayLRShakeAnimation();
        int unlockLevel = Managers.Pass != null ? Managers.Pass.GetUnlockLevel() : 0;
        string message = $"Unlock at Level {unlockLevel}!";
        if (CommonToast.Instance != null)
        {
            CommonToast.Instance.Show(message);
        }
        else
        {
            GameplayToast.Show(message);
        }
    }

    private async UniTask OnClickShowDialogAsync()
    {
        var manager = Managers.Pass;
        if (manager == null) return;

        if (manager.IsFinished())
        {
            await ShowEndDialogs();
        }
        else
        {
            await ShowDialog();
        }
    }

    public async UniTask ShowDialog()
    {
        if (_dialogOpen || Managers.Pass == null || !Managers.Pass.CheckActive()) return;
        _dialogOpen = true;
        try
        {
            Transform parent = GetPopupParent();
            if (Managers.Pass.ShouldShowTutorial())
            {
                var opened = await PassOpenDialogContent.Show(parent);
                int result = opened.Result;
                opened.Dismissed += value => result = value;
                await PassUI.WaitForClose(opened);
                if (result != 1) return;
            }
            var dialog = await PassDetailDialog.Show(parent);
            if (dialog != null)
            {
                await dialog.WaitForCloseAsync();
            }
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            if (this != null)
            {
                _dialogOpen = false;
                UpdateUI();
            }
        }
    }

    private async UniTask ShowEndDialogs()
    {
        if (_dialogOpen || Managers.Pass == null) return;
        _dialogOpen = true;
        try
        {
            Transform parent = GetPopupParent();
            var ended = await PassEndDialogContent.Show(parent);
            await PassUI.WaitForClose(ended);
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            if (this != null)
            {
                _dialogOpen = false;
                UpdateUI();
            }
        }
    }

    public override async UniTask ShowPopupDialog()
    {
        if (Managers.Pass == null || Managers.Pass.IsLocked()) return;

        if (Managers.Pass.IsFinished())
        {
            await ShowEndDialogs();
        }
        else if (Managers.Pass.ShouldShowTutorial() && HomeEventController.CanShowForceGuide())
        {
            HomeEventController.ForceGuideCount++;
            Transform parent = GetPopupParent();
            var opened = await PassOpenDialogContent.Show(parent);
            int result = opened.Result;
            opened.Dismissed += value => result = value;
            await PassUI.WaitForClose(opened);
            if (result == 1)
            {
                Managers.Pass.SetTutorialShown();
                var tutorial = gameObject.GetOrAddComponent<HomeEventTutorial>();
                if (tutorial != null)
                {
                    await tutorial.ShowFinger();
                    await UniTask.WaitUntil(() => tutorial == null || !tutorial.IsShowing,
                        cancellationToken: this.GetCancellationTokenOnDestroy());
                    await UniTask.WaitUntil(() => !_dialogOpen,
                        cancellationToken: this.GetCancellationTokenOnDestroy());
                }
            }
        }
    }

    private Transform GetPopupParent()
    {
        var canvas = Utils.GetCurrentUiCanvas();
        if (canvas != null) return canvas.transform;
        var parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.rootCanvas != null)
        {
            return parentCanvas.rootCanvas.transform;
        }
        return transform.root;
    }

    public override bool PlayIdleAnimation()
    {
        PassUI.Spine(_iconSpine, "idle", true);
        return _iconSpine != null;
    }

    public override bool HasNonSkippableAnimation() => _dialogOpen;

    public override void CommitProgressBeforeEnterGame()
    {
        if (Managers.Pass != null)
        {
            Managers.Pass.UpdateLastStageAndPoints();
        }
    }

    protected override void OnDestroy()
    {
        if (Managers.Pass != null)
        {
            Managers.Pass.OnProgressChanged.RemoveListener(UpdateUI);
        }
        if (_textBar != null)
        {
            _textBar.RemoveOnTimerStopListener(UpdateUI);
        }
        base.OnDestroy();
    }
}
