using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui
{
	public class PassLockItem : PassToastItemBase
	{
        protected override void Awake()
        {
            base.Awake();
            if (_button != null) _button.onClick.AddListener(() =>
                PassPurchaseDialogContent.Show(transform.GetComponentInParent<Canvas>()?.rootCanvas.transform, "pass_lock").Forget());
        }
	}
}
