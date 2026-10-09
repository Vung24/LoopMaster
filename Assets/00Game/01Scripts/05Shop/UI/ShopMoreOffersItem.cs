using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShopUI
{
	public class ShopMoreOffersItem : ShopItemBase
	{
		public UnityEvent<string> OnClickMoreButton = new UnityEvent<string>();
		[SerializeField] protected Button _moreBtn;

		protected override void Awake()
		{
			base.Awake();
			if (_moreBtn == null)
			{
				_moreBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "MoreBtn")
					?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "MoreBtn")
					?? GetComponentInChildren<Button>(true);
			}

			if (_moreBtn != null)
			{
				if (_moreBtn == _buyBtn) _buyBtn.onClick.RemoveListener(OnClickBuyBtn);
				_moreBtn.onClick.AddListener(OnClickMoreBtn);
			}
		}

		public override void Init(string type, IShopDelegate shopDelegate)
		{
			base.Init(type, shopDelegate);
		}

		private void OnClickMoreBtn()
		{
			OnClickMoreButton?.Invoke(Type);
		}
	}
}
