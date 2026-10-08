using Sugar.UI;
using UnityEngine;

namespace Snui
{
	public class PassFinalBundleCell : TableViewCell
	{
        public static int Height = 545;
        public PassFinalBundleItem FinalBundleItem { get; private set; }
        protected override void Awake() { base.Awake(); FinalBundleItem = GetComponentInChildren<PassFinalBundleItem>(true); }
        public void Init(int displayStage = int.MaxValue) => FinalBundleItem?.Refresh(displayStage);
	}
}
