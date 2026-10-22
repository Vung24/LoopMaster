using UnityEngine;
using UnityEngine.SceneManagement;
using SRDebugger;

namespace Snui
{
    public class CheatManager : MonoBehaviour
    {
        public static CheatManager Current { get; private set; }
        public CheatDelegate Commands { get; private set; }
        internal static int RequestedLevel;
        public static bool SkipTutorial
        {
            get => GamePrefs.GetInt(GamePrefKeys.SkipTutorial, 0) != 0;
            set { GamePrefs.SetInt(GamePrefKeys.SkipTutorial, value ? 1 : 0); GamePrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => Ensure();

        public static CheatManager Ensure()
        {
            if (Current == null) new GameObject("CheatManager").AddComponent<CheatManager>();
            return Current;
        }

        private void Awake()
        {
            if (Current != null && Current != this) { Destroy(gameObject); return; }
            Current = this;
            DontDestroyOnLoad(gameObject);
            Commands = GetComponent<CheatDelegate>() ?? gameObject.AddComponent<CheatDelegate>();
            if (GetComponent<CheatGesture>() == null) gameObject.AddComponent<CheatGesture>();
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        [ContextMenu("Toggle Cheat Menu")]
        public void Toggle()
        {
            if (SRDebug.Instance == null) return;
            if (SRDebug.Instance.IsDebugPanelVisible)
            {
                SRDebug.Instance.HideDebugPanel();
            }
            else
            {
                SRDebug.Instance.ShowDebugPanel(DefaultTabs.Options);
            }
        }

        private void OnSceneChanged(Scene previous, Scene next)
        {
            if (SRDebug.Instance != null && SRDebug.Instance.IsDebugPanelVisible)
            {
                SRDebug.Instance.HideDebugPanel();
            }
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            if (Current == this) Current = null;
        }
    }
}
