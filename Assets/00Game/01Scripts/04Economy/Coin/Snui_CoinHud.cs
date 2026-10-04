using Snui;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Base coin counter used by both the prefab and the interactive CoinHud component.</summary>
public class Snui_CoinHud : Snui_BaseHud
{
	public static Snui_CoinHud Current;
	[SerializeField] private Transform _addIconReference;
	[SerializeField] private Image _coinIconReference;
	private Transform _originParent;
	private Vector2 _originLocalPos;
	protected Button _button;
	protected Transform _addIcon;
	protected Image _coinIcon;
	private bool _isShaking;
	private InventoryManager _inventory;

	private void OnEnable()
	{
		_inventory = InventoryManager.Ensure();
		_inventory.OnInventoryChanged.AddListener(RefreshInventory);
		RefreshInventory(InventoryKeys.COIN, 0);
	}
	private void OnDisable()
	{
		if (_inventory != null) _inventory.OnInventoryChanged.RemoveListener(RefreshInventory);
	}
	protected override void Start() => RefreshInventory(InventoryKeys.COIN, 0);
	public override void SetAutoUpdate(bool isAuto)
	{
		base.SetAutoUpdate(isAuto);
		if (isAuto) RefreshInventory(InventoryKeys.COIN, 0);
	}
	private void RefreshInventory(string key, int delta)
	{
		if (AutoUpdate && key == InventoryKeys.COIN && _inventory != null)
			SetNumber(_inventory.GetAmount(InventoryKeys.COIN));
	}

	protected override void Awake()
	{
		base.Awake();
		Current = this;
		_originParent = transform.parent;
		_originLocalPos = transform.localPosition;
		_button = GetComponent<Button>();
		_addIcon = _addIconReference != null
			? _addIconReference
			: HierarchyReferenceCache.Descendant(transform, "Add");
		_coinIcon = _coinIconReference != null
			? _coinIconReference
			: HierarchyReferenceCache.Component<Image>(HierarchyReferenceCache.Descendant(transform, "Icon"));
		if (_coinIcon != null && _coinIcon.sprite == null && DataPrefabLibrary.Current != null)
			_coinIcon.sprite = DataPrefabLibrary.Current.HudCoinSprite;
	}

	public override void PlayShakeAnimation()
	{
		if (_isShaking) return;
		_isShaking = true;
		base.PlayShakeAnimation();
		Invoke(nameof(StopShakeAnimation), 0.35f);
	}

	public void StopShakeAnimation() => _isShaking = false;
	public void DisableClick(bool isDisplayAddImg = false) { if (_button != null) _button.interactable = false; if (_addIcon != null) _addIcon.gameObject.SetActive(isDisplayAddImg); }
	public void EnableClick() { if (_button != null) _button.interactable = true; if (_addIcon != null) _addIcon.gameObject.SetActive(false); }

	public static void TryResetParent()
	{
		if (Current == null || Current._originParent == null) return;
		Current.transform.SetParent(Current._originParent, false);
		Current.transform.localPosition = Current._originLocalPos;
	}

	public static void TrySetAutoUpdate(bool autoUpdate) { if (Current != null) Current.SetAutoUpdate(autoUpdate); }
	public static void TrySetParent(Transform parent) { if (Current != null && parent != null) Current.transform.SetParent(parent, false); }

	protected override void OnDestroy()
	{
		if (Current == this) Current = null;
		base.OnDestroy();
	}
}
