using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShopUI
{
	public interface IShopDelegate
	{
		List<string> GetShopProducts();
		bool IsLimitShowProduct(string id);
		[Obsolete] void ClaimFreeCoins(int amount);
		string GetProductResPath(string id);
	}

	public class ShopDelegate : IShopDelegate
	{
		private GameObject _loading;

		public virtual List<string> GetShopProducts()
		{
			return new List<string>();
		}

		public bool IsLimitShowProduct(string type)
		{
			return false;
		}

		public void ClaimFreeCoins(int amount)
		{
		}

		public virtual string GetProductResPath(string id)
		{
			return id;
		}
	}
}