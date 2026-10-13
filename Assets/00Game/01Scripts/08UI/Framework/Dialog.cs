using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace Snui
{
    [RequireComponent(typeof(CanvasGroup))]
    public class Dialog : BasePopup
    {
        public enum EnterAnimType { None, FadeIn }
        public enum ExitAnimType { None, FadeOut }
        public static Color DefaultMaskColor = new Color(0f, 0f, 0f, 0.8f);
        public static Transform DefaultParent { get; set; }
        public EnterAnimType EnterAnim;
        public ExitAnimType ExitAnim;
        public float FadeInOutDuration = 0.03f;
        public bool CloseOnContentResult = true;
        protected Image _bg;
        protected Transform _safeArea;
        protected DialogContent _content;
        private Coroutine fade;
        private Action callback;
        private bool entered;
        private int animationSession;

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _bg = GetComponent<Image>();
            SafeArea area = GetComponentInChildren<SafeArea>(true);
            _safeArea = area == null ? transform : area.transform;
        }

        protected override void Start() { if (!entered) PlayEnterAnimation(); }

        public static async UniTask<Dialog> ShowEmpty(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            Dialog dialog = await PopupAssets.Instantiate<Dialog>("Dialog", parent, parent.GetCancellationTokenOnDestroy());
            dialog.PlayEnterAnimation();
            return dialog;
        }

        public static UniTask<T> ShowContent<T>(string path, float fadeAnimDuration = 0.03f) where T : DialogContent
        { return ShowContent<T>(GetDefaultParent(), path, fadeAnimDuration); }

        public static async UniTask<T> ShowContent<T>(Transform parent, string path, float fadeAnimDuration = 0.03f) where T : DialogContent
        {
            Dialog dialog = await ShowEmpty(parent);
            try
            {
                dialog.FadeInOutDuration = fadeAnimDuration;
                T content = await PopupAssets.Instantiate<T>(path, dialog._safeArea,
                    dialog.GetCancellationTokenOnDestroy());
                dialog.AddDialogContent(content);
                return content;
            }
            catch { if (dialog != null) Destroy(dialog.gameObject); throw; }
        }

        public static async UniTask<T> ShowContent<T>(Transform parent, GameObject prefab, float fadeAnimDuration = 0.03f) where T : DialogContent
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
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
            return await ShowContent<T>(parent, reference.RuntimeKey.ToString(), fadeAnimDuration);
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
            if (IsClosing || (IsOpen && entered)) return;
            base.PlayEnterAnimation();
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            animationSession++;
            entered = true;
            StopFade();
            if (EnterAnim == EnterAnimType.FadeIn && FadeInOutDuration > 0f)
                fade = StartCoroutine(Fade(0f, 1f));
            else SetVisible(1f, true);
        }

        // Content owns its close button and result; the frame owns its lifetime.
        protected override void ResolveCloseButton() { }

        protected override void OnEscapeKeyPressed()
        {
            DialogContent content = _content != null ? _content : GetComponent<DialogContent>();
            if (content != null && content.isActiveAndEnabled) content.HandleBackButton();
            else
            {
                // Composed gameplay frames manage their own result flow.
                var overlay = GetComponent<EventPrefabOverlay>();
                var button = overlay == null ? null : overlay.CloseButton;
                if (button != null && button.isActiveAndEnabled && button.interactable)
                    button.onClick.Invoke();
            }
        }

        protected override async UniTask PlayCloseAnimationAsync()
        {
            int session = animationSession;
            StopFade();
            if (ExitAnim == ExitAnimType.FadeOut && FadeInOutDuration > 0f && gameObject.activeInHierarchy)
            {
                float from = CanvasGroup.alpha;
                float elapsed = 0f;
                while (elapsed < FadeInOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    SetVisible(Mathf.Lerp(from, 0f, elapsed / FadeInOutDuration), false);
                    await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
                    if (session != animationSession || !isActiveAndEnabled) return;
                }
                SetVisible(0f, false);
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            float elapsed = 0f;
            SetVisible(from, true);
            while (elapsed < FadeInOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetVisible(Mathf.Lerp(from, to, elapsed / FadeInOutDuration), true);
                yield return null;
            }
            SetVisible(to, true);
            fade = null;
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
            base.OnDestroy();
        }
    }
}
