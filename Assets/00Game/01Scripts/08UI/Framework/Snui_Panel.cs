using UnityEngine;
using UnityEngine.Events;

namespace Snui
{
    public class Snui_Panel : MonoBehaviour
    {
        public static int ShowingCount;
        public UnityAction OnDestoryed;
        private bool counted;

        public static void DestroyPanel(GameObject go)
        {
            if (go == null) return;
            Snui_Panel panel = go.GetComponent<Snui_Panel>();
            if (panel != null) panel.PlayExitAnimation();
            else Destroy(go);
        }

        protected virtual void Start() { PlayEnterAnimation(); }
        public virtual void PlayEnterAnimation() { gameObject.SetActive(true); }
        public virtual void PlayExitAnimation() { Destroy(gameObject); }

        protected virtual void OnEnable()
        {
            if (counted) return;
            ShowingCount++;
            counted = true;
        }

        protected virtual void OnDisable()
        {
            if (!counted) return;
            ShowingCount = Mathf.Max(0, ShowingCount - 1);
            counted = false;
        }

        protected virtual void OnDestroy() { OnDisable(); OnDestoryed?.Invoke(); }
    }
}
