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
				Transform buyTrans = HierarchyReferenceCache.Child(transform, "BuyButton");
				if (buyTrans != null)
				{
					_buyBtn = buyTrans.GetComponent<Button>();
				}
				if (_buyBtn == null)
				{
					_buyBtn = GetComponentInChildren<Button>();
				}
			}
			if (_buyBtn != null)
			{
				_buyBtn.onClick.AddListener(OnClickBuyBtn);
			}

			if (_price == null)
			{
				_price = GetComponentInChildren<Snui_Price>();
			}
		}

		public virtual void Init(string type, IShopDelegate shopDelegate)
		{
			Type = type;
			SetPrice(type);
		}

		private void SetPrice(string type)
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
	}
}