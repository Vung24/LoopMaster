using Cysharp.Threading.Tasks;
using Sugar.UI;
using UnityEngine;

namespace Snui
{
	public class PassTicketCell : TableViewCell
	{
        public static int Height = 140;
        public void Refresh()
        {
            var button = GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (button != null) button.interactable = Managers.Pass != null && Managers.Pass.CanPurchaseVIP();
        }
        protected override void Awake()
        {
            base.Awake();
            var button = GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (button != null) button.onClick.AddListener(() =>
                PassPurchaseDialogContent.Show(transform.GetComponentInParent<Canvas>()?.rootCanvas.transform, "pass_ticket").Forget());
        }
	}
}
