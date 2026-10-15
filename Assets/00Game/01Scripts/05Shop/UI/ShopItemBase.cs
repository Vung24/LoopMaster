using Snui;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShopUI
{
	public class ShopItemBase : MonoBehaviour
	{
		public UnityEvent<string> OnClickBuyButton = new UnityEvent<string>();
		[HideInInspector] public string Type;
		[HideInInspector] public int Index;
		[SerializeField] protected Button _buyBtn;
		[SerializeField] protected Snui_Price _price;

		public RectTransform RectTransform => (RectTransform)transform;

		protected virtual void Awake()
		{
			if (_buyBtn == null)
			{
				_buyBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "BuyBtn")
					?? HierarchyReferenceCache.ChildComponent<Button>(transform, "BuyButton")
					?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "BuyBtn")
					?? GetComponentInChildren<Button>(true);
			}

			if (_buyBtn != null)
			{
				_buyBtn.onClick.AddListener(OnClickBuyBtn);
			}

			if (_price == null)
			{
				_price = (_buyBtn == null ? null : _buyBtn.GetComponentInChildren<Snui_Price>(true))
					?? HierarchyReferenceCache.ChildComponent<Snui_Price>(transform, "BuyBtn")
					?? HierarchyReferenceCache.ChildComponent<Snui_Price>(transform, "Snui_Price")
					?? HierarchyReferenceCache.DescendantComponent<Snui_Price>(transform, "Snui_Price")
					?? HierarchyReferenceCache.ChildComponent<Snui_Price>(transform, "Price")
					?? HierarchyReferenceCache.DescendantComponent<Snui_Price>(transform, "Price")
					?? GetComponentInChildren<Snui_Price>(true);
			}
		}

		public virtual void Init(string type, IShopDelegate shopDelegate)
		{
			Type = type;
			SetPrice(type);
		}

		protected void SetPrice(string type)
		{
			if (_price != null)
			{
				_price.SetPrice(type);
			}
		}

		protected virtual void OnClickBuyBtn()
		{
			OnClickBuyButton?.Invoke(Type);
		}

		public virtual void SetEffectsActive(bool active) { }
	}
}
