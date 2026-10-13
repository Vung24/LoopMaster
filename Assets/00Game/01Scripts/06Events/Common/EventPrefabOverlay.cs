using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
    public sealed class EventPrefabOverlay : MonoBehaviour
    {
        public Button CloseButton;
        public System.Action Closed;
        private void OnDestroy() { Closed?.Invoke(); Closed = null; }
        private void Update()
        {
            // BasePopup handles input, busy guards, and the close animation.
            if (GetComponent<BasePopup>() != null) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (CloseButton != null)
            {
                if (CloseButton.interactable && CloseButton.enabled) CloseButton.onClick.Invoke();
            }
            else if (TryGetComponent<DialogContent>(out var content)) content.Dismiss();
            else Destroy(gameObject);
        }
    }
}
