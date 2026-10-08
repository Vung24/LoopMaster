using UnityEngine;

namespace Snui
{
	public class PassLockLine : MonoBehaviour
	{
        public void Refresh()
        {
            bool locked = Managers.Pass != null && !Managers.Pass.IsVip();
            PassUI.Active(transform, "LockBg", locked);
            PassUI.Active(transform, "Lock", locked);
        }
        private void Start() => Refresh();
	}
}
