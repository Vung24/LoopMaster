using Snui;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

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
	private Sequence _shakeTween;
	private Vector3 _originBgScale;
	private InventoryManager _inventory;

	private void OnEnable()
	{
		_inventory = InventoryManager.Ensure();
		_inventory.OnInventoryChanged.AddListener(RefreshInventory);
		if (AutoUpdate) SetNumber(_inventory.GetAmount(InventoryKeys.COIN));
	}
	private void OnDisable()
	{
		if (_inventory != null) _inventory.OnInventoryChanged.RemoveListener(RefreshInventory);
		StopNumberAnimation();
		ResetShake();
	}
	protected override void Start() { if (AutoUpdate && _inventory != null) SetNumber(_inventory.GetAmount(InventoryKeys.COIN)); }
	public override void SetAutoUpdate(bool isAuto)
	{
		base.SetAutoUpdate(isAuto);
		StopNumberAnimation();
		if (!isAuto || _inventory == null) return;
		int balance = _inventory.GetAmount(InventoryKeys.COIN);
		int added = balance - _currentNumber;
		IncreaseTo(balance, _increaseAnimTime);
		if (added > 0) PlayAddNumberAnimation(added);
	}
	private void RefreshInventory(string key, int delta)
	{
		if (AutoUpdate && key == InventoryKeys.COIN && _inventory != null)
			IncreaseTo(_inventory.GetAmount(InventoryKeys.COIN), _increaseAnimTime);
	}

	protected override void Awake()
	{
		base.Awake();
		InventoryKey = InventoryKeys.COIN;
		_originBgScale = _bgNode != null ? _bgNode.localScale : Vector3.one;
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
		if (_coinIcon != null && _coinIcon.sprite == null && PrefabLibrary.Current != null)
			_coinIcon.sprite = PrefabLibrary.Current.HudCoinSprite;
	}

	public override void PlayShakeAnimation()
	{
		if (_isShaking) return;
		_isShaking = true;
		if (_bgNode == null) { _isShaking = false; return; }
		_shakeTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
		_shakeTween.Append(_bgNode.DOScale(Vector3.Scale(_originBgScale, new Vector3(0.85f, 0.85f, 1f)), 0.03f));
		_shakeTween.Append(_bgNode.DOScale(Vector3.Scale(_originBgScale, new Vector3(0.84f, 0.84f, 1f)), 0.06f));
		_shakeTween.Append(_bgNode.DOScale(_originBgScale, 0.03f));
		_shakeTween.SetLoops(6).OnStepComplete(() => { if (!_isShaking) ResetShake(); }).OnComplete(ResetShake);
	}

	public void StopShakeAnimation() => _isShaking = false;
	private void ResetShake()
	{
		_shakeTween?.Kill();
		_shakeTween = null;
		_isShaking = false;
		if (_bgNode != null) _bgNode.localScale = _originBgScale;
	}
	public override void PlayAddNumberAnimation(int num) => ShowAddNumberToast(num, new Vector2(125f, -40f));
	protected void ShowAddNumberToast(int num, Vector2 offset)
	{
		if (num > 0 && isActiveAndEnabled) AddNumberToast.Show(transform, "+" + num, offset, 1f, 42f);
	}
	public void DisableClick(bool isDisplayAddImg = false)
	{
		if (_button != null)
		{
			_button.enabled = false;
			// Clear any disabled/pressed tint left by an earlier interaction.
			if (_button.targetGraphic != null) _button.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
		}
		if (_addIcon != null) _addIcon.gameObject.SetActive(isDisplayAddImg);
	}
	public void EnableClick() { if (_button != null) { _button.enabled = true; _button.interactable = true; } if (_addIcon != null) _addIcon.gameObject.SetActive(true); }

	public static void ResetCurrentParent()
	{
		if (Current == null || Current._originParent == null) return;
		Current.transform.SetParent(Current._originParent, false);
		Current.transform.localPosition = Current._originLocalPos;
	}

	public static void SetCurrentAutoUpdate(bool autoUpdate) { if (Current != null) Current.SetAutoUpdate(autoUpdate); }
	public static void SetCurrentParent(Transform parent) { if (Current != null && parent != null) Current.transform.SetParent(parent, false); }

	protected override void OnDestroy()
	{
		if (Current == this) Current = null;
		ResetShake();
		base.OnDestroy();
	}
}
