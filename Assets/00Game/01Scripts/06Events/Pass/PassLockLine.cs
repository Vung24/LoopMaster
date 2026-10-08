using UnityEngine;

namespace Snui
{
	public class PassLockLine : MonoBehaviour
	{
        public void Refresh()
        {
            bool locked = Managers.Pass != null && !Managers.Pass.IsVip();
            var manager = Managers.Pass;
            if (manager == null) return;
            gameObject.SetActive(!manager.IsFinalRewardUnlocked());
            transform.SetAsLastSibling();
            var rect = transform as RectTransform;
            if (rect != null) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -(PassTicketCell.Height + Mathf.Max(1, manager.GetCurrentStage()) * PassRewardsCell.Height));
            PassUI.Active(transform, "LockBg", locked);
            PassUI.Active(transform, "Lock", locked);
            if (locked) PassUI.Spine(PassUI.Node<Spine.Unity.SkeletonGraphic>(transform, "Lock"), "lock", true);
        }
        private void Start() => Refresh();
	}
}
