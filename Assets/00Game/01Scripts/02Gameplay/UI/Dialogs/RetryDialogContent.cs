using Snui;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class RetryDialogContent : DialogContent
{
    public const int RESULT_RESTART = 1;

    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Button _btnRetry;
    [SerializeField] private Button _btnClose;
    [SerializeField] private Transform _heart;
    [SerializeField] private Transform _unlimitedHeart;
    private GameController _controller;
    private bool _isRestarting;
    private BaseHeartManager _heartManager;
    private int _offerVersion;

    protected override void Awake()
    {
        base.Awake();
        if (_titleText == null) _titleText = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Title");
        if (_btnRetry == null) _btnRetry = HierarchyReferenceCache.ChildComponent<Button>(transform, "RetryBtn");
        if (_btnClose == null) _btnClose = HierarchyReferenceCache.ChildComponent<Button>(transform, "CloseButton");
        if (_heart == null) _heart = HierarchyReferenceCache.Path(transform, "TipsBg", "Heart");
        if (_unlimitedHeart == null) _unlimitedHeart = HierarchyReferenceCache.Path(transform, "TipsBg", "UnlimitedHeart");
        Bind(_controller != null ? _controller : GameController.Current);
    }

    public void Bind(GameController controller)
    {
        _controller = controller;
        BindButton(_btnRetry, OnClickRetry);
        BindButton(_btnClose, OnClickCloseButton);
        UpdateHeartIcons();
        if (_titleText != null)
            _titleText.text = controller != null ? "LEVEL " + controller.CurrentLevelNumber : "RETRY LEVEL";
    }

    public override void Show()
    {
        _offerVersion++;
        base.Show();
        Snui_Text.EnsureFontAssets(transform);
        Bind(_controller != null ? _controller : GameController.Current);
    }

    public void OnClickRetry()
    {
        if (!isActiveAndEnabled || Result != RESULT_UNKNOWN || _isRestarting || _controller == null) return;
        RestartGame().Forget();
    }

    private async UniTask RestartGame()
    {
        _isRestarting = true;
        var controller = _controller;
        var data = controller.Data;
        int offerVersion = _offerVersion;
        try
        {
            if (Managers.Heart != null)
            {
                Managers.Heart.UpdateHeart();
                if (!Managers.Heart.IsHeartEnough())
                {
                    var dialog = await MoreHeartDialogContent.Show(transform.parent, JumpHeartDialogSourceScene.TryAgainBoosterIn);
                    if (dialog == null) return;
                    var overlay = dialog.GetComponentInParent<Dialog>();
                    var retryCanvas = GetComponentInParent<Canvas>();
                    if (overlay != null)
                        RewardDialog.PrepareOverlay(overlay.gameObject, Mathf.Max(1200,
                            retryCanvas != null ? retryCanvas.sortingOrder + 2 : 1200));
                    await UniTask.WaitUntil(() => dialog == null || dialog.Result != RESULT_UNKNOWN || !dialog.isActiveAndEnabled,
                        cancellationToken: this.GetCancellationTokenOnDestroy());
                }
            }
            if (this == null || !isActiveAndEnabled || Result != RESULT_UNKNOWN || _offerVersion != offerVersion ||
                _controller != controller || controller == null || controller.Data != data ||
                (Managers.Heart != null && !Managers.Heart.IsHeartEnough())) return;
            if (controller.TryReplayLevel()) Dismiss(RESULT_RESTART);
        }
        catch (System.OperationCanceledException) { }
        finally { _isRestarting = false; }
    }

    private void UpdateHeartIcons()
    {
        bool infinite = Managers.Heart != null && Managers.Heart.IsInfinite();
        if (_heart != null) _heart.gameObject.SetActive(!infinite);
        if (_unlimitedHeart != null) _unlimitedHeart.gameObject.SetActive(infinite);
    }

    private void OnEnable()
    {
        _heartManager = Managers.Heart;
        if (_heartManager != null) _heartManager.OnInfinityHeartChanged.AddListener(UpdateHeartIcons);
        UpdateHeartIcons();
    }

    private void OnDisable()
    {
        _offerVersion++;
        if (_heartManager != null) _heartManager.OnInfinityHeartChanged.RemoveListener(UpdateHeartIcons);
        _heartManager = null;
    }

    protected override void OnClickCloseButton()
    {
        if (isActiveAndEnabled) Dismiss(RESULT_CLOSE);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null) return;
        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }
}
