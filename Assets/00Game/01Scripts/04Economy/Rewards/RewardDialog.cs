using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class RewardDialog : BasePopup
	{
        public const string PATH="RewardDialog";
        public GameObject RewardCellPrefab, SpecialSparksPrefab, Fireworks;
        public AudioClip RewardSound, ChestSound, FireworksClip, RewardsPopAfterCardsClip;
        private readonly List<RewardCell> _cells=new List<RewardCell>();
        [SerializeField] private Button _fullBtn;
        [SerializeField] private TMP_Text _desc;
        [SerializeField] private Transform _safeArea;
        [SerializeField] private Image _mask;
        private SkeletonGraphic _chest;
        private ClaimCoinAnim _coins;
        private UniTask _itemFlights;
        private int _milestone;
        private bool _saved, _closing, _showing, _revealed;
        private Coroutine _animation;
        private bool _levelBoxPresentation;
        private Transform _titleNode;
        private CanvasGroup _titleGroup, _buttonTextGroup;
        private Canvas _machineCanvas;
        private bool _machineOverrideSorting;
        private int _machineSortingLayer, _machineSortingOrder;
        private readonly List<Tween> _presentationTweens=new List<Tween>();
        public override bool IsBusy => _closing;
        public bool RewardSaved => _saved;
        public static async UniTask<RewardDialog> ShowSpecialStreakRewards(List<RewardItem> items, Transform parent)
        {
            if (parent == null || items == null || items.Count == 0)
                return null;
            var instance = await PopupAssets.Instantiate(PATH, parent, parent.GetCancellationTokenOnDestroy());
            var dialog = instance.GetComponent<RewardDialog>();
            if (dialog == null) { Destroy(instance); return null; }
            PrepareOverlay(instance, 1350);
            dialog._saved = true;
            var layout = RewardLayoutHelper.GetRewardsLayoutPos(items.Count);
            bool hasCoins = false;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].Type == RewardType.Coin) hasCoins = true;
                var cell = RewardCell.Show(items[i], dialog._safeArea, true, true).GetAwaiter().GetResult();
                if (cell == null) continue;
                cell.transform.localPosition = new Vector3(layout.Item2[i].x, layout.Item2[i].y, 0f);
                cell.transform.localScale = Vector3.one * layout.Item1;
                dialog._cells.Add(cell);
            }
            if (hasCoins)
            {
                HudContainer.Current?.CoinHud?.SetAutoUpdate(false);
                Snui_CoinHud.SetCurrentAutoUpdate(false);
            }
            else
            {
                HudContainer.Current?.CoinHud?.SetAutoUpdate(true);
            }
            Snui_HeartHud.SetCurrentAutoUpdate(true);
            dialog.Reveal();
            return dialog;
        }
        public static async UniTask<RewardDialog> ShowLevelBox(int milestone, Transform parent, SkeletonGraphic chest=null, bool waitForMachine=false, bool receiveImmediately=false, CancellationToken cancellationToken=default)
        {
            if(parent==null) return null;
            foreach(var existing in parent.GetComponentsInChildren<RewardDialog>(true))
                if(existing.isActiveAndEnabled) return existing;
            var instance=await PopupAssets.Instantiate(PATH,parent,cancellationToken);
            var dialog=instance.GetComponent<RewardDialog>();
            if(dialog==null) { Destroy(instance); return null; }
            bool saved=LevelBoxManager.Ensure().TryOpen(milestone,out var result);
            PrepareOverlay(instance,1300);
            dialog.Initialize(milestone,saved,chest,waitForMachine,receiveImmediately);
            return dialog;
        }

        public static async UniTask<RewardDialog> ShowWithChest(List<RewardItem> items, string eventKey = null, string title = null, Transform parent = null, SkeletonGraphic chest = null, bool waitForMachine = false)
        {
            var p = parent ?? Utils.GetCurrentUiCanvas()?.transform;
            if (p == null) return null;
            var instance = await PopupAssets.Instantiate(PATH, p, p.GetCancellationTokenOnDestroy());
            var dialog = instance.GetComponent<RewardDialog>();
            if (dialog == null) { Destroy(instance); return null; }
            PrepareOverlay(instance, 1300);
            dialog._saved = true;
            if (!string.IsNullOrEmpty(title) && dialog._desc != null)
            {
                dialog._desc.text = title;
            }
            bool hasCoins = false;
            if (items != null)
            {
                var layout = RewardLayoutHelper.GetRewardsLayoutPos(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null && items[i].Type == RewardType.Coin) hasCoins = true;
                    var cell = await RewardCell.Show(items[i], dialog._safeArea, true, true);
                    if (cell != null)
                    {
                        cell.transform.localPosition = new Vector3(layout.Item2[i].x, layout.Item2[i].y, 0f);
                        cell.transform.localScale = Vector3.one * layout.Item1;
                        dialog._cells.Add(cell);
                    }
                }
            }
            if (hasCoins)
            {
                HudContainer.Current?.CoinHud?.SetAutoUpdate(false);
                Snui_CoinHud.SetCurrentAutoUpdate(false);
            }
            if (chest != null)
            {
                dialog._chest = Instantiate(chest, dialog._safeArea, false);
                dialog._chest.gameObject.SetActive(true);
                dialog._chest.transform.localPosition = new Vector3(0, -80, 0);
                dialog._chest.UnscaledTime = true;
            }
            dialog._showing = true;
            foreach (var cell in dialog._cells) cell.gameObject.SetActive(false);
            if (!waitForMachine) dialog.BeginChestAnimation();
            return dialog;
        }

        public async UniTask PlayChestAnim(SkeletonGraphic chest, string animName, float duration = 1f, float delay = 0f, float fade = 0f)
        {
            if (chest != null && chest.AnimationState != null)
            {
                chest.AnimationState.SetAnimation(0, animName, false);
            }
            await UniTask.WaitForSeconds(duration);
            Reveal();
        }

        public static void PrepareOverlay(GameObject root,int order)
        {
            var canvas=root.GetComponent<Canvas>(); if(canvas==null) canvas=root.AddComponent<Canvas>(); canvas.overrideSorting=true; canvas.sortingOrder=order;
            if(root.GetComponent<GraphicRaycaster>()==null) root.AddComponent<GraphicRaycaster>();
            root.transform.SetAsLastSibling(); Snui_Text.EnsureFontAssets(root.transform);
        }
        protected override void Awake()
        {
            base.Awake();
            if (_fullBtn == null) _fullBtn = GetComponent<Button>();
            if (_fullBtn == null) _fullBtn = gameObject.AddComponent<Button>();
            _fullBtn.onClick.AddListener(OnClickReceive);
            if (_safeArea == null) _safeArea = HierarchyReferenceCache.Child(transform, "SafeArea") ?? HierarchyReferenceCache.Descendant(transform, "SafeArea") ?? transform;
            if (_desc == null) _desc = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Text") ?? HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Text");
            if (_mask == null) _mask = HierarchyReferenceCache.ChildComponent<Image>(transform, "Mask") ?? HierarchyReferenceCache.DescendantComponent<Image>(transform, "Mask");
        }
        private void Initialize(int milestone,bool saved,SkeletonGraphic sourceChest,bool waitForMachine,bool receiveImmediately)
        {
            _milestone=milestone; _saved=saved;
            _levelBoxPresentation=!receiveImmediately;
            var layout=RewardLayoutHelper.GetRewardsLayoutPos(LevelBoxManager.Ensure().GetMilestoneRewards(milestone).Count);
            int index=0;
            bool hasCoins = false;
            foreach(var item in LevelBoxManager.Ensure().GetMilestoneRewards(milestone))
            {
                if (item != null && item.Type == RewardType.Coin) hasCoins = true;
                var cell=RewardCell.Show(item,_safeArea,true,true).GetAwaiter().GetResult();
                if(cell==null) continue;
                cell.transform.localPosition=new Vector3(layout.Item2[index].x,layout.Item2[index].y,0);
                cell.transform.localScale=Vector3.one*layout.Item1; _cells.Add(cell); index++;
            }
            if (hasCoins)
            {
                HudContainer.Current?.CoinHud?.SetAutoUpdate(false);
                Snui_CoinHud.SetCurrentAutoUpdate(false);
            }
            else
            {
                HudContainer.Current?.CoinHud?.SetAutoUpdate(true);
            }
            Snui_HeartHud.SetCurrentAutoUpdate(true);
            if(!saved) { SetDescription("REWARD NOT SAVED — TAP TO RETRY"); return; }
            if(receiveImmediately) { Reveal(); return; }
            PrepareLevelBoxPresentation();
            if(sourceChest!=null)
            {
                _chest=Instantiate(sourceChest,_safeArea,false); _chest.gameObject.SetActive(true);
                _chest.transform.position=sourceChest.transform.position; _chest.UnscaledTime=true;
                _chest.Initialize(true);
                foreach(var follower in _chest.GetComponentsInChildren<BoneFollowerGraphic>(true)) follower.Initialize();
                if(_chest.AnimationState!=null) _chest.AnimationState.Event+=OnChestEvent;
                _chest.gameObject.SetActive(false);
            }
            _showing=true; foreach(var cell in _cells) cell.gameObject.SetActive(false);
            if(!waitForMachine) BeginChestAnimation();
        }
        public void BeginChestAnimation()
        {
            if(!_saved || _revealed || _closing || !isActiveAndEnabled || _animation!=null) return;
            RestoreMachineSorting();
            _animation=StartCoroutine(ShowChest());
        }
        public void FocusMachine(Transform machine)
        {
            if(machine==null || !_saved || _revealed || _closing || _animation!=null || !isActiveAndEnabled) return;
            RestoreMachineSorting();
            var overlay=GetComponent<Canvas>();
            if(overlay==null) return;
            _machineCanvas=machine.GetComponent<Canvas>();
            if(_machineCanvas==null)
                _machineCanvas=machine.gameObject.AddComponent<Canvas>();
            _machineOverrideSorting=_machineCanvas.overrideSorting;
            _machineSortingLayer=_machineCanvas.sortingLayerID;
            _machineSortingOrder=_machineCanvas.sortingOrder;
            // Keep the machine and its Spine child renderers above the dim until the chest takes over.
            _machineCanvas.enabled=true;
            _machineCanvas.overrideSorting=true;
            _machineCanvas.sortingLayerID=overlay.sortingLayerID;
            _machineCanvas.sortingOrder=overlay.sortingOrder+1;
        }
        private void RestoreMachineSorting()
        {
            if(_machineCanvas!=null)
            {
                _machineCanvas.overrideSorting=_machineOverrideSorting;
                _machineCanvas.sortingLayerID=_machineSortingLayer;
                _machineCanvas.sortingOrder=_machineSortingOrder;
                // Keep the nested Canvas enabled so Spine submesh renderers continue drawing on Home.
            }
            _machineCanvas=null;
        }
        private void SetDescription(string text) { if(_desc!=null) _desc.text=text; }
        private IEnumerator ShowChest()
        {
            _showing=true; foreach(var cell in _cells) cell.gameObject.SetActive(false);
            PlaySound(ChestSound);
            var animation=_chest?.SkeletonData?.FindAnimation("jump");
            if(animation!=null) { _chest.gameObject.SetActive(true); _chest.AnimationState.SetAnimation(0,animation,false); }
            else { Reveal(); yield break; }
            if(_levelBoxPresentation)
            {
                _presentationTweens.Add(DOTween.Sequence().SetUpdate(true).SetDelay(.13f)
                    .Append(_chest.transform.DOLocalMoveY(237f,.266f).SetEase(Ease.OutQuart))
                    .Append(_chest.transform.DOLocalMoveY(0f,.233f).SetEase(Ease.InQuart)));
            }
            float remaining=animation.Duration;
            while(_showing && remaining>0) { remaining-=Time.unscaledDeltaTime; yield return null; }
            if(_showing) Reveal();
        }
        private void OnChestEvent(Spine.TrackEntry track, Spine.Event evt)
        {
            if(!isActiveAndEnabled || _closing || evt?.Data==null) return;
            if(evt.Data.Name=="rewards") Reveal();
            else if(evt.Data.Name=="particle" && _levelBoxPresentation && _chest!=null)
            {
                var spark=HierarchyReferenceCache.ChildComponent<ParticleSystem>(_chest.transform,"giftBoxSpark");
                if(spark!=null) spark.Play(true);
            }
            else if(evt.Data.Name=="particle" && SpecialSparksPrefab!=null && _chest!=null)
            { var sparks=Instantiate(SpecialSparksPrefab,_chest.transform,false); Destroy(sparks,2); }
        }
        private void Reveal()
        {
            if(_revealed || _closing || !isActiveAndEnabled) return;
            RestoreMachineSorting();
            _revealed=true;
            _showing=false;
            if(_chest!=null) _chest.gameObject.SetActive(false);
            foreach(var cell in _cells) if(cell!=null) cell.gameObject.SetActive(true);
            SetDescription("TAP TO COLLECT"); PlaySound(RewardSound);
            if(_levelBoxPresentation) StartCoroutine(PlayLevelBoxReveal());
        }
        private void PrepareLevelBoxPresentation()
        {
            _titleNode=HierarchyReferenceCache.Child(_safeArea,"Title");
            if(_titleNode!=null)
            {
                _titleGroup=_titleNode.GetComponent<CanvasGroup>();
                if(_titleGroup==null) _titleGroup=_titleNode.gameObject.AddComponent<CanvasGroup>();
                _titleGroup.alpha=0; _titleNode.localScale=Vector3.zero;
            }
            if(_desc!=null)
            {
                _buttonTextGroup=_desc.GetComponent<CanvasGroup>();
                if(_buttonTextGroup==null) _buttonTextGroup=_desc.gameObject.AddComponent<CanvasGroup>();
                _buttonTextGroup.alpha=0;
            }
        }
        private IEnumerator PlayLevelBoxReveal()
        {
            _showing=true;
            var layout=RewardLayoutHelper.GetRewardsLayoutPos(_cells.Count);
            for(int i=0;i<_cells.Count;i++)
            {
                var cell=_cells[i].transform;
                cell.localPosition=Vector3.zero; cell.localScale=Vector3.one*(layout.Item1*.5f);
                _presentationTweens.Add(DOTween.Sequence().SetUpdate(true)
                    .Append(cell.DOLocalMove(new Vector3(layout.Item2[i].x,layout.Item2[i].y,0),.13f).SetEase(Ease.OutCubic))
                    .Join(cell.DOScale(layout.Item1,.13f).SetEase(Ease.InOutSine))
                    .Append(cell.DOScale(layout.Item1*.9f,.13f).SetEase(Ease.InOutSine))
                    .Append(cell.DOScale(layout.Item1,.16f).SetEase(Ease.InOutSine)));
            }
            if(_titleNode!=null)
                _presentationTweens.Add(DOTween.Sequence().SetUpdate(true).SetDelay(.1f)
                    .Append(_titleNode.DOScale(1.08f,.3f).SetEase(Ease.OutSine))
                    .Join(_titleGroup.DOFade(1f,.2f))
                    .Append(_titleNode.DOScale(1f,.23f).SetEase(Ease.InSine)));
            foreach(string name in new[] {"FireworksL","FireworksR"})
            {
                var graphic=HierarchyReferenceCache.ChildComponent<SkeletonGraphic>(_safeArea,name);
                if(graphic==null) continue;
                graphic.Initialize(false); graphic.UnscaledTime=true;
                if(graphic.SkeletonData?.FindAnimation("animation")!=null) graphic.AnimationState.SetAnimation(0,"animation",false);
            }
            yield return new WaitForSecondsRealtime(.45f);
            if(_buttonTextGroup!=null) _presentationTweens.Add(_buttonTextGroup.DOFade(1f,.2f).SetUpdate(true));
            _showing=false;
        }
        public void OnClickReceive()
        {
            if(_closing || !isActiveAndEnabled) return;
            if(!_saved)
            {
                if(!LevelBoxManager.Ensure().TryOpen(_milestone,out var result)) { SetDescription("REWARD NOT SAVED — TAP TO RETRY"); return; }
                _saved=true;
                if(_levelBoxPresentation) PrepareLevelBoxPresentation();
                Reveal(); return;
            }
            if(_showing) { if(!_revealed) Reveal(); return; }
            _closing=true; _animation=StartCoroutine(CollectAndClose());
        }
        private IEnumerator CollectAndClose()
        {
            if(_fullBtn!=null) _fullBtn.interactable=false;
            int coins=0; Vector3 start=transform.position;
            foreach(var cell in _cells)
            {
                if(cell==null || cell.RewardItem?.Type!=RewardType.Coin) continue;
                coins+=cell.RewardItem.Count; start=cell.transform.position;
                cell.SetBackLight(false);
                _presentationTweens.Add(cell.transform.DOScale(Vector3.zero,.2f).SetUpdate(true)
                    .OnComplete(()=> { if(cell!=null) cell.gameObject.SetActive(false); }));
            }
            int order=GetComponent<Canvas>().sortingOrder;
            HudContainer.SetCurrentTop(order+1);
            if(Snui_HeartHud.Current!=null && Snui_HeartHud.Current.isActiveAndEnabled)
                Snui_HeartHud.Current.SetTop(order+1);
            _itemFlights=new RewardItemDismissAnimator().FlyToTarget(_cells,OpenCancellationToken);
            // Native common claim fades the title, prompt and dim while rewards travel.
            yield return new WaitForSecondsRealtime(.1f);
            if(_titleNode==null) _titleNode=HierarchyReferenceCache.Child(_safeArea,"Title");
            FadeClaimNode(_titleNode);
            if(_desc!=null) FadeClaimNode(_desc.transform);
            if(_mask!=null) _presentationTweens.Add(_mask.DOFade(0f,.2f).SetUpdate(true));
            var hud=HudContainer.Current?.CoinHud ?? Snui_CoinHud.Current;
            if(coins>0 && hud!=null && hud.isActiveAndEnabled)
            {
                hud.SetTop(order+1);
                _coins=gameObject.AddComponent<ClaimCoinAnim>();
                yield return _coins.Play(coins,transform,start,hud,null,order+2);
            }
            yield return _itemFlights.ToCoroutine();
            yield return new WaitForSecondsRealtime(.2f);
            ForceClose();
        }
        private void FadeClaimNode(Transform node)
        {
            if(node==null) return;
            var group=node.GetComponent<CanvasGroup>();
            if(group==null) group=node.gameObject.AddComponent<CanvasGroup>();
            _presentationTweens.Add(group.DOFade(0f,.2f).SetUpdate(true));
        }
        private void PlaySound(AudioClip clip) { if(clip!=null && AudioManager.Current!=null) AudioManager.Current.PlayOneShot(clip,gameObject); }
        public bool OnAndroidBackButtonPressed() => TryClose();
        protected override void OnDisable()
        {
            RestoreMachineSorting();
            _closing=true; StopAllCoroutines(); _animation=null; _coins?.Cancel();
            foreach(var tween in _presentationTweens) tween.Kill();
            _presentationTweens.Clear();
            if(_chest!=null && _chest.AnimationState!=null) _chest.AnimationState.Event-=OnChestEvent;
            InventoryManager.Current?.ReplayPendingRewardJournal();
            HudContainer.Current?.CoinHud?.SetAutoUpdate(true); HudContainer.RevertCurrentToOriginLayer();
            Snui_CoinHud.Current?.RevertToOriginLayer(); Snui_HeartHud.SetCurrentAutoUpdate(true);
            if(Snui_HeartHud.Current!=null) Snui_HeartHud.Current.RevertToOriginLayer();
            base.OnDisable();
        }
        protected override void OnDestroy() { if(_fullBtn!=null) _fullBtn.onClick.RemoveListener(OnClickReceive); base.OnDestroy(); }
	}
}
