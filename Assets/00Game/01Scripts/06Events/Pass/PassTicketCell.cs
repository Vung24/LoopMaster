using Cysharp.Threading.Tasks;
using Sugar.UI;
using UnityEngine;

namespace Snui
{
	public class PassTicketCell : TableViewCell
	{
        public static int Height = 140;
        protected override void Awake()
        {
            base.Awake();
            var button = GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (button != null) button.onClick.AddListener(() =>
                PassPurchaseDialogContent.Show(transform.GetComponentInParent<Canvas>()?.rootCanvas.transform, "pass_ticket").Forget());
        }
	}
}
