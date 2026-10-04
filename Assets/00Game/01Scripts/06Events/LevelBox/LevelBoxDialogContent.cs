using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using Snui;

public class LevelBoxDialogContent : DialogContent
{
    [SerializeField] private Button _continueBtn;
    [SerializeField] private ProgressBar _progressBar;
    [SerializeField] private RewardCell _rewardCell;
    protected override void Awake()
    {
        base.Awake();
        if(_continueBtn!=null) _continueBtn.onClick.AddListener(OnClickCloseButton);
    }
    protected override void Start()
    {
        base.Start();
        var stage=LevelBoxManager.Ensure().GetCurStageInfo();
        _progressBar?.SetPercent(stage.Progress,stage.Goal);
        if(_rewardCell!=null) _rewardCell.Init(stage.Reward,true,false).Forget();
    }
    private void Update() { if(Input.GetKeyDown(KeyCode.Escape)) OnAndroidBackButtonPressed(); }
    private void OnDestroy() { if(_continueBtn!=null) _continueBtn.onClick.RemoveListener(OnClickCloseButton); if(_closeBtn!=null) _closeBtn.onClick.RemoveListener(OnClickCloseButton); }
}
