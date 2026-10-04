using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class RewardCell : MonoBehaviour
	{
        public RewardItem RewardItem;
        public AudioClip CollectSound;
        private Image _icon;
        private TMP_Text _count;
        private void Awake()
        {
            _icon = HierarchyReferenceCache.Component<Image>(HierarchyReferenceCache.Descendant(transform,"Icon"));
            _count = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Descendant(transform,"Count"));
            foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            if (RewardItem != null && RewardItem.Type != null) Init(RewardItem,false,false).Forget();
        }
        public static UniTask<RewardCell> Show(RewardItem item, Transform parent, bool showBackLight=true, bool isRewardDialog=false, bool canShowInfoIcon=false)
        {
            var prefab=DataPrefabLibrary.Current?.RewardCellPrefab;
            if(prefab==null || parent==null) return UniTask.FromResult<RewardCell>(null);
            var cell=Instantiate(prefab,parent,false).GetComponent<RewardCell>();
            cell.Init(item,showBackLight,false,isRewardDialog,canShowInfoIcon).Forget();
            return UniTask.FromResult(cell);
        }
        public UniTask Init(RewardItem item, bool showBackLight=true, bool fadeIn=true, bool isInRewardDialog=false, bool canShowInfoIcon=false)
        {
            RewardItem=item;
            Snui_Text.EnsureFontAssets(transform);
            var ribbon=HierarchyReferenceCache.Descendant(transform,"Ribbon");
            if(ribbon!=null) ribbon.gameObject.SetActive(false); // no offline heart timer
            var info=HierarchyReferenceCache.Descendant(transform,"InfoIcon"); if(info!=null) info.gameObject.SetActive(false);
            SetBackLight(showBackLight);
            if(item==null || item.Type==null) return UniTask.CompletedTask;
            if(_icon!=null) { _icon.sprite=DataPrefabLibrary.Current?.ResolveRewardIcon(item.Type.GetIconPath()); _icon.enabled=_icon.sprite!=null; }
            bool heart=item.Type==RewardType.InfinityHeart || item.Type==RewardType.Heart;
            if(_count!=null)
            {
                _count.gameObject.SetActive(true);
                _count.text=heart ? "∞ HEART — OFFLINE" : item.Type==RewardType.Coin ? item.Count.ToString() : "x"+item.Count;
                if(_icon==null || _icon.sprite==null) _count.text=heart ? "∞ HEART — OFFLINE" : item.Type.Key.ToUpperInvariant()+" "+item.Count;
                if(heart) { _count.enableAutoSizing=true; _count.fontSizeMax=30; _count.fontSizeMin=16; (_count.transform as RectTransform).sizeDelta=new Vector2(260,58); }
            }
            return UniTask.CompletedTask;
        }
        public void SetBackLight(bool active)
        { var light=HierarchyReferenceCache.Descendant(transform,"BackLight"); if(light!=null) light.gameObject.SetActive(active); }
        public void PlayCollectSound() { if(AudioManager.Current!=null) AudioManager.Current.PlayOneShot(CollectSound,gameObject); }
	}
}
