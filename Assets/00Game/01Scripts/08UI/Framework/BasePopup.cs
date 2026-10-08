using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
    public abstract class BasePopup : Snui_Panel
    {
        [Header("Base Popup Settings")]
        [SerializeField] protected Button _closeBtn;
        [SerializeField] protected bool _closeOnEscape = true;
        [SerializeField] protected bool _destroyOnClose = true;

        protected bool _isClosing;
        private bool _notifiedClosed;
        private readonly UniTaskCompletionSource _closeTcs = new UniTaskCompletionSource();

        public event Action Closed;
        public bool IsClosing => _isClosing;
        public virtual bool IsBusy => false;

        public UniTask WaitForCloseAsync() =>
            _closeTcs.Task.AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

        protected virtual void Awake()
        {
            ResolveCloseButton();
            BindCloseButton();
        }

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
            if (_closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
            {
                OnEscapeKeyPressed();
            }
        }

        protected virtual void OnEscapeKeyPressed()
        {
            if (CanClose())
            {
                Close();
            }
        }

        protected virtual void OnClickCloseButton()
        {
            if (CanClose())
            {
                Close();
            }
        }

        public virtual bool CanClose()
        {
            return !_isClosing && !IsBusy;
        }

        public virtual void Close()
        {
            if (_isClosing) return;
            _isClosing = true;

            CloseAsync().Forget();
        }

        public override void PlayExitAnimation()
        {
            Close();
        }

        protected virtual async UniTaskVoid CloseAsync()
        {
            OnClosing();

            if (_closeBtn != null) _closeBtn.interactable = false;

            await PlayCloseAnimationAsync();

            NotifyClosed();

            if (_destroyOnClose)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        protected virtual async UniTask PlayCloseAnimationAsync()
        {
            await UniTask.CompletedTask;
        }

        protected virtual void OnClosing() { }

        protected virtual void OnClosed() { }

        protected void NotifyClosed()
        {
            if (_notifiedClosed) return;
            _notifiedClosed = true;

            _closeTcs.TrySetResult();
            Closed?.Invoke();
            Closed = null;
            OnClosed();
        }

        protected override void OnDestroy()
        {
            if (_closeBtn != null)
            {
                _closeBtn.onClick.RemoveListener(OnClickCloseButton);
            }
            NotifyClosed();
            base.OnDestroy();
        }
    }
}

