using UnityEngine;
using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using Spine.Unity;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelBoxMachine : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Transform _bar1, _bar2;
    [SerializeField] private RectTransform _progressMaskRect;
    [SerializeField] private TMP_Text _progressText;
    [SerializeField] private SkeletonGraphic _machineSpine, _chest;
    [SerializeField] private Button _button;
    [SerializeField] private Transform _lockIcon;
    [SerializeField] private AudioClip _chestAudioClip, _sliceDropAudioClip, _progressFullAudioClip, _rockerAudioClip;
    private HomeController _home;
    private RewardDialog _rewards;
    private LevelBoxDialogContent _progress;
    private GameObject _progressRoot;
    private Tweener _maskTween;
    private Tweener _textTween;
    private float _shownProgress;
    private Spine.AnimationState _machineState;
    private LevelBoxStageInfo _displayStage;
    private bool _busy;
    private int _dialogVersion;
    private CancellationTokenSource _dialogLoad;
    private readonly List<Material> _ownedMaterials = new List<Material>();
    public bool HasDialogOpen => _busy || _rewards!=null || _progress!=null;
    public bool HasAuthoredLayout => _machineSpine!=null && _button!=null;
    private static readonly float[] FastMask={40,80,126,170,210};
    private static readonly float[] NormalMask={21,42,64,85,105,126,147,168,190,210};
    private void Awake()
    {
        RestoreSpineMaterials(_machineSpine);
        RestoreSpineMaterials(_chest);
        if(_machineSpine!=null) _machineSpine.Initialize(true);
        if(_chest!=null) _chest.Initialize(true);
        foreach(var follower in GetComponentsInChildren<BoneFollowerGraphic>(true))
            if(follower.skeletonGraphic==_machineSpine || follower.skeletonGraphic==_chest)
                follower.Initialize();
        if(_button!=null) _button.onClick.AddListener(OnClickBtn);
        if(_machineSpine!=null)
        {
            _machineSpine.UnscaledTime=true;
            _machineSpine.OnAnimationRebuild+=OnMachineAnimationRebuilt;
            BindMachineEvents();
        }
        if(_chest!=null) _chest.gameObject.SetActive(false);
    }
    private void RestoreSpineMaterials(SkeletonGraphic graphic)
    {
        var library=PrefabLibrary.Current;
        if(graphic==null || library==null) return;
        graphic.material=CopyMaterial(graphic.material,library.LevelBoxSpineShader);
        graphic.additiveMaterial=CopyMaterial(graphic.additiveMaterial,library.LevelBoxSpineAdditiveShader);
        graphic.multiplyMaterial=CopyMaterial(graphic.multiplyMaterial,library.LevelBoxSpineMultiplyShader);
        graphic.screenMaterial=CopyMaterial(graphic.screenMaterial,library.LevelBoxSpineScreenShader);
    }
    private Material CopyMaterial(Material original,Shader shader)
    {
        if(original==null || shader==null) return original;
        var copy=new Material(original) { shader=shader, name=original.name+" (LevelBox shader fallback)" };
        if(copy.HasProperty("_StraightAlphaInput") && copy.GetFloat("_StraightAlphaInput")!=0) copy.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
        if(copy.HasProperty("_CanvasGroupCompatible") && copy.GetFloat("_CanvasGroupCompatible")!=0) copy.EnableKeyword("_CANVAS_GROUP_COMPATIBLE");
        _ownedMaterials.Add(copy);
        return copy;
    }
    public void Bind(HomeController home)
    {
        _home=home; RefreshProgress();
        var manager=LevelBoxManager.Ensure();
        if(manager.IsUnlock && manager.FinishedLevel>manager.LastViewedLevel && manager.PendingMilestone==0) PlayAnims().Forget();
    }
    private void OnEnable() => RefreshProgress();
    public bool IsGetRewards() => LevelBoxManager.Ensure().PendingMilestone>0;
    public void RefreshProgress()
    {
        var manager=LevelBoxManager.Ensure();
        var stage=manager.GetCurStageInfo(); int pending=manager.PendingMilestone;
        if(pending>0) { stage=manager.GetStageInfo(pending-1); stage.Progress=stage.Goal; }
        _displayStage=stage;
        if(_lockIcon!=null) _lockIcon.gameObject.SetActive(!manager.IsUnlock);
        if(_button!=null) _button.interactable=manager.IsUnlock;
        if(_bar1!=null) _bar1.gameObject.SetActive(stage.Goal==5);
        if(_bar2!=null) _bar2.gameObject.SetActive(stage.Goal==10);
        KillProgressTweens();
        SetProgressText(stage.Progress);
        if(_progressMaskRect!=null) SetProgressMaskWidth(GetTargetMaskOffsetPosX(stage.Progress,stage.Goal));
        PlayMachine(manager.IsUnlock ? "idle" : "static",true,resetPose:true);
    }
    public static float GetTargetMaskOffsetPosX(int progress,int goal) => progress<=0 ? 0 : (goal>5 ? NormalMask : FastMask)[Mathf.Clamp(progress-1,0,goal>5 ? 9 : 4)];
    private void SetProgressMaskWidth(float width)
    {
        _progressMaskRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
    }
    private void SetProgressText(float progress)
    {
        _shownProgress=progress;
        int goal=_displayStage==null ? 0 : _displayStage.Goal;
        if(_progressText!=null) _progressText.text=(goal>0 ? Mathf.Clamp01(progress/goal)*100f : 0).ToString("0")+"%";
    }
    private void KillProgressTweens()
    {
        _maskTween?.Kill(); _maskTween=null;
        _textTween?.Kill(); _textTween=null;
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if(eventData.button==PointerEventData.InputButton.Left) OnClickBtn();
    }
    public void OnClickBtn() => OpenDialog().Forget();
    /// <summary>Introduces the unlocked machine on Home and waits for dismissal.</summary>
    public async UniTask ShowHomeIntroduction(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!isActiveAndEnabled || !LevelBoxManager.Ensure().ShouldShowTutorial()) return;
        await OpenDialog();
        await UniTask.WaitUntil(IsIntroductionClosed, cancellationToken: token);
    }

    private bool IsIntroductionClosed()
    {
        return this == null || !HasDialogOpen;
    }

    private async UniTask OpenDialog()
    {
        if(_busy || !isActiveAndEnabled || (_home!=null && !_home.CanOpenLevelBox(this))) return;
        var manager=LevelBoxManager.Ensure(); if(!manager.IsUnlock) return;
        _busy=true;
        int version=_dialogVersion;
        var lifetime=CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _dialogLoad=lifetime;
        try
        {
            Transform parent=GetComponentInParent<Canvas>()?.rootCanvas.transform;
            if(parent==null) { _busy=false; return; }
            int milestone=manager.PendingMilestone;
            if(milestone>0)
            {
                var rewards=await RewardDialog.ShowLevelBox(milestone,parent,_chest,waitForMachine:true,cancellationToken:lifetime.Token);
                if(this==null || version!=_dialogVersion || !isActiveAndEnabled) { if(rewards!=null) Destroy(rewards.gameObject); return; }
                _rewards=rewards;
                if(_rewards==null) { _busy=false; return; }
                _rewards.Closed+=OnRewardClosed;
                _rewards.FocusMachine(transform);
                manager.OnViewed(); PlayMachine("last",false);
                if(_rewards.RewardSaved) StartCoroutine(RewardHandoffFallback(_rewards));
            }
            else
            {
                var progress=await Dialog.ShowContent<LevelBoxDialogContent>(parent,"LevelBoxDialogContent");
                if(this==null || version!=_dialogVersion || !isActiveAndEnabled) { if(progress!=null) Destroy(progress.GetComponentInParent<Dialog>().gameObject); return; }
                _progress=progress;
                _progressRoot=_progress.GetComponentInParent<Dialog>().gameObject;
                RewardDialog.PrepareOverlay(_progressRoot,1300);
                _progress.Dismissed+=OnProgressClosed;
                manager.OnViewed();
            }
        }
        catch(OperationCanceledException) when(lifetime.IsCancellationRequested) { }
        catch(Exception error) { if(this!=null && version==_dialogVersion) _busy=false; Debug.LogWarning("LevelBox UI unavailable: "+error.Message); }
        finally { if(_dialogLoad==lifetime) _dialogLoad=null; lifetime.Dispose(); }
    }
    private void OnRewardClosed() { if(_rewards!=null) _rewards.Closed-=OnRewardClosed; _rewards=null; _busy=false; if(isActiveAndEnabled) RefreshProgress(); }
    private void OnProgressClosed(int result)
    {
        LevelBoxManager.Ensure().SetTutorialShown();
        if(_progress!=null) _progress.Dismissed-=OnProgressClosed;
        _progress=null; _busy=false;
        if(isActiveAndEnabled) RefreshProgress();
    }
    public UniTask PlayAnims()
    {
        if(!isActiveAndEnabled) return UniTask.CompletedTask;
        var manager=LevelBoxManager.Ensure(); var old=manager.GetStageInfo(manager.LastViewedLevel);
        RefreshProgress();
        if(_progressMaskRect!=null) SetProgressMaskWidth(GetTargetMaskOffsetPosX(old.Progress,old.Goal));
        SetProgressText(old.Progress);
        PlayMachine("first",false); manager.OnViewed();
        StartCoroutine(ProgressFallback());
        return UniTask.CompletedTask;
    }
    private IEnumerator ProgressFallback()
    {
        var first=_machineSpine?.SkeletonData?.FindAnimation("first");
        if(first!=null) yield return new WaitForSecondsRealtime(first.Duration);
        if(isActiveAndEnabled && !HasDialogOpen) RefreshProgress();
    }
    private IEnumerator RewardHandoffFallback(RewardDialog rewards)
    {
        var last=_machineSpine?.SkeletonData?.FindAnimation("last");
        if(last!=null) yield return new WaitForSecondsRealtime(last.Duration);
        if(isActiveAndEnabled && _rewards==rewards) rewards.BeginChestAnimation();
    }
    private void PlayMachine(string name,bool loop,bool resetPose=false)
    {
        if(_machineSpine?.SkeletonData?.FindAnimation(name)!=null)
        {
            if(resetPose)
            {
                // The reward animation can leave slots hidden that idle does not key.
                _machineSpine.AnimationState.ClearTracks();
                _machineSpine.Skeleton.SetToSetupPose();
            }
            _machineSpine.AnimationState.SetAnimation(0,name,loop);
            _machineSpine.Update(0);
            // OnEnable may run before the child SkeletonGraphic.Awake synchronizes its submesh renderers.
            // Queue the UI rebuild; Spine will update the mesh after its initialization completes.
            if(resetPose) _machineSpine.SetAllDirty();
        }
    }
    private void OnMachineAnimationRebuilt(ISkeletonAnimation animation) => BindMachineEvents();
    private void BindMachineEvents()
    {
        var state=_machineSpine==null ? null : _machineSpine.AnimationState;
        if(ReferenceEquals(state,_machineState)) return;
        if(_machineState!=null) _machineState.Event-=OnMachineEvent;
        _machineState=state;
        if(_machineState!=null) _machineState.Event+=OnMachineEvent;
    }
    private void OnMachineEvent(Spine.TrackEntry track,Spine.Event evt)
    {
        if(!isActiveAndEnabled || evt?.Data==null) return;
        if(_machineSpine==null || _machineSpine.AnimationState.GetCurrent(0)!=track) return;
        switch(evt.Data.Name)
        {
            case "progress":
                PlaySound(_sliceDropAudioClip); var stage=_displayStage ?? LevelBoxManager.Ensure().GetCurStageInfo();
                KillProgressTweens();
                if(_progressMaskRect!=null) _maskTween=DOTween.To(()=>_progressMaskRect.rect.width,SetProgressMaskWidth,GetTargetMaskOffsetPosX(stage.Progress,stage.Goal),.6f).SetUpdate(true);
                if(_progressText!=null) _textTween=DOTween.To(()=>_shownProgress,SetProgressText,stage.Progress,.6f).SetUpdate(true);
                break;
            case "text_scale": PlaySound(_progressFullAudioClip); break;
            case "rocker": PlaySound(_rockerAudioClip); break;
            case "replace":
                if(_rewards!=null && _rewards.RewardSaved) { PlaySound(_chestAudioClip); _rewards.BeginChestAnimation(); }
                break;
        }
    }
    private void PlaySound(AudioClip clip) { if(clip!=null && AudioManager.Current!=null) AudioManager.Current.PlayOneShot(clip,gameObject); }
    private void OnDisable()
    {
        _dialogVersion++;
        _dialogLoad?.Cancel();
        _dialogLoad=null;
        StopAllCoroutines(); KillProgressTweens();
        if(_progressText!=null) _progressText.transform.DOKill();
        if(_rewards!=null) { _rewards.Closed-=OnRewardClosed; _rewards.Close(); } _rewards=null;
        if(_progress!=null) _progress.Dismissed-=OnProgressClosed; _progress=null;
        if(_progressRoot!=null) { _progressRoot.SetActive(false); Destroy(_progressRoot); } _progressRoot=null;
        _busy=false;
    }
    private void OnDestroy()
    {
        if(_button!=null) _button.onClick.RemoveListener(OnClickBtn);
        if(_machineSpine!=null) _machineSpine.OnAnimationRebuild-=OnMachineAnimationRebuilt;
        if(_machineState!=null) _machineState.Event-=OnMachineEvent;
        _machineState=null;
        foreach(var material in _ownedMaterials) if(material!=null) Destroy(material);
        _ownedMaterials.Clear();
    }
}
