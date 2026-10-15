using System;
using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuyToolDialogContent : DialogContent
{
	public const int RESULT_PURCHASED = 1;
	private string _toolKey;
	[SerializeField] private Button _adBtn, _coinBtn;
	[SerializeField] private Transform _titleText, _desc, _icon;
	[SerializeField] private Transform _undoIcon, _shuffleIcon, _pathIcon;
	[SerializeField] private TMP_Text _coinPriceLabel;
	private Func<bool> _isValid;
	private bool _processing;
	public int Price { get; private set; }
	public string ToolKey => _toolKey;
	private void EnsureReferences()
	{
		_adBtn = _adBtn != null ? _adBtn : DialogUI.ResolveButton(transform, "AdBtn");
		_coinBtn = _coinBtn != null ? _coinBtn : DialogUI.ResolveButton(transform, "CoinBtn");
		_closeBtn = _closeBtn != null ? _closeBtn : (DialogUI.ResolveButton(transform, "CloseButton") ?? DialogUI.ResolveButton(transform, "CloseBtn"));
		if (_titleText == null) _titleText = HierarchyReferenceCache.Descendant(transform, "Title");
		if (_desc == null) _desc = HierarchyReferenceCache.Descendant(transform, "Desc");
		if (_icon == null) _icon = HierarchyReferenceCache.Descendant(transform, "ToolBackLigh") ?? HierarchyReferenceCache.Descendant(transform, "ToolBackLight");
		if (_undoIcon == null) _undoIcon = HierarchyReferenceCache.Descendant(transform, InventoryKeys.TOOL_UNDO);
		if (_shuffleIcon == null) _shuffleIcon = HierarchyReferenceCache.Descendant(transform, InventoryKeys.TOOL_SHUFFLE);
		if (_pathIcon == null) _pathIcon = HierarchyReferenceCache.Descendant(transform, InventoryKeys.TOOL_PATH);
		if (_coinPriceLabel == null && _coinBtn != null)
			_coinPriceLabel = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Descendant(_coinBtn.transform, "Count"));
		DialogUI.Bind(_adBtn, OnClickOfflineFreeButton);
		DialogUI.Bind(_coinBtn, OnClickCoinBtn);
		DialogUI.Bind(_closeBtn, OnClickCloseButton);
	}

	protected override void Awake()
	{
		base.Awake();
		EnsureReferences();
	}

	public void Init(string toolKey, Func<bool> isValid = null)
	{
		EnsureReferences();
		_toolKey = toolKey;
		_isValid = isValid;
		_processing = false;
		Price = toolKey == InventoryKeys.TOOL_UNDO ? 450 : 900;
		DialogUI.Label(_titleText, toolKey == InventoryKeys.TOOL_PATH ? "ADD SPACE" : (toolKey ?? "BOOSTER").ToUpperInvariant());
		DialogUI.Label(_desc, toolKey == InventoryKeys.TOOL_UNDO ? "Undo the last move" :
			toolKey == InventoryKeys.TOOL_SHUFFLE ? "Shuffle the colors in available boxes" : "Add space to the belt");
		if (_icon != null)
		{
			_icon.gameObject.SetActive(true);
			foreach (Transform child in _icon)
			{
				if (InventoryManager.IsBoosterKey(child.name))
					child.gameObject.SetActive(string.Equals(child.name, toolKey, StringComparison.OrdinalIgnoreCase));
			}
		}
		if (_undoIcon != null)
			_undoIcon.gameObject.SetActive(string.Equals(toolKey, InventoryKeys.TOOL_UNDO, StringComparison.OrdinalIgnoreCase));
		if (_shuffleIcon != null)
			_shuffleIcon.gameObject.SetActive(string.Equals(toolKey, InventoryKeys.TOOL_SHUFFLE, StringComparison.OrdinalIgnoreCase));
		if (_pathIcon != null)
			_pathIcon.gameObject.SetActive(string.Equals(toolKey, InventoryKeys.TOOL_PATH, StringComparison.OrdinalIgnoreCase));

		Transform iconContainer = _icon != null && _icon.parent != null ? _icon.parent : transform;
		if (iconContainer != null)
		{
			for (int i = 0; i < iconContainer.childCount; i++)
			{
				Transform child = iconContainer.GetChild(i);
				if (InventoryManager.IsBoosterKey(child.name))
					child.gameObject.SetActive(string.Equals(child.name, toolKey, StringComparison.OrdinalIgnoreCase));
			}
		}
		if (_coinBtn != null)
		{
			DialogUI.Label(HierarchyReferenceCache.Descendant(_coinBtn.transform, "Count"), Price.ToString());
		}
		if (_coinPriceLabel != null) _coinPriceLabel.text = Price.ToString();
		if (_adBtn != null)
		{
			DialogUI.HideNamed(_adBtn.transform, "Ad");
			DialogUI.Label(_adBtn.transform, "FREE (OFFLINE)");
		}
		Snui_Text.EnsureFontAssets(transform);
	}
	private bool CanPurchase => !_processing && isActiveAndEnabled && Result == RESULT_UNKNOWN &&
		InventoryManager.Ensure().IsBoosterUnlocked(_toolKey) && (_isValid == null || _isValid());
	public void OnClickCoinBtn()
	{
		if (!CanPurchase) return;
		_processing = true;
		if (InventoryManager.Ensure().TryPurchaseBooster(_toolKey, Price)) Dismiss(RESULT_PURCHASED);
		else { _processing = false; DialogUI.Label(_desc, "Not enough coins. You need " + Price + "."); }
	}
	public void OnClickOfflineFreeButton()
	{
		if (!CanPurchase) return;
		_processing = true;
		if (InventoryManager.Ensure().Add(_toolKey, 1, "offline_free")) Dismiss(RESULT_PURCHASED);
		else _processing = false;
	}
}

public static class DialogUI
{
	public static Button ResolveButton(Transform root, string name)
	{
		Transform node = HierarchyReferenceCache.Descendant(root, name);
		return node == null ? null : node.GetComponent<Button>();
	}
	public static void Bind(Button button, UnityEngine.Events.UnityAction action)
	{
		if (button == null) return;
		button.onClick.RemoveListener(action);
		button.onClick.AddListener(action);
	}
	public static void Label(Transform node, string text)
	{
		if (node == null) return;
		foreach (Snui_Text label in node.GetComponentsInChildren<Snui_Text>(true)) label.SetText(text);
		foreach (TMP_Text label in node.GetComponentsInChildren<TMP_Text>(true)) label.text = text;
		foreach (Text label in node.GetComponentsInChildren<Text>(true)) label.text = text;
	}
	public static void HideNamed(Transform root, string name)
	{
		Transform node = HierarchyReferenceCache.Descendant(root, name);
		if (node != null) node.gameObject.SetActive(false);
	}
}
