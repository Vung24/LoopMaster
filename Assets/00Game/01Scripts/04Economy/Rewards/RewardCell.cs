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
        [SerializeField] private Image _icon;
        [SerializeField] private bool _useNativeIconSize;
        [SerializeField] private TMP_Text _count;
        [SerializeField] private GameObject _ribbon;
        [SerializeField] private TMP_Text _infinityText;
        [SerializeField] private GameObject _ribbonIcon;
        [SerializeField] private GameObject _infoIcon;
        [SerializeField] private GameObject _backLight;
        private void Awake()
        {
            if (_icon == null) 
                _icon = HierarchyReferenceCache.ChildComponent<Image>(transform, "Icon") 
                    ?? HierarchyReferenceCache.DescendantComponent<Image>(transform, "Icon");
            if (_count == null) 
                _count = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Count") 
                    ?? HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Count");
            if (_ribbon == null)
            {
                var r = HierarchyReferenceCache.Child(transform, "Ribbon") 
                    ?? HierarchyReferenceCache.Descendant(transform, "Ribbon");
                if (r != null) _ribbon = r.gameObject;
            }
            if (_ribbon != null)
            {
                if (_infinityText == null)
                    _infinityText = HierarchyReferenceCache.ChildComponent<TMP_Text>(_ribbon.transform, "Time") 
                        ?? HierarchyReferenceCache.DescendantComponent<TMP_Text>(_ribbon.transform, "Time");
                if (_ribbonIcon == null)
                {
                    var ri = HierarchyReferenceCache.Child(_ribbon.transform, "Icon") 
                        ?? HierarchyReferenceCache.Descendant(_ribbon.transform, "Icon");
                    if (ri != null) _ribbonIcon = ri.gameObject;
                }
            }
            if (_infoIcon == null)
            {
                var info = HierarchyReferenceCache.Child(transform, "InfoIcon") 
                    ?? HierarchyReferenceCache.Descendant(transform, "InfoIcon");
                if (info != null) _infoIcon = info.gameObject;
            }
            if (_backLight == null)
            {
                var bl = HierarchyReferenceCache.Child(transform, "BackLight") 
                    ?? HierarchyReferenceCache.Descendant(transform, "BackLight");
                if (bl != null) _backLight = bl.gameObject;
            }
            foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            if (RewardItem != null && RewardItem.Type != null) Init(RewardItem,false,false).Forget();
        }
        public static UniTask<RewardCell> Show(RewardItem item, Transform parent, bool showBackLight=true, bool isRewardDialog=false, bool canShowInfoIcon=false)
        {
            var prefab=PrefabLibrary.Current?.RewardCellPrefab;
            if(prefab==null || parent==null) return UniTask.FromResult<RewardCell>(null);
            var cell=Instantiate(prefab,parent,false).GetComponent<RewardCell>();
            cell.Init(item,showBackLight,false,isRewardDialog,canShowInfoIcon).Forget();
            return UniTask.FromResult(cell);
        }
        public UniTask Init(RewardItem item, bool showBackLight=true, bool fadeIn=true, bool isInRewardDialog=false, bool canShowInfoIcon=false)
        {
            RewardItem=item;
            Snui_Text.EnsureFontAssets(transform);
            if(_infoIcon!=null) _infoIcon.SetActive(false);
            SetBackLight(showBackLight);
            if(item==null || item.Type==null) return UniTask.CompletedTask;
            if (_icon != null)
            {
                _icon.sprite = PrefabLibrary.Current?.ResolveRewardIcon(item.Type.GetIconPath());
                _icon.enabled = _icon.sprite != null;
                if ((_useNativeIconSize || isInRewardDialog) && _icon.sprite != null)
                {
                    _icon.type = Image.Type.Simple;
                    _icon.preserveAspect = true;
                    _icon.SetNativeSize();
                }
            }

            bool isTimeLimited = item.Type.IsTimeLimited();
            if (isTimeLimited)
            {
                if (_count != null) _count.gameObject.SetActive(false);
                if (_ribbon != null)
                {
                    _ribbon.SetActive(true);
                    if (_ribbonIcon != null)
                        _ribbonIcon.SetActive(item.Type != RewardType.InfinityHeart);
                    if (_infinityText != null)
                    {
                        _infinityText.gameObject.SetActive(true);
                        _infinityText.text = TimeFormatter.FormatRewardDuration(item.Count);
                    }
                }
                else if (_count != null)
                {
                    _count.gameObject.SetActive(true);
                    _count.text = TimeFormatter.FormatRewardDuration(item.Count);
                }
            }
            else
            {
                if (_ribbon != null) _ribbon.SetActive(false);
                if (_count != null)
                {
                    _count.gameObject.SetActive(!item.Type.IsCardPack());
                    _count.text = item.Type == RewardType.Coin ? item.Count.ToString("N0") : "x" + item.Count;
                    if (_icon == null || _icon.sprite == null)
                        _count.text = item.Type.Key.ToUpperInvariant() + " " + item.Count;
                }
            }
            return UniTask.CompletedTask;
        }
        public void SetBackLight(bool active)
        { if(_backLight!=null) _backLight.SetActive(active); }
        public void SetTranslucence(float alpha = 0.5f)
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = alpha;
        }
        public void PlayCollectSound() { if(AudioManager.Current!=null) AudioManager.Current.PlayOneShot(CollectSound,gameObject); }
	}
}
