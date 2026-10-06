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
	[SerializeField] private TMP_Text _coinPriceLabel;
	private Func<bool> _isValid;
	private bool _processing;
	public int Price { get; private set; }
	public string ToolKey => _toolKey;
	protected override void Awake()
	{
		base.Awake();
		_adBtn = _adBtn != null ? _adBtn : RecoveryDialogUI.ResolveButton(transform, "AdBtn");
		_coinBtn = _coinBtn != null ? _coinBtn : RecoveryDialogUI.ResolveButton(transform, "CoinBtn");
		_closeBtn = _closeBtn != null ? _closeBtn : RecoveryDialogUI.ResolveButton(transform, "CloseButton");
		if (_titleText == null) _titleText = HierarchyReferenceCache.Descendant(transform, "Title");
		if (_desc == null) _desc = HierarchyReferenceCache.Descendant(transform, "Desc");
		if (_icon == null) _icon = HierarchyReferenceCache.Descendant(transform, "ToolBackLigh");
		if (_coinPriceLabel == null && _coinBtn != null)
			_coinPriceLabel = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Descendant(_coinBtn.transform, "Count"));
		RecoveryDialogUI.Bind(_adBtn, OnClickOfflineFreeButton);
		RecoveryDialogUI.Bind(_coinBtn, OnClickCoinBtn);
		RecoveryDialogUI.Bind(_closeBtn, OnClickCloseButton);
	}
	public void Init(string toolKey, Func<bool> isValid = null)
	{
		_toolKey = toolKey;
		_isValid = isValid;
		_processing = false;
		// Native default group: Undo 450, Shuffle 900, Path 900.
		Price = toolKey == InventoryKeys.TOOL_UNDO ? 450 : 900;
		RecoveryDialogUI.Label(_titleText, toolKey == InventoryKeys.TOOL_PATH ? "ADD SPACE" : (toolKey ?? "BOOSTER").ToUpperInvariant());
		RecoveryDialogUI.Label(_desc, toolKey == InventoryKeys.TOOL_UNDO ? "Undo the last move" :
			toolKey == InventoryKeys.TOOL_SHUFFLE ? "Shuffle the colors in available boxes" : "Add space to the belt");
		if (_icon != null)
			foreach (Transform child in _icon)
				if (InventoryManager.IsBoosterKey(child.name)) child.gameObject.SetActive(child.name == toolKey);
		if (_coinPriceLabel != null) _coinPriceLabel.text = Price.ToString();
		else if (_coinBtn != null) RecoveryDialogUI.Label(HierarchyReferenceCache.Descendant(_coinBtn.transform, "Count"), Price.ToString());
		if (_adBtn != null)
		{
			RecoveryDialogUI.HideNamed(_adBtn.transform, "Ad");
			RecoveryDialogUI.Label(_adBtn.transform, "FREE (OFFLINE)");
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
		else { _processing = false; RecoveryDialogUI.Label(_desc, "Not enough coins. You need " + Price + "."); }
	}
	public void OnClickOfflineFreeButton()
	{
		if (!CanPurchase) return;
		_processing = true;
		if (InventoryManager.Ensure().Add(_toolKey, 1, "offline_free")) Dismiss(RESULT_PURCHASED);
		else _processing = false;
	}
}

// Bind hierarchy nodes where the prefab export omitted native private references.
public static class RecoveryDialogUI
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
