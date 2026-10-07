using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class RewardDialog : MonoBehaviour
	{
        public const string PATH="Snui/Common/Prefabs/RewardDialog.prefab";
        public GameObject RewardCellPrefab, SpecialSparksPrefab, Fireworks;
        public AudioClip RewardSound, ChestSound, FireworksClip, RewardsPopAfterCardsClip;
        private readonly List<RewardCell> _cells=new List<RewardCell>();
        [SerializeField] private Button _fullBtn;
        [SerializeField] private TMP_Text _desc;
        [SerializeField] private Transform _safeArea;
        [SerializeField] private Image _mask;
        private SkeletonGraphic _chest;
        private ClaimCoinAnim _coins;
        private int _milestone;
        private bool _saved, _closing, _showing, _revealed;
        private Coroutine _animation;
        public event Action Closed;
        public bool RewardSaved => _saved;
        // Presentation receipt only: SpecialStreak has already journaled these rewards.
        public static UniTask<RewardDialog> ShowSpecialStreakRewards(List<RewardItem> items, Transform parent)
        {
            var prefab = RecoveryPrefabLibrary.Current?.Resolve(PATH);
            if (parent == null || prefab == null || items == null || items.Count == 0)
                return UniTask.FromResult<RewardDialog>(null);
            var instance = Instantiate(prefab, parent, false);
            var dialog = instance.GetComponent<RewardDialog>();
            if (dialog == null) { Destroy(instance); return UniTask.FromResult<RewardDialog>(null); }
            PrepareOverlay(instance, 1350);
            dialog._saved = true;
            var layout = RewardLayoutHelper.GetRewardsLayoutPos(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var cell = RewardCell.Show(items[i], dialog._safeArea, true, true).GetAwaiter().GetResult();
                if (cell == null) continue;
                cell.transform.localPosition = new Vector3(layout.Item2[i].x, layout.Item2[i].y, 0f);
                cell.transform.localScale = Vector3.one * layout.Item1;
                dialog._cells.Add(cell);
            }
            HudContainer.Current?.CoinHud?.SetAutoUpdate(true);
            Snui_HeartHud.TrySetAutoUpdate(true);
            dialog.Reveal();
            return UniTask.FromResult(dialog);
        }
        public static UniTask<RewardDialog> ShowLevelBox(int milestone, Transform parent, SkeletonGraphic chest=null, bool waitForMachine=false)
        {
            if(parent==null) return UniTask.FromResult<RewardDialog>(null);
            foreach(var existing in parent.GetComponentsInChildren<RewardDialog>(true))
                if(existing.isActiveAndEnabled) return UniTask.FromResult(existing);
            var prefab=RecoveryPrefabLibrary.Current?.Resolve(PATH);
            if(prefab==null) { Debug.LogWarning("Recovered RewardDialog prefab missing; box remains pending."); return UniTask.FromResult<RewardDialog>(null); }
            // Native ShowWithChest commits BEFORE instantiation/animation.
            bool saved=LevelBoxManager.Ensure().TryOpen(milestone,out var result);
            var instance=Instantiate(prefab,parent,false);
            var dialog=instance.GetComponent<RewardDialog>();
            if(dialog==null) { Destroy(instance); return UniTask.FromResult<RewardDialog>(null); }
            PrepareOverlay(instance,1300);
            dialog.Initialize(milestone,saved,chest,waitForMachine);
            return UniTask.FromResult(dialog);
        }
        public static void PrepareOverlay(GameObject root,int order)
        {
            var canvas=root.GetComponent<Canvas>(); if(canvas==null) canvas=root.AddComponent<Canvas>(); canvas.overrideSorting=true; canvas.sortingOrder=order;
            if(root.GetComponent<GraphicRaycaster>()==null) root.AddComponent<GraphicRaycaster>();
            root.transform.SetAsLastSibling(); Snui_Text.EnsureFontAssets(root.transform); GameResultUI.EnsureEventSystem();
        }
        private void Awake()
        {
            if (_fullBtn == null) _fullBtn = GetComponent<Button>();
            if (_fullBtn == null) _fullBtn = gameObject.AddComponent<Button>();
            _fullBtn.onClick.AddListener(OnClickReceive);
            if (_safeArea == null) _safeArea = HierarchyReferenceCache.Descendant(transform,"SafeArea") ?? transform;
            if (_desc == null) _desc = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Descendant(transform,"Text"));
            if (_mask == null) _mask = HierarchyReferenceCache.Component<Image>(HierarchyReferenceCache.Descendant(transform,"Mask"));
        }
        private void Initialize(int milestone,bool saved,SkeletonGraphic sourceChest,bool waitForMachine)
        {
            _milestone=milestone; _saved=saved;
            var layout=RewardLayoutHelper.GetRewardsLayoutPos(LevelBoxManager.Ensure().GetMilestoneRewards(milestone).Count);
            int index=0;
            foreach(var item in LevelBoxManager.Ensure().GetMilestoneRewards(milestone))
            {
                var cell=RewardCell.Show(item,_safeArea,true,true).GetAwaiter().GetResult();
                if(cell==null) continue;
                cell.transform.localPosition=new Vector3(layout.Item2[index].x,layout.Item2[index].y,0);
                cell.transform.localScale=Vector3.one*layout.Item1; _cells.Add(cell); index++;
            }
            HudContainer.Current?.CoinHud?.SetAutoUpdate(true);
            if(!saved) { SetDescription("REWARD NOT SAVED — TAP TO RETRY"); return; }
            if(sourceChest!=null)
            {
                _chest=Instantiate(sourceChest,_safeArea,false); _chest.gameObject.SetActive(true);
                _chest.transform.localPosition=new Vector3(0,-80,0); _chest.UnscaledTime=true;
                if(_chest.AnimationState!=null) _chest.AnimationState.Event+=OnChestEvent;
            }
            _showing=true; foreach(var cell in _cells) cell.gameObject.SetActive(false);
            if(!waitForMachine) BeginChestAnimation();
        }
        public void BeginChestAnimation()
        {
            if(!_saved || _revealed || _closing || !isActiveAndEnabled || _animation!=null) return;
            _animation=StartCoroutine(ShowChest());
        }
        private void SetDescription(string text) { if(_desc!=null) _desc.text=text; }
        private IEnumerator ShowChest()
        {
            _showing=true; foreach(var cell in _cells) cell.gameObject.SetActive(false);
            PlaySound(ChestSound);
            var animation=_chest?.SkeletonData?.FindAnimation("jump");
            if(animation!=null) _chest.AnimationState.SetAnimation(0,animation,false);
            else { Reveal(); yield break; }
            float remaining=animation.Duration;
            while(_showing && remaining>0) { remaining-=Time.unscaledDeltaTime; yield return null; }
            if(_showing) Reveal(); // Missing Spine event cannot strand the receipt/UI.
        }
        private void OnChestEvent(Spine.TrackEntry track, Spine.Event evt)
        {
            if(!isActiveAndEnabled || _closing || evt?.Data==null) return;
            if(evt.Data.Name=="rewards") Reveal();
            else if(evt.Data.Name=="particle" && SpecialSparksPrefab!=null && _chest!=null)
            { var sparks=Instantiate(SpecialSparksPrefab,_chest.transform,false); Destroy(sparks,2); }
        }
        private void Reveal()
        {
            if(_revealed || _closing || !isActiveAndEnabled) return;
            _revealed=true;
            _showing=false;
            if(_chest!=null) _chest.gameObject.SetActive(false);
            foreach(var cell in _cells) if(cell!=null) cell.gameObject.SetActive(true);
            SetDescription("TAP TO COLLECT"); PlaySound(RewardSound);
        }
        public void OnClickReceive()
        {
            if(_closing || !isActiveAndEnabled) return;
            if(!_saved)
            {
                if(!LevelBoxManager.Ensure().TryOpen(_milestone,out var result)) { SetDescription("REWARD NOT SAVED — TAP TO RETRY"); return; }
                _saved=true; Reveal(); return;
            }
            if(_showing) { Reveal(); return; }
            _closing=true; _animation=StartCoroutine(CollectAndClose());
        }
        private IEnumerator CollectAndClose()
        {
            int coins=0; Vector3 start=transform.position;
            foreach(var cell in _cells) if(cell!=null) { cell.PlayCollectSound(); if(cell.RewardItem.Type==RewardType.Coin) { coins+=cell.RewardItem.Count; start=cell.transform.position; } }
            if(coins>0 && HudContainer.Current?.CoinHud!=null)
            {
                HudContainer.TrySetTop(1301);
                _coins=gameObject.AddComponent<ClaimCoinAnim>();
                // Hide the opaque mask while coins fly on the existing HUD flight layer.
                if(_mask!=null) _mask.color=new Color(0,0,0,.15f);
                yield return _coins.Play(coins,transform.parent,start,HudContainer.Current.CoinHud,null,1302);
            }
            Close();
        }
        private void PlaySound(AudioClip clip) { if(clip!=null && AudioManager.Current!=null) AudioManager.Current.PlayOneShot(clip,gameObject); }
        private void Update() { if(Input.GetKeyDown(KeyCode.Escape)) OnAndroidBackButtonPressed(); }
        public bool OnAndroidBackButtonPressed() { if(!isActiveAndEnabled) return false; Close(); return true; }
        public void Close() { gameObject.SetActive(false); Destroy(gameObject); }
        private void OnDisable()
        {
            _closing=true; StopAllCoroutines(); _animation=null; _coins?.Cancel();
            if(_chest!=null && _chest.AnimationState!=null) _chest.AnimationState.Event-=OnChestEvent;
            // Back may close a failed attempt whose receipt was saved but whose
            // final journal cleanup failed. Resolve it before returning to Home.
            InventoryManager.Current?.RecoverRewardJournal();
            HudContainer.Current?.CoinHud?.SetAutoUpdate(true); HudContainer.TryRevertToOriginLayer();
            Closed?.Invoke(); Closed=null;
        }
        private void OnDestroy() { if(_fullBtn!=null) _fullBtn.onClick.RemoveListener(OnClickReceive); }
	}
}
