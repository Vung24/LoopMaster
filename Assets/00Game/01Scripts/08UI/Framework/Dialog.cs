using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace Snui
{
    [RequireComponent(typeof(CanvasGroup))]
    public class Dialog : Snui_Panel
    {
        public enum EnterAnimType { None, FadeIn }
        public enum ExitAnimType { None, FadeOut }
        public static Color DefaultMaskColor = new Color(0f, 0f, 0f, 0.8f);
        public static Transform DefaultParent { get; set; }
        public EnterAnimType EnterAnim;
        public ExitAnimType ExitAnim;
        public float FadeInOutDuration = 0.03f;
        public bool CloseOnContentResult = true;
        protected CanvasGroup _canvasGroup;
        protected Image _bg;
        protected Transform _safeArea;
        protected DialogContent _content;
        private Coroutine fade;
        private Action callback;
        private GameObject addressableContent;
        private bool closing;
        private bool entered;

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _bg = GetComponent<Image>();
            SafeArea area = GetComponentInChildren<SafeArea>(true);
            _safeArea = area == null ? transform : area.transform;
        }

        protected override void Start() { if (!entered) PlayEnterAnimation(); }

        public static UniTask<Dialog> ShowEmpty(Transform parent)
        {
            DataPrefabLibrary library = DataPrefabLibrary.Current;
            if (library == null || library.DialogPrefab == null)
                throw new InvalidOperationException("The recovered Dialog prefab is not assigned.");
            GameObject root = Instantiate(library.DialogPrefab, parent, false);
            Dialog dialog = root.GetComponent<Dialog>();
            if (dialog == null) { Destroy(root); throw new InvalidOperationException("Dialog prefab has no Dialog component."); }
            dialog.PlayEnterAnimation();
            return UniTask.FromResult(dialog);
        }

        public static UniTask<T> ShowContent<T>(string path, float fadeAnimDuration = 0.03f) where T : DialogContent
        { return ShowContent<T>(GetDefaultParent(), path, fadeAnimDuration); }

        public static async UniTask<T> ShowContent<T>(Transform parent, string path, float fadeAnimDuration = 0.03f) where T : DialogContent
        {
            DataPrefabLibrary library = DataPrefabLibrary.Current;
            GameObject prefab = library == null ? null : library.Resolve(path);
            if (prefab == null) throw new InvalidOperationException("No offline prefab is available for " + path);
            Dialog dialog = await ShowEmpty(parent);
            dialog.FadeInOutDuration = fadeAnimDuration;
            GameObject instance = Instantiate(prefab, dialog._safeArea, false);
            T content = instance.GetComponent<T>();
            if (content == null) { Destroy(dialog.gameObject); throw new InvalidOperationException("Prefab has no " + typeof(T).Name); }
            dialog.AddDialogContent(content);
            return content;
        }

        public static UniTask<T> ShowContent<T>(AssetReference reference, float fadeAnimDuration = 0.03f) where T : DialogContent
        { return ShowContent<T>(GetDefaultParent(), reference, fadeAnimDuration); }

        public static async UniTask<T> ShowContent<T>(Transform parent, AssetReference reference, float fadeAnimDuration = 0.03f) where T : DialogContent
        {
            if (reference == null || !reference.RuntimeKeyIsValid()) throw new ArgumentException("Invalid content reference.", nameof(reference));
            Dialog dialog = await ShowEmpty(parent);
            try
            {
                dialog.FadeInOutDuration = fadeAnimDuration;
                GameObject instance = await reference.InstantiateAsync(dialog._safeArea);
                if (dialog == null) { Addressables.ReleaseInstance(instance); throw new OperationCanceledException(); }
                dialog.addressableContent = instance;
                T content = instance.GetComponent<T>();
                if (content == null) throw new InvalidOperationException("Prefab has no " + typeof(T).Name);
                dialog.AddDialogContent(content);
                return content;
            }
            catch { if (dialog != null) Destroy(dialog.gameObject); throw; }
        }

        public static async UniTask<Dialog> ShowWithContent(Transform parent, DialogContent content, Action callback = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            Dialog dialog = await ShowEmpty(parent);
            dialog.AddDialogContent(content, callback);
            return dialog;
        }

        private static Transform GetDefaultParent()
        {
            if (DefaultParent != null) return DefaultParent;
            UICanvas canvas = UICanvas.Current;
            if (canvas == null) throw new InvalidOperationException(
                "Assign Dialog.DefaultParent or supply a parent when no gameplay UI canvas is active.");
            return canvas.transform;
        }

        public void AddDialogContent(DialogContent content, Action callback = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (_content != null)
            {
                _content.Dismissed -= OnContentDismissed;
                if (_content != content) _content.Hide();
            }
            _content = content;
            this.callback = callback;
            content.transform.SetParent(_safeArea == null ? transform : _safeArea, false);
            content.Dismissed += OnContentDismissed;
            closing = false;
            content.Show();
        }

        private void OnContentDismissed(int result)
        {
            Action completed = callback;
            callback = null;
            if (CloseOnContentResult) PlayExitAnimation();
            completed?.Invoke();
        }

        public override void PlayEnterAnimation()
        {
            entered = true;
            gameObject.SetActive(true);
            closing = false;
            StopFade();
            if (EnterAnim == EnterAnimType.FadeIn && FadeInOutDuration > 0f)
                fade = StartCoroutine(Fade(0f, 1f, false));
            else SetVisible(1f, true);
        }

        public override void PlayExitAnimation()
        {
            if (closing) return;
            closing = true;
            StopFade();
            if (ExitAnim == ExitAnimType.FadeOut && FadeInOutDuration > 0f && gameObject.activeInHierarchy)
                fade = StartCoroutine(Fade(CanvasGroup == null ? 1f : CanvasGroup.alpha, 0f, true));
            else Destroy(gameObject);
        }

        private IEnumerator Fade(float from, float to, bool destroy)
        {
            float elapsed = 0f;
            SetVisible(from, !destroy);
            while (elapsed < FadeInOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetVisible(Mathf.Lerp(from, to, elapsed / FadeInOutDuration), !destroy);
                yield return null;
            }
            SetVisible(to, !destroy);
            fade = null;
            if (destroy) Destroy(gameObject);
        }

        private void SetVisible(float alpha, bool interactable)
        {
            if (CanvasGroup == null) return;
            CanvasGroup.alpha = alpha;
            CanvasGroup.interactable = interactable;
            CanvasGroup.blocksRaycasts = true;
        }

        private void StopFade() { if (fade != null) { StopCoroutine(fade); fade = null; } }
        protected override void OnDisable() { StopFade(); base.OnDisable(); }
        protected override void OnDestroy()
        {
            if (_content != null) _content.Dismissed -= OnContentDismissed;
            if (addressableContent != null) Addressables.ReleaseInstance(addressableContent);
            base.OnDestroy();
        }
    }
}
