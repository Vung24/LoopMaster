using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
    public sealed class DailyCheckInHomeUI : MonoBehaviour
    {
        private HomeController _home;
        private GameObject _entry, _notice, _dialog;
        private Button _button;
        private float _nextRefresh;
        public bool IsDialogOpen => _dialog != null && _dialog.activeInHierarchy;

        public void Bind(HomeController home)
        {
            _home = home;
            if (!DailyCheckInManager.IsOpen) return;
            if (_entry != null) return;
            _entry = DailyCheckInUI.Button("DailyCheckInButton", transform, "DAILY\nREWARDS",
                new Vector2(170, 105), Vector2.zero, new Color(.12f, .28f, .48f));
            var rect = (RectTransform)_entry.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(115, 335);
            _button = _entry.GetComponent<Button>(); _button.onClick.AddListener(Open);
            _notice = DailyCheckInUI.Text("Notice", _entry.transform, "!", new Vector2(32, 36),
                new Vector2(75, 45), 30, new Color(1, .79f, .25f)).gameObject;
            Snui_Text.EnsureFontAssets(_entry.transform);
            Refresh();
        }

        private bool CanOpen => _home != null && _home.CanOpenSpecialStreak &&
            _home.HomePage != null && !_home.HomePage.IsEntryBlocked;

        private void Open()
        {
            if (!CanOpen || !DailyCheckInManager.IsOpen || IsDialogOpen) return;
            _dialog = DailyCheckInUI.Create(_home.HomePage.GetComponentInParent<Canvas>().transform);
        }

        public async UniTask ShowOnArrival(CancellationToken token)
        {
            if (!DailyCheckInManager.IsOpen) return;
            var manager = DailyCheckInManager.Instance;
            manager.JudgeCrossDay();
            if (!DailyCheckInManager.IsOpen || !manager.IsDialogNeedAutoShow() || _home == null) return;
            token.ThrowIfCancellationRequested();
            if (!IsDialogOpen)
                _dialog = DailyCheckInUI.Create(_home.HomePage.GetComponentInParent<Canvas>().transform);
            var dialog = _dialog;
            try
            {
                await UniTask.WaitUntil(() => dialog == null || !dialog.activeInHierarchy, cancellationToken: token);
            }
            finally
            {
                if (token.IsCancellationRequested && dialog != null) Destroy(dialog);
                if (_dialog == dialog) _dialog = null;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && IsDialogOpen) _dialog.SetActive(false);
            if (_dialog != null && !_dialog.activeInHierarchy) { Destroy(_dialog); _dialog = null; }
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 1f;
            Refresh();
        }

        private void Refresh()
        {
            if (_entry == null) return;
            _entry.SetActive(DailyCheckInManager.IsOpen);
            if (!DailyCheckInManager.IsOpen) { OnDisable(); return; }
            if (_notice != null) _notice.SetActive(DailyCheckInManager.Instance.CheckAnyRewardCanClaim());
            if (_button != null) _button.interactable = CanOpen;
        }

        private void OnDisable()
        {
            if (_dialog != null) { _dialog.SetActive(false); Destroy(_dialog); }
            _dialog = null;
        }

        private void OnDestroy()
        {
            OnDisable();
            if (_button != null) _button.onClick.RemoveListener(Open);
        }
    }
}
