using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public sealed class PassPurchaseItem : PassToastItemBase
	{
        [SerializeField] private Button _claimBtn;
        [SerializeField] private Transform _getMark;
        [SerializeField] private Transform _lockMark;
        [SerializeField] private Spine.Unity.SkeletonGraphic _lockGraphic;
        [SerializeField] private Transform _heartNode;
        [SerializeField] private Transform _boxNode;
        [SerializeField] private TMPro.TMP_Text _countText;
        [SerializeField] private UnityEngine.UI.Image _heartImage;

        protected override void Awake()
        {
            base.Awake();
            if (_claimBtn == null) _claimBtn = PassUI.Bind(transform, "ClaimBtn", Purchase);
            else { _claimBtn.onClick.RemoveListener(Purchase); _claimBtn.onClick.AddListener(Purchase); }
            if (_getMark == null) _getMark = HierarchyReferenceCache.Child(transform, "Get") ?? HierarchyReferenceCache.Descendant(transform, "Get");
            if (_lockMark == null) _lockMark = HierarchyReferenceCache.Child(transform, "Lock") ?? HierarchyReferenceCache.Descendant(transform, "Lock");
            if (_lockGraphic == null && _lockMark != null) _lockGraphic = _lockMark.GetComponent<Spine.Unity.SkeletonGraphic>();
            if (_heartNode == null) _heartNode = HierarchyReferenceCache.Child(transform, "Heart") ?? HierarchyReferenceCache.Descendant(transform, "Heart");
            if (_boxNode == null) _boxNode = HierarchyReferenceCache.Child(transform, "Box") ?? HierarchyReferenceCache.Descendant(transform, "Box");
            if (_countText == null) _countText = HierarchyReferenceCache.ChildComponent<TMPro.TMP_Text>(transform, "Count") ?? HierarchyReferenceCache.DescendantComponent<TMPro.TMP_Text>(transform, "Count");
            if (_heartImage == null && _heartNode != null) _heartImage = _heartNode.GetComponent<UnityEngine.UI.Image>();
            if (_button != null) _button.onClick.AddListener(Purchase);
        }
        public void Init(int row) => Refresh();
        public void Refresh()
        {
            bool vip = Managers.Pass.IsVip();
            if (_getMark != null) _getMark.gameObject.SetActive(vip);
            else PassUI.Active(transform, "Get", vip);

            if (_claimBtn != null) _claimBtn.gameObject.SetActive(!vip && Managers.Pass.CanPurchaseVIP());
            else PassUI.Active(transform, "ClaimBtn", !vip && Managers.Pass.CanPurchaseVIP());

            if (_lockMark != null) _lockMark.gameObject.SetActive(!vip);
            else PassUI.Active(transform, "Lock", !vip);

            if (!vip)
            {
                if (_lockGraphic != null) PassUI.Spine(_lockGraphic, "lock", true);
                else PassUI.Spine(PassUI.Node<Spine.Unity.SkeletonGraphic>(transform, "Lock"), "lock", true);
            }

            if (_heartNode != null) _heartNode.gameObject.SetActive(Managers.Pass.IsHeartEnable());
            else PassUI.Active(transform, "Heart", Managers.Pass.IsHeartEnable());

            if (_boxNode != null) _boxNode.gameObject.SetActive(Managers.Pass.IsTeamEnable());
            else PassUI.Active(transform, "Box", Managers.Pass.IsTeamEnable());

            if (_countText != null) _countText.text = "8";
            else PassUI.Text(transform, "Count", "8");

            if (_heartImage != null) PassUI.NativeIcon(_heartImage);
            else PassUI.NativeIcon(PassUI.Node<UnityEngine.UI.Image>(transform, "Heart"));
        }
        private void Purchase()
        {
            if (Managers.Pass.CanPurchaseVIP())
                PassPurchaseDialogContent.Show(transform.GetComponentInParent<Canvas>()?.rootCanvas.transform, "pass_first_stage").Forget();
        }
        public UniTask PlayUnlockAnim() { Refresh(); PassUI.Animate(transform); return UniTask.CompletedTask; }
	}
}
