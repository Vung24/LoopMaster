using UnityEngine;
using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using Spine.Unity;
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
    private Spine.AnimationState _machineState;
    private LevelBoxStageInfo _displayStage;
    private bool _busy;
    private readonly System.Collections.Generic.List<Material> _ownedMaterials = new System.Collections.Generic.List<Material>();
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
    // Keep recovery for older prefabs, using the package shader references instead
    // of Shader.Find: exported placeholder shaders have the same shader names.
    private void RestoreSpineMaterials(SkeletonGraphic graphic)
    {
        var library=RecoveryPrefabLibrary.Current;
        if(graphic==null || library==null) return;
        graphic.material=CopyMaterial(graphic.material,library.LevelBoxSpineShader);
        graphic.additiveMaterial=CopyMaterial(graphic.additiveMaterial,library.LevelBoxSpineAdditiveShader);
        graphic.multiplyMaterial=CopyMaterial(graphic.multiplyMaterial,library.LevelBoxSpineMultiplyShader);
        graphic.screenMaterial=CopyMaterial(graphic.screenMaterial,library.LevelBoxSpineScreenShader);
    }
    private Material CopyMaterial(Material original,Shader shader)
    {
        if(original==null || shader==null) return original;
        var copy=new Material(original) { shader=shader, name=original.name+" (LevelBox recovery)" };
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
        if(_progressText!=null) _progressText.text=stage.Progress+"/"+stage.Goal;
        if(_progressMaskRect!=null) { _maskTween?.Kill(); var offset=_progressMaskRect.offsetMax; offset.x=GetTargetMaskOffsetPosX(stage.Progress,stage.Goal); _progressMaskRect.offsetMax=offset; }
        PlayMachine(manager.IsUnlock ? "idle" : "static",true);
    }
    public static float GetTargetMaskOffsetPosX(int progress,int goal) => progress<=0 ? 0 : (goal>5 ? NormalMask : FastMask)[Mathf.Clamp(progress-1,0,goal>5 ? 9 : 4)];
    public void OnPointerClick(PointerEventData eventData)
    {
        if(eventData.button==PointerEventData.InputButton.Left) OnClickBtn();
    }
    public void OnClickBtn()
    {
        if(_busy || !isActiveAndEnabled || (_home!=null && !_home.CanOpenLevelBox(this))) return;
        var manager=LevelBoxManager.Ensure(); if(!manager.IsUnlock) return;
        _busy=true;
        try
        {
            Transform parent=GetComponentInParent<Canvas>()?.rootCanvas.transform;
            if(parent==null) { _busy=false; return; }
            int milestone=manager.PendingMilestone;
            if(milestone>0)
            {
                _rewards=RewardDialog.ShowLevelBox(milestone,parent,_chest,true).GetAwaiter().GetResult();
                if(_rewards==null) { _busy=false; return; }
                _rewards.Closed+=OnRewardClosed;
                manager.OnViewed(); PlayMachine("last",false);
                var last=_machineSpine?.SkeletonData?.FindAnimation("last");
                StartCoroutine(ChestFallback(_rewards,last==null ? 0 : last.Duration));
                PlaySound(_chestAudioClip);
            }
            else
            {
                _progress=Dialog.ShowContent<LevelBoxDialogContent>(parent,LevelBoxManager.PATH).GetAwaiter().GetResult();
                _progressRoot=_progress.GetComponentInParent<Dialog>().gameObject;
                RewardDialog.PrepareOverlay(_progressRoot,1300);
                _progress.Dismissed+=OnProgressClosed;
                manager.OnViewed();
            }
        }
        catch(Exception error) { _busy=false; Debug.LogWarning("LevelBox UI unavailable: "+error.Message); }
    }
    private void OnRewardClosed() { if(_rewards!=null) _rewards.Closed-=OnRewardClosed; _rewards=null; _busy=false; if(isActiveAndEnabled) RefreshProgress(); }
    private void OnProgressClosed(int result)
    {
        if(_progress!=null) _progress.Dismissed-=OnProgressClosed;
        _progress=null; _busy=false;
        if(isActiveAndEnabled) RefreshProgress();
    }
    public UniTask PlayAnims()
    {
        if(!isActiveAndEnabled) return UniTask.CompletedTask;
        var manager=LevelBoxManager.Ensure(); var old=manager.GetStageInfo(manager.LastViewedLevel);
        RefreshProgress();
        if(_progressMaskRect!=null) { var p=_progressMaskRect.offsetMax; p.x=GetTargetMaskOffsetPosX(old.Progress,old.Goal); _progressMaskRect.offsetMax=p; }
        PlayMachine("first",false); manager.OnViewed();
        StartCoroutine(ProgressFallback());
        return UniTask.CompletedTask;
    }
    private IEnumerator ChestFallback(RewardDialog owner,float duration)
    {
        if(duration>0) yield return new WaitForSecondsRealtime(duration);
        if(owner!=null && _rewards==owner && isActiveAndEnabled) owner.BeginChestAnimation();
    }
    private IEnumerator ProgressFallback()
    {
        var first=_machineSpine?.SkeletonData?.FindAnimation("first");
        if(first!=null) yield return new WaitForSecondsRealtime(first.Duration);
        if(isActiveAndEnabled && !HasDialogOpen) RefreshProgress();
    }
    private void PlayMachine(string name,bool loop)
    {
        if(_machineSpine?.SkeletonData?.FindAnimation(name)!=null)
        {
            _machineSpine.AnimationState.SetAnimation(0,name,loop);
            _machineSpine.Update(0);
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
                if(_progressMaskRect!=null) { _maskTween?.Kill(); _maskTween=DOTween.To(()=>_progressMaskRect.offsetMax.x,x=> { var p=_progressMaskRect.offsetMax; p.x=x; _progressMaskRect.offsetMax=p; },GetTargetMaskOffsetPosX(stage.Progress,stage.Goal),.5f).SetUpdate(true); }
                break;
            case "text_scale": _progressText?.transform.DOPunchScale(Vector3.one*.15f,.25f).SetUpdate(true); break;
            case "rocker": PlaySound(_rockerAudioClip); break;
            case "replace": if(_rewards!=null && _rewards.RewardSaved) { PlaySound(_progressFullAudioClip); _rewards.BeginChestAnimation(); } break;
        }
    }
    private void PlaySound(AudioClip clip) { if(clip!=null && AudioManager.Current!=null) AudioManager.Current.PlayOneShot(clip,gameObject); }
    private void OnDisable()
    {
        StopAllCoroutines(); _maskTween?.Kill(); _maskTween=null;
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
