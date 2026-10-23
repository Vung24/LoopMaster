using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

namespace Snui
{
	public class CheatDialog : MonoBehaviour
	{
        [SerializeField] private Button _closeBtn;
        [SerializeField] private TMP_Text _info;
        [SerializeField] private Toggle _noAdToggle, _adToggle, _skipTutorialToggle, _passVipToggle;
        [SerializeField] private TMP_InputField _levelInput;
        [SerializeField] private Button _reloadBtn, _levelPrevBtn, _levelNextBtn;
        [SerializeField] private Image _inventoryIcon;
        [SerializeField] private TMP_InputField _inventoryInput;
        [SerializeField] private Button _inventoryPlusBtn, _inventoryMinusBtn;
        [SerializeField] private Image _eventItemIcon;
        [SerializeField] private TMP_InputField _eventItemInput;
        [SerializeField] private Button _eventItemPlusBtn;
        [SerializeField] private Button _firebaseInfoBtn, _logBtn, _AdDebugBtn, _customBtnPrefab;
        private CheatDelegate _commands;
        private CheatItemSelector _selector;
        private Button _winBtn, _failBtn;
        private int _inventoryIndex, _eventIndex;
        private bool _busy;
        private string _message = "F8 / hold 3 fingers / Escape to close";
        private Sprite _defaultEventSprite;

        public void Initialize(CheatDelegate commands)
        {
            _commands = commands;
            _defaultEventSprite = _eventItemIcon == null ? null : _eventItemIcon.sprite;
            var canvas = GetComponent<Canvas>();
            if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 32000; }
            Snui_Text.EnsureFontAssets(transform);
            if (_info != null) { _info.enableAutoSizing = true; _info.fontSizeMin = 16; _info.fontSizeMax = 28; }
            Bind(_closeBtn, Close);
            Bind(_reloadBtn, () => LoadLevel(0).Forget());
            Bind(_levelPrevBtn, () => LoadLevel(-1).Forget());
            Bind(_levelNextBtn, () => LoadLevel(1).Forget());
            _winBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "WinButton");
            _failBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "FailButton");
            Bind(_winBtn, () => EndGame(true));
            Bind(_failBtn, () => EndGame(false));
            Bind(_inventoryPlusBtn, () => ChangeInventory(true));
            Bind(_inventoryMinusBtn, () => ChangeInventory(false));
            Bind(_eventItemPlusBtn, AddEventItems);
            if (_inventoryIcon != null) Bind(_inventoryIcon.GetComponent<Button>() ?? _inventoryIcon.gameObject.AddComponent<Button>(), () => SelectInventory().Forget());
            if (_eventItemIcon != null) Bind(_eventItemIcon.GetComponent<Button>() ?? _eventItemIcon.gameObject.AddComponent<Button>(), () => { _eventIndex = 1 - _eventIndex; Refresh(); });
            if (_levelInput != null) _levelInput.SetTextWithoutNotify((GameController.Current?.CurrentLevelNumber > 0 ? GameController.Current.CurrentLevelNumber : GameProgressStore.NextPlayableLevel).ToString());
            InitNumberInput(_levelInput);
            InitNumberInput(_inventoryInput);
            InitNumberInput(_eventItemInput);
            if (_inventoryInput != null) _inventoryInput.SetTextWithoutNotify("100");
            if (_eventItemInput != null) _eventItemInput.SetTextWithoutNotify("1");
            if (_skipTutorialToggle != null)
            {
                _skipTutorialToggle.SetIsOnWithoutNotify(CheatManager.SkipTutorial);
                _skipTutorialToggle.onValueChanged.AddListener(value => { _commands.SetSkipTutorial(value); Refresh(); });
            }
            if (_passVipToggle != null)
            {
                SetLabel(_passVipToggle.transform, "VIP Hearts");
                _passVipToggle.SetIsOnWithoutNotify(HeartManager.EnsureInitialized().MAX_HEART == BaseHeartManager.PASS_VIP_MAX_HEART);
                _passVipToggle.onValueChanged.AddListener(value => { _commands.SetVipHearts(value); Refresh(); });
            }
            DisableService(_noAdToggle, "No AD (offline)");
            DisableService(_adToggle, "AD unavailable");
            DisableService(_firebaseInfoBtn, "Firebase unavailable");
            DisableService(_AdDebugBtn, "AD Debug unavailable");
            Bind(_logBtn, () => { Debug.Log(_info != null ? _info.text : "LoopMaster cheats"); _message = "State written to Unity Console"; Refresh(); });
            if (_customBtnPrefab != null)
            {
                SetLabel(_customBtnPrefab.transform, "Refill Hearts");
                Bind(_customBtnPrefab, RefillHearts);
                _customBtnPrefab.gameObject.SetActive(true);
            }
            Refresh();
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        { if (button != null) { button.targetGraphic = button.GetComponent<Graphic>(); button.onClick.AddListener(action); } }
        private static void InitNumberInput(TMP_InputField input)
        { if (input != null) { input.contentType = TMP_InputField.ContentType.IntegerNumber; input.characterLimit = 10; } }
        private static void SetLabel(Transform root, string text)
        { var label = root.GetComponentInChildren<TMP_Text>(true); if (label != null) label.text = text; }
        private static void DisableService(Selectable control, string label)
        { if (control != null) { control.interactable = false; SetLabel(control.transform, label); } }
        private static bool ReadAmount(TMP_InputField input, out int amount)
        { amount = 0; return input != null && int.TryParse(input.text, out amount) && amount > 0; }

        private void EndGame(bool win)
        {
            if (_busy) return;
            if (_commands.ForceEnd(win)) Close();
            else { _message = "Open a playing level first"; Refresh(); }
        }

        private async UniTaskVoid LoadLevel(int offset)
        {
            if (_busy) return;
            if (!ReadAmount(_levelInput, out int level) || (long)level + offset < 1 || (long)level + offset > int.MaxValue)
            { _message = "Enter a valid level number"; Refresh(); return; }
            _busy = true;
            _message = "Loading level...";
            Refresh();
            bool loaded = false;
            try { loaded = await _commands.LoadLevel(level + offset); }
            finally { _busy = false; }
            if (this == null) return;
            if (loaded) Close();
            else { _message = "Level unavailable or game is busy"; Refresh(); }
        }

        private async UniTaskVoid SelectInventory()
        {
            if (_selector != null || _busy) return;
            _selector = await CheatItemSelector.Show(transform);
            if (_selector == null) { _message = "Item selector prefab missing"; Refresh(); return; }
            _selector.Init(CheatDelegate.InventoryTypes, _inventoryIndex);
            _selector.OnSelected = index => { _inventoryIndex = index; Refresh(); };
        }

        private void ChangeInventory(bool add)
        {
            if (_busy) return;
            bool success = ReadAmount(_inventoryInput, out int amount) &&
                _commands.ChangeInventory(CheatDelegate.InventoryTypes[_inventoryIndex], amount, add);
            _message = success ? "Inventory updated" : "Invalid amount, limit reached, or insufficient inventory";
            Refresh();
        }

        private void AddEventItems()
        {
            if (_busy) return;
            bool success = ReadAmount(_eventItemInput, out int amount) && _commands.AddEventItems(_eventIndex, amount);
            _message = success ? "Event items added" : "Event must be ongoing; check amount and tutorial";
            Refresh();
        }

        private void RefillHearts()
        {
            if (_busy) return;
            var heart = HeartManager.EnsureInitialized();
            _commands.ChangeInventory(RewardType.Heart, heart.MAX_HEART, true);
            _message = "Hearts refilled: " + heart.GetHeartCount() + "/" + heart.MAX_HEART;
            Refresh();
        }

        public void Refresh()
        {
            if (_commands == null) return;
            var type = CheatDelegate.InventoryTypes[_inventoryIndex];
            if (_inventoryIcon != null) _inventoryIcon.sprite = PrefabLibrary.Current?.ResolveRewardIcon(type.GetIconPath());
            var infinity = _inventoryIcon == null ? null : HierarchyReferenceCache.Child(_inventoryIcon.transform, "Infinity");
            if (infinity != null) infinity.gameObject.SetActive(type.IsTimeLimited());
            if (_eventItemIcon != null)
                _eventItemIcon.sprite = _defaultEventSprite;
            if (_info != null)
            {
                _info.text = $"{type.Key}: {_commands.GetAmount(type)} (tap icon to select)\n{_message}";
            }
            bool active = !_busy && GameController.Current?.Data != null && GameController.Current.EndState == GameEndState.Playing;
            if (_winBtn != null) _winBtn.interactable = active;
            if (_failBtn != null) _failBtn.interactable = active;
            foreach (var button in new[] { _reloadBtn, _levelPrevBtn, _levelNextBtn, _inventoryPlusBtn, _inventoryMinusBtn, _eventItemPlusBtn })
                if (button != null) button.interactable = !_busy;
        }

        public void Close()
        {
            if (_selector != null) _selector.Close();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
	}
}
