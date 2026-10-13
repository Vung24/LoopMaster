using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BasePopup : Snui_Panel
    {
        private static readonly List<BasePopup> OpenPopups = new List<BasePopup>();
        private static int _escapeHandledFrame = -1;
        [Header("Base Popup Settings")]
        [SerializeField] protected Button _closeBtn;
        [SerializeField] protected bool _closeOnEscape = true;
        [SerializeField] protected bool _destroyOnClose = true;
        [SerializeField] protected CanvasGroup _canvasGroup;

        protected bool _isClosing;
        private bool _notifiedClosed;
        private UniTaskCompletionSource _closeTcs = new UniTaskCompletionSource();
        private CancellationTokenSource _openCts;
        private CancellationTokenSource _sessionCts;
        private int _session;
        private bool _destroyScheduled;
        private bool _deactivatingForClose;
        private bool _insideDisable;
        private bool _reopenRequested;

        public event Action Closed;
        public bool IsClosing => _isClosing;
        public bool IsOpen => isActiveAndEnabled && !_isClosing && !_notifiedClosed && !_destroyScheduled;
        public virtual bool IsBusy => false;

        /// <summary>Cancelled when closing starts, the popup is disabled, or it is destroyed.</summary>
        public CancellationToken OpenCancellationToken => _openCts == null
            ? new CancellationToken(true) : _openCts.Token;

        // Destruction completes the session too; only caller cancellation cancels this wait.
        public UniTask WaitForCloseAsync(CancellationToken cancellationToken = default) =>
            _closeTcs.Task.AttachExternalCancellation(cancellationToken);

        protected virtual void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            ResolveCloseButton();
            BindCloseButton();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_destroyScheduled) return;
            OpenPopups.Remove(this);
            OpenPopups.Add(this);

            if (_notifiedClosed) _closeTcs = new UniTaskCompletionSource();
            _notifiedClosed = false;
            _isClosing = false;
            _session++;
            DisposeSessionTokens();
            _openCts = new CancellationTokenSource();
            _sessionCts = new CancellationTokenSource();
            SetInteraction(true);

            int session = _session;
            OnOpening();
            if (session == _session && IsOpen) OnOpened();
        }

        /// <summary>Reopens a retained popup. Calling Show while open or closing has no effect.</summary>
        public virtual void Show()
        {
            if (_destroyScheduled || (_isClosing && !_notifiedClosed) || IsOpen) return;
            if (_insideDisable)
            {
                if (!_reopenRequested) ReopenAfterDisableAsync().Forget();
                return;
            }
            gameObject.SetActive(true);
        }

        private async UniTaskVoid ReopenAfterDisableAsync()
        {
            _reopenRequested = true;
            int session = _session;
            try
            {
                // Unity rejects SetActive from inside its own OnDisable callback.
                await UniTask.NextFrame(cancellationToken: this.GetCancellationTokenOnDestroy());
                if (this != null && session == _session) Show();
            }
            catch (OperationCanceledException) { }
            finally { _reopenRequested = false; }
        }

        public override void PlayEnterAnimation() => Show();

        protected virtual void ResolveCloseButton()
        {
            if (_closeBtn == null)
            {
                _closeBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseBtn")
                         ?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseButton")
                         ?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "BackButton");
            }
        }

        protected virtual void BindCloseButton()
        {
            if (_closeBtn != null)
            {
                _closeBtn.onClick.RemoveListener(OnClickCloseButton);
                _closeBtn.onClick.AddListener(OnClickCloseButton);
            }
        }

        protected virtual void Update()
        {
            if (_closeOnEscape && Input.GetKeyDown(KeyCode.Escape) &&
                _escapeHandledFrame != Time.frameCount && IsTopmostPopup())
            {
                _escapeHandledFrame = Time.frameCount;
                OnEscapeKeyPressed();
            }
        }

        private bool IsTopmostPopup()
        {
            for (int i = OpenPopups.Count - 1; i >= 0; i--)
            {
                BasePopup popup = OpenPopups[i];
                if (popup != null && popup.isActiveAndEnabled) return popup == this;
            }
            return false;
        }

        protected virtual void OnEscapeKeyPressed()
        {
            TryClose();
        }

        protected virtual void OnClickCloseButton()
        {
            TryClose();
        }

        public virtual bool CanClose()
        {
            return IsOpen && !IsBusy;
        }

        public virtual void Close()
        {
            TryClose();
        }

        public bool TryClose()
        {
            if (!CanClose()) return false;
            ForceClose();
            return true;
        }

        /// <summary>Bypasses the busy guard, for navigation and owner cleanup.</summary>
        public void ForceClose()
        {
            if (_isClosing || _notifiedClosed || _destroyScheduled) return;
            _isClosing = true;
            SetInteraction(false);
            CloseAsync().Forget();
        }

        public override void PlayExitAnimation()
        {
            ForceClose();
        }

        protected virtual async UniTaskVoid CloseAsync()
        {
            int session = _session;
            CancellationToken token = _sessionCts == null
                ? this.GetCancellationTokenOnDestroy() : _sessionCts.Token;
            try
            {
                CancelToken(_openCts);
                if (this == null || session != _session || _notifiedClosed) return;
                OnClosing();
                if (this == null || session != _session || _notifiedClosed) return;
                await PlayCloseAnimationAsync().AttachExternalCancellation(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                // An old animation must never disable a reopened session.
                if (this != null && session == _session && !_notifiedClosed)
                {
                    _destroyScheduled = _destroyOnClose;
                    _deactivatingForClose = true;
                    try { gameObject.SetActive(false); }
                    finally { _deactivatingForClose = false; }
                    if (session == _session) NotifyClosed();
                    if (_destroyScheduled) Destroy(gameObject);
                }
            }
        }

        protected virtual async UniTask PlayCloseAnimationAsync()
        {
            await UniTask.CompletedTask;
        }

        protected virtual void OnClosing() { }

        protected virtual void OnClosed() { }

        protected virtual void OnOpening() { }

        protected virtual void OnOpened() { }

        private void SetInteraction(bool interactable)
        {
            if (_closeBtn != null) _closeBtn.interactable = interactable;
            if (_canvasGroup == null) return;
            _canvasGroup.interactable = interactable;
            // Keep blocking the background during the exit animation.
            _canvasGroup.blocksRaycasts = true;
        }

        protected void NotifyClosed()
        {
            if (_notifiedClosed) return;
            _notifiedClosed = true;

            UniTaskCompletionSource completion = _closeTcs;
            Action closed = Closed;
            CancellationTokenSource open = _openCts;
            CancellationTokenSource session = _sessionCts;
            Closed = null;
            CancelToken(open);
            CancelToken(session);
            try { OnClosed(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            if (closed != null)
            {
                foreach (Action callback in closed.GetInvocationList())
                {
                    try { callback(); }
                    catch (Exception exception) { Debug.LogException(exception, this); }
                }
            }
            completion.TrySetResult();
        }

        protected override void OnDisable()
        {
            OpenPopups.Remove(this);
            base.OnDisable();
            // Normal close notifies after SetActive returns, so Closed can reopen safely.
            if (_deactivatingForClose) return;
            _insideDisable = true;
            try { NotifyClosed(); }
            finally { _insideDisable = false; }
        }

        private void CancelToken(CancellationTokenSource source)
        {
            if (source == null || source.IsCancellationRequested) return;
            try { source.Cancel(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void DisposeSessionTokens()
        {
            _openCts?.Dispose();
            _sessionCts?.Dispose();
            _openCts = null;
            _sessionCts = null;
        }

        protected override void OnDestroy()
        {
            _destroyScheduled = true;
            if (_closeBtn != null)
            {
                _closeBtn.onClick.RemoveListener(OnClickCloseButton);
            }
            NotifyClosed();
            DisposeSessionTokens();
            base.OnDestroy();
        }
    }
}

