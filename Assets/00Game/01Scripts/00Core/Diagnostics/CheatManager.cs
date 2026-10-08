using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace Snui
{
	public class CheatManager : MonoBehaviour
	{
        public static CheatManager Current { get; private set; }
        public CheatDialog Dialog { get; private set; }
        public CheatDelegate Commands { get; private set; }
        private bool _loading;
        private int _version;
        internal static int RequestedLevel;
        public static bool SkipTutorial
        {
            get => PlayerPrefs.GetInt("offline_cheat_SkipTutorial", 0) != 0;
            set { PlayerPrefs.SetInt("offline_cheat_SkipTutorial", value ? 1 : 0); PlayerPrefs.Save(); }
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
        public void Toggle() => LoadDialog().Forget();
        private async UniTask LoadDialog()
        {
            if (Dialog != null) { Dialog.Close(); return; }
            if (_loading) return;
            _loading = true;
            int version = _version;
            try
            {
                var dialog = await PopupAssets.Instantiate<CheatDialog>("CheatDialog", transform, this.GetCancellationTokenOnDestroy());
                if (this == null || version != _version) { Destroy(dialog.gameObject); return; }
                Dialog = dialog;
                Dialog.Initialize(Commands);
            }
            finally { if (this != null) _loading = false; }
        }

        private void OnSceneChanged(Scene previous, Scene next) { _version++; if (Dialog != null) Dialog.Close(); }
        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            if (Current == this) Current = null;
        }
	}
}
