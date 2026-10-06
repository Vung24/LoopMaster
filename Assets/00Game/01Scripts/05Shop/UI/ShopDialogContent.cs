using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using UnityEngine;
using UnityEngine.UI;

namespace ShopUI
{
	public class ShopDialogContent : DialogContent
	{
		public static int RESULT_MORE_OFFER = 1;
		[HideInInspector] public string Route = "";
		public RectOffset Padding;
		public float Spacing = 20f;
		public bool UploadShowingLog = true;
		[SerializeField] private ScrollRect _scrollList;
		private List<ShopItemBase> _items = new List<ShopItemBase>();
		private IShopDelegate _delegate;

		public IShopDelegate Delegate { set => _delegate = value; }

		public static async UniTask<ShopDialogContent> Show(Transform parent, IShopDelegate shopDelegate, string route)
		{
			var settings = ShopUISettings_SO.Instance;
			ShopDialogContent content = null;
			if (settings != null && settings.ShopDialogContentPrefab != null && settings.ShopDialogContentPrefab.RuntimeKeyIsValid())
			{
				content = await Dialog.ShowContent<ShopDialogContent>(parent, settings.ShopDialogContentPrefab);
			}

			if (content == null)
			{
				var prefab = RecoveryPrefabLibrary.Current?.ShopDialogContentPrefab
					?? RecoveryPrefabLibrary.Current?.Resolve("ShopDialogContent")
					?? Resources.Load<GameObject>("Shop/Prefab/ShopDialogContent")
					?? Resources.Load<GameObject>("04Prefabs/Shop/ShopDialogContent")
					?? Resources.Load<GameObject>("ShopUI/Prefab/ShopDialogContent");

				if (prefab != null)
				{
					var go = Instantiate(prefab, parent, false);
					var rt = go.GetComponent<RectTransform>();
					if (rt != null)
					{
						rt.anchorMin = Vector2.zero;
						rt.anchorMax = Vector2.one;
						rt.offsetMin = Vector2.zero;
						rt.offsetMax = Vector2.zero;
						rt.localScale = Vector3.one;
					}
					content = go.GetComponent<ShopDialogContent>();
				}
			}

			if (content != null)
			{
				content.Delegate = shopDelegate;
				content.Route = route;
			}

			return content;
		}

		public void HideCloseBtn()
		{
			if (_closeBtn != null)
			{
				_closeBtn.gameObject.SetActive(false);
			}
		}

		public void SetContentBottom(int bottom)
		{
			if (Padding == null) Padding = new RectOffset();
			Padding.bottom = bottom;
			Relayout();
		}

		protected override void Awake()
		{
			base.Awake();
			if (Padding == null) Padding = new RectOffset();
			if (_scrollList == null)
			{
				_scrollList = HierarchyReferenceCache.ChildComponent<ScrollRect>(transform, "ShopList")
					?? HierarchyReferenceCache.ChildComponent<ScrollRect>(transform, "ScrollList")
					?? HierarchyReferenceCache.DescendantComponent<ScrollRect>(transform, "ShopList")
					?? HierarchyReferenceCache.DescendantComponent<ScrollRect>(transform, "ScrollList")
					?? GetComponentInChildren<ScrollRect>(true);
			}

			if (_closeBtn == null)
			{
				_closeBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "CloseButton")
					?? HierarchyReferenceCache.ChildComponent<Button>(transform, "CloseBtn")
					?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseButton")
					?? HierarchyReferenceCache.DescendantComponent<Button>(transform, "CloseBtn")
					?? GetComponentInChildren<Button>(true);
			}

			if (_items == null)
			{
				_items = new List<ShopItemBase>();
			}
		}

		protected override void Start()
		{
			base.Start();
			if (_closeBtn != null)
			{
				_closeBtn.onClick.AddListener(() => Hide());
			}

			InitAsync(this.GetCancellationTokenOnDestroy()).Forget();
		}

		private void OnDestroy()
		{
			if (_items != null)
			{
				foreach (var item in _items)
				{
					if (item != null) item.transform.DOKill();
				}
			}
		}

		private async UniTask InitAsync(CancellationToken token)
		{
			if (_delegate == null) _delegate = new ShopDelegate();
			var products = _delegate.GetShopProducts();
			if (products == null || products.Count == 0) return;

			var tasks = new List<UniTask>();
			for (int i = 0; i < products.Count; i++)
			{
				string product = products[i];
				if (!_delegate.IsLimitShowProduct(product))
				{
					tasks.Add(AddItems(product, i, token));
				}
			}

			await UniTask.WhenAll(tasks);
			if (token.IsCancellationRequested) return;

			UpdateItemOrder();
			Relayout();
			PlayItemsEnterAnimation();
		}

		private void RefreshShop()
		{
			if (_items == null || _delegate == null) return;
			bool changed = false;
			for (int i = _items.Count - 1; i >= 0; i--)
			{
				var item = _items[i];
				if (item != null && _delegate.IsLimitShowProduct(item.Type))
				{
					Destroy(item.gameObject);
					_items.RemoveAt(i);
					changed = true;
				}
			}

			if (changed)
			{
				Relayout();
			}
		}

		private void Relayout()
		{
			if (_scrollList == null || _items == null) return;
			if (Padding == null) Padding = new RectOffset();

			Transform contentTrans = _scrollList.content != null ? _scrollList.content : _scrollList.transform;
			RectTransform contentRt = contentTrans as RectTransform;

			float totalItemHeights = 0f;
			for (int i = 0; i < _items.Count; i++)
			{
				if (_items[i] != null && _items[i].RectTransform != null)
				{
					totalItemHeights += _items[i].RectTransform.rect.height;
				}
			}

			float contentHeight = totalItemHeights + Padding.top + Padding.bottom + Spacing * Mathf.Max(0, _items.Count - 1);
			if (contentRt != null)
			{
				contentRt.sizeDelta = new Vector2(contentRt.sizeDelta.x, contentHeight);
			}

			float currentY = 0f;
			for (int i = 0; i < _items.Count; i++)
			{
				var item = _items[i];
				if (item == null || item.RectTransform == null) continue;

				var rt = item.RectTransform;
				rt.pivot = new Vector2(0.5f, 0.5f);
				rt.anchorMin = new Vector2(0.5f, 1f);
				rt.anchorMax = new Vector2(0.5f, 1f);
				float h = rt.rect.height;
				float x = (Padding.left - Padding.right) * 0.5f;
				float y = -Padding.top - currentY - h * 0.5f - Spacing * i;
				rt.anchoredPosition = new Vector2(x, y);
				currentY += h;
			}
		}

		private void UpdateItemOrder()
		{
			if (_items == null) return;
			_items = _items.OrderBy(item => item.Index).ToList();
			for (int i = 0; i < _items.Count; i++)
			{
				if (_items[i] != null)
				{
					_items[i].transform.SetSiblingIndex(_items[i].Index);
				}
			}
		}

		private async UniTask AddItems(string type, int index, CancellationToken token)
		{
			string resPath = _delegate != null ? _delegate.GetProductResPath(type) : type;
			GameObject prefab = null;

			if (RecoveryPrefabLibrary.Current != null && !string.IsNullOrEmpty(resPath))
			{
				prefab = RecoveryPrefabLibrary.Current.Resolve(resPath);
			}

			if (prefab == null && !string.IsNullOrEmpty(resPath))
			{
				prefab = Resources.Load<GameObject>(resPath)
					?? Resources.Load<GameObject>("Shop/Prefab/" + System.IO.Path.GetFileNameWithoutExtension(resPath))
					?? Resources.Load<GameObject>("04Prefabs/Shop/" + System.IO.Path.GetFileNameWithoutExtension(resPath));
			}

			if (prefab == null && RecoveryPrefabLibrary.Current != null)
			{
				if (type.IndexOf("more", StringComparison.OrdinalIgnoreCase) >= 0)
					prefab = RecoveryPrefabLibrary.Current.ShopMoreOffersItemPrefab;
				else if (type.IndexOf("coin", StringComparison.OrdinalIgnoreCase) >= 0)
					prefab = RecoveryPrefabLibrary.Current.ShopCoinItemPrefab;
				else
					prefab = RecoveryPrefabLibrary.Current.ShopBundleItemPrefab;
			}

			if (prefab == null)
			{
				if (type.IndexOf("more", StringComparison.OrdinalIgnoreCase) >= 0)
					prefab = Resources.Load<GameObject>("Shop/Prefab/MoreOffer") ?? Resources.Load<GameObject>("04Prefabs/Shop/MoreOffer");
				else if (type.IndexOf("coin", StringComparison.OrdinalIgnoreCase) >= 0)
					prefab = Resources.Load<GameObject>("Shop/Prefab/CoinItem") ?? Resources.Load<GameObject>("04Prefabs/Shop/CoinItem");
				else
					prefab = Resources.Load<GameObject>("Shop/Prefab/BundleBase") ?? Resources.Load<GameObject>("04Prefabs/Shop/BundleBase");
			}

			Transform container = _scrollList != null && _scrollList.content != null ? _scrollList.content : transform;
			if (prefab != null)
			{
				var go = Instantiate(prefab, container, false);
				var item = go.GetComponent<ShopItemBase>();
				if (item == null)
				{
					if (type.IndexOf("more", StringComparison.OrdinalIgnoreCase) >= 0)
						item = go.AddComponent<ShopMoreOffersItem>();
					else if (type.IndexOf("coin", StringComparison.OrdinalIgnoreCase) >= 0)
						item = go.AddComponent<ShopCoinItem>();
					else
						item = go.AddComponent<ShopBundleItem>();
				}

				item.Init(type, _delegate);
				item.Index = index;
				item.OnClickBuyButton.AddListener(OnItemClickBuy);

				if (item is ShopMoreOffersItem moreOffer)
				{
					moreOffer.OnClickMoreButton.AddListener(OnClickMoreOffer);
				}

				item.gameObject.SetActive(false);
				_items.Add(item);
			}

			await UniTask.Yield();
		}

		private void OnItemClickBuy(string type)
		{
			int coinReward = 0;
			var match = System.Text.RegularExpressions.Regex.Match(type ?? "", @"\d+");
			if (match.Success && int.TryParse(match.Value, out int count))
			{
				coinReward = count;
			}
			else
			{
				coinReward = 1000;
			}

			int currentCoins = PlayerPrefs.GetInt("GameCoins", 1000);
			PlayerPrefs.SetInt("GameCoins", currentCoins + coinReward);
			PlayerPrefs.Save();

			OnPurchaseFinish(type, true);
		}

		private void OnPurchaseFinish(string product, bool isSuccess)
		{
			if (!isSuccess) return;
			CheckRefreshShop(product).Forget();
		}

		private async UniTaskVoid CheckRefreshShop(string product)
		{
			await UniTask.Delay(100);
			if (_delegate != null && _delegate.IsLimitShowProduct(product))
			{
				RefreshShop();
			}
		}

		private void OnClickMoreOffer(string type)
		{
			if (_scrollList != null)
			{
				_scrollList.DOVerticalNormalizedPos(1f, 0.3f).SetLink(_scrollList.gameObject);
			}
			else
			{
				Dismiss(RESULT_MORE_OFFER);
			}
		}

		private void PlayItemsEnterAnimation()
		{
			if (_items == null || _items.Count == 0) return;

			float screenWidth = Screen.width;
			RectTransform rect = transform as RectTransform;
			if (rect != null && rect.rect.width > 0)
			{
				screenWidth = rect.rect.width;
			}

			for (int i = 0; i < _items.Count; i++)
			{
				var item = _items[i];
				if (item == null) continue;

				item.gameObject.SetActive(true);
				Vector3 pos = item.transform.localPosition;
				float targetX = pos.x;
				pos.x -= screenWidth;
				item.transform.localPosition = pos;

				float delay = item.Index * 0.05f;
				var seq = DOTween.Sequence();
				seq.Append(item.transform.DOLocalMoveX(targetX + 20f, 0.37f).SetEase(Ease.OutQuad));
				seq.Append(item.transform.DOLocalMoveX(targetX, 0.28f).SetEase(Ease.OutCubic));
				seq.SetDelay(delay);
				seq.SetLink(item.gameObject);
			}
		}
	}
}
