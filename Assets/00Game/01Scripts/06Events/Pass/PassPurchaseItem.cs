using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui
{
	public sealed class PassPurchaseItem : PassToastItemBase
	{
        protected override void Awake()
        {
            base.Awake();
            PassUI.Bind(transform, "ClaimBtn", Purchase);
            if (_button != null) _button.onClick.AddListener(Purchase);
        }
        public void Init(int row) => Refresh();
        public void Refresh()
        {
            bool vip = Managers.Pass.IsVip();
            PassUI.Active(transform, "Get", vip);
            PassUI.Active(transform, "ClaimBtn", !vip && Managers.Pass.CanPurchaseVIP());
            PassUI.Active(transform, "Lock", !vip);
            if (!vip) PassUI.Spine(PassUI.Node<Spine.Unity.SkeletonGraphic>(transform, "Lock"), "lock", true);
            PassUI.Active(transform, "Heart", Managers.Pass.IsHeartEnable());
            PassUI.Active(transform, "Box", Managers.Pass.IsTeamEnable());
            PassUI.Text(transform, "Count", "8");
            PassUI.NativeIcon(PassUI.Node<UnityEngine.UI.Image>(transform, "Heart"));
        }
        private void Purchase()
        {
            if (Managers.Pass.CanPurchaseVIP())
                PassPurchaseDialogContent.Show(transform.GetComponentInParent<Canvas>()?.rootCanvas.transform, "pass_first_stage").Forget();
        }
        public UniTask PlayUnlockAnim() { Refresh(); PassUI.Animate(transform); return UniTask.CompletedTask; }
	}
}
