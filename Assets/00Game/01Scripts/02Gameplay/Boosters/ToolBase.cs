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
	private bool _introLocked;
	private bool _lastFree;
	private Image _iconImage;

	public string Key => _key;
	public Sprite IconSprite
	{
		get
		{
			if (_iconImage == null) CacheLegacyPrefabReferences();
			return _iconImage != null ? _iconImage.sprite : null;
		}
	}
	public RectTransform IconRectTransform
	{
		get
		{
			if (_iconImage == null) CacheLegacyPrefabReferences();
			return _iconImage != null ? _iconImage.rectTransform : null;
		}
	}

	public void SetIntroLocked(bool locked)
	{
		_introLocked = locked;
		UpdateState();
	}

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
		if (_icon == null) _icon = HierarchyReferenceCache.Child(transform, "Icon");
		if (_icon == null) _icon = HierarchyReferenceCache.Child(transform, "Image");
		if (_iconImage == null)
		{
			// Legacy tool prefabs keep their booster artwork in Image; Price/Icon is a coin.
			Transform artwork = HierarchyReferenceCache.Child(transform, "Image");
			if (artwork != null) _iconImage = artwork.GetComponent<Image>();
			if (_iconImage == null && _icon != null)
				_iconImage = _icon.GetComponent<Image>();
		}
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

	private bool IsLocked() => _introLocked || _inventory == null || !_inventory.IsBoosterUnlocked(_key);

	private void UpdateState()
	{
		bool locked = IsLocked();
		int count = _inventory == null ? 0 : _inventory.GetAmount(_key);
		bool free = BoosterUnlockProgress.IsFirstUseFree(_key);
		if (_hasRendered && locked == _lastLocked && count == _lastCount && _canUse == _lastCanUse && free == _lastFree) return;
		_hasRendered = true;
		_lastLocked = locked;
		_lastCount = count;
		_lastFree = free;
		_lastCanUse = _canUse;
		SetLock(locked);
		SetActive(_addIcon, !locked && count == 0 && !free);
		SetActive(_num, !locked && count > 0);
		if (_count != null) _count.SetText(count.ToString());
		SetActive(_priceNode, false);
		if (_button != null) _button.interactable = !locked;

		if (_icon != null)
		{
			Graphic graphic = _icon.GetComponent<Graphic>();
			if (graphic != null)
			{
				Color color = graphic.color;
				color.a = (!locked && count > 0 && !_canUse) ? 0.6f : 1f;
				graphic.color = color;
			}
		}
	}

	public void UpdateCanUseState(bool canUse)
	{
		_canUse = canUse;
		UpdateState();
	}

	public bool Consume() => _inventory != null && _inventory.IsBoosterUnlocked(_key) && _inventory.Use(_key);

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
