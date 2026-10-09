using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToolBase : MonoBehaviour
{
	[SerializeField] private string _key;
	[SerializeField] private Transform _lock;
	[SerializeField] private Transform _addIcon;
	[SerializeField] private Transform _num;
	[SerializeField] private TMP_Text _count;
	[SerializeField] private TMP_Text _unlockText;
	[SerializeField] private Button _button;
	[SerializeField] private Transform _priceNode;
	[SerializeField] private TMP_Text _priceText;
	[SerializeField] private Transform _icon;
	private InventoryManager _inventory;
	private bool _canUse = true;
	private bool _hasRendered;
	private bool _lastLocked;
	private bool _lastCanUse;
	private int _lastCount;

	public string Key => _key;

	private void Awake()
	{
		CacheLegacyPrefabReferences();
		_inventory = InventoryManager.Ensure();
		UpdateState();
	}

	private void OnEnable()
	{
		SubscribeInventory();
		UpdateState();
	}

	private void OnDisable()
	{
		if (_inventory != null) _inventory.OnInventoryChanged.RemoveListener(OnCountChanged);
	}

	public void Bind(string key)
	{
		_key = key;
		_hasRendered = false;
		CacheLegacyPrefabReferences();
		if (isActiveAndEnabled) SubscribeInventory();
		UpdateState();
	}

	private void SubscribeInventory()
	{
		if (_inventory != null) _inventory.OnInventoryChanged.RemoveListener(OnCountChanged);
		_inventory = InventoryManager.Ensure();
		_inventory.OnInventoryChanged.AddListener(OnCountChanged);
	}

	private void CacheLegacyPrefabReferences()
	{
		if (_icon == null) _icon = HierarchyReferenceCache.Descendant(transform, "Icon");
		if (_lock == null) _lock = HierarchyReferenceCache.Descendant(transform, "Locked");
		if (_addIcon == null) _addIcon = HierarchyReferenceCache.Descendant(transform, "Add");
		if (_num == null) _num = HierarchyReferenceCache.Descendant(transform, "Num");
		if (_priceNode == null) _priceNode = HierarchyReferenceCache.Descendant(transform, "Price");
		if (_count == null)
			_count = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Child(_num, "Text"));
		if (_unlockText == null)
		{
			Transform unlockNode = HierarchyReferenceCache.Path(_lock, "LockedLevel", "Text")
				?? HierarchyReferenceCache.Child(_lock, "Text");
			_unlockText = HierarchyReferenceCache.Component<TMP_Text>(unlockNode);
		}
		if (_priceText == null)
		{
			Transform priceTextNode = HierarchyReferenceCache.Child(_priceNode, "Count")
				?? HierarchyReferenceCache.Child(_priceNode, "Text");
			_priceText = HierarchyReferenceCache.Component<TMP_Text>(priceTextNode);
		}
		if (_button == null) _button = GetComponent<Button>();
		if (_button != null)
		{
			// Keep the normal appearance while interactable blocks unavailable boosters.
			ColorBlock colors = _button.colors;
			colors.disabledColor = colors.normalColor;
			_button.colors = colors;
			SpriteState sprites = _button.spriteState;
			sprites.disabledSprite = null;
			_button.spriteState = sprites;
		}
	}

	private void OnCountChanged(string key, int delta)
	{
		if (key == _key) UpdateState();
	}

	private bool IsLocked() => _inventory == null || !_inventory.IsBoosterUnlocked(_key);

	private void UpdateState()
	{
		bool locked = IsLocked();
		int count = _inventory == null ? 0 : _inventory.GetAmount(_key);
		if (_hasRendered && locked == _lastLocked && count == _lastCount && _canUse == _lastCanUse) return;
		_hasRendered = true;
		_lastLocked = locked;
		_lastCount = count;
		_lastCanUse = _canUse;
		SetLock(locked);
		SetActive(_addIcon, !locked && count == 0);
		SetActive(_num, !locked && count > 0);
		if (_count != null) _count.SetText(count.ToString());
		SetActive(_priceNode, false);
		if (_button != null) _button.interactable = !locked && _canUse;
	}

	public void UpdateCanUseState(bool canUse)
	{
		_canUse = canUse;
		UpdateState();
	}

	public bool TryConsume() => _inventory != null && _inventory.IsBoosterUnlocked(_key) && _inventory.Use(_key);

	public void AddCount(int amount)
	{
		if (_inventory != null && amount > 0) _inventory.Add(_key, amount);
	}

	private void SetLock(bool isLock)
	{
		SetActive(_icon, !isLock);
		SetActive(_lock, isLock);
		if (_unlockText != null) _unlockText.SetText(InventoryManager.GetToolUnlockLevel(_key).ToString());
	}

	private static void SetActive(Transform target, bool active)
	{
		if (target != null) target.gameObject.SetActive(active);
	}
}
