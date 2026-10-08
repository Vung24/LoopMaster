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
		[SerializeField] private Image _ribbonImage;
		private List<ShopItemBase> _items = new List<ShopItemBase>();
		private IShopDelegate _delegate;
		private bool _initializing;
		private bool _initialized;
		private readonly List<Sequence> _itemAnimations = new List<Sequence>();

		public IShopDelegate Delegate { set => _delegate = value; }

		public static async UniTask<ShopDialogContent> Show(Transform parent, IShopDelegate shopDelegate, string route)
		{
			HeartManager.EnsureInitialized();
			if (parent == null) return null;
			var content = await PopupAssets.Instantiate<ShopDialogContent>("ShopDialogContent", parent, parent.GetCancellationTokenOnDestroy());

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
			if (_closeBtn != null)
			{
				_closeBtn.onClick.RemoveListener(OnClickCloseButton);
				_closeBtn.onClick.AddListener(OnClickCloseButton);
			}
			OfflineShopPurchases.PurchaseFinished += OnPurchaseFinish;
		}

		protected override void Start()
		{
			base.Start();
			InitAsync(this.GetCancellationTokenOnDestroy()).Forget();
			if (_ribbonImage != null)
			{
				var ribbon = _ribbonImage.rectTransform;
				ribbon.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
					ribbon.rect.height + ScreenHelper.GetCanvasNotchHeight(_canvas));
			}
		}

		private void OnEnable()
		{
			if (_initialized) RefreshShop();
		}

		private void OnDisable()
		{
			StopItemAnimations(true);
		}

		private void StopItemAnimations(bool complete)
		{
			foreach (var animation in _itemAnimations)
				if (animation != null && animation.IsActive()) animation.Kill(complete);
			_itemAnimations.Clear();
		}

		private void OnDestroy()
		{
			OfflineShopPurchases.PurchaseFinished -= OnPurchaseFinish;
			StopItemAnimations(false);
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
			if (_initialized || _initializing) return;
			_initializing = true;
			try
			{
				if (_delegate == null) _delegate = new ShopDelegate();
				var products = _delegate.GetShopProducts();
				if (products == null) return;

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
				_initialized = true;
				if (isActiveAndEnabled) PlayItemsEnterAnimation();
				else foreach (var item in _items) if (item != null) item.gameObject.SetActive(true);
			}
			finally { _initializing = false; }
		}

		private void RefreshShop()
		{
			if (_items == null || _delegate == null) return;
			StopItemAnimations(true);
			bool changed = false;
			for (int i = _items.Count - 1; i >= 0; i--)
			{
				var item = _items[i];
				if (item != null && _delegate.IsLimitShowProduct(item.Type))
				{
					item.gameObject.SetActive(false);
					Destroy(item.gameObject);
					_items.RemoveAt(i);
					changed = true;
				}
			}

			if (changed)
			{
				UpdateItemOrder();
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
				contentRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
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
				float x = Padding.left - Padding.right;
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
			token.ThrowIfCancellationRequested();
			string resPath = _delegate != null ? _delegate.GetProductResPath(type) : type;
			GameObject prefab = null;

			if (PrefabLibrary.Current != null && !string.IsNullOrEmpty(resPath))
			{
				prefab = PrefabLibrary.Current.Resolve(resPath);
			}

			if (prefab == null && !string.IsNullOrEmpty(resPath))
			{
				prefab = Resources.Load<GameObject>(resPath.EndsWith(".prefab") ? resPath.Substring(0, resPath.Length - 7) : resPath)
					?? Resources.Load<GameObject>("Shop/Prefab/" + System.IO.Path.GetFileNameWithoutExtension(resPath))
					?? Resources.Load<GameObject>("04Prefabs/Shop/" + System.IO.Path.GetFileNameWithoutExtension(resPath));
			}

			if (prefab == null && PrefabLibrary.Current != null)
			{
				if (type.IndexOf("more", StringComparison.OrdinalIgnoreCase) >= 0)
					prefab = PrefabLibrary.Current.ShopMoreOffersItemPrefab;
				else if (type.IndexOf("coin", StringComparison.OrdinalIgnoreCase) >= 0)
					prefab = PrefabLibrary.Current.ShopCoinItemPrefab;
				else
					prefab = PrefabLibrary.Current.ShopBundleItemPrefab;
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

			token.ThrowIfCancellationRequested();
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

			else Debug.LogWarning("Shop prefab is missing for " + type, this);
			await UniTask.Yield(token);
		}

		private void OnItemClickBuy(string type)
		{
			OfflineShopPurchases.Purchase(type, Route);
		}

		private void OnPurchaseFinish(string product, bool isSuccess)
		{
			if (!isSuccess) return;
			CheckRefreshShop(product).Forget();
		}

		private async UniTaskVoid CheckRefreshShop(string product)
		{
			await UniTask.WaitUntil(() => !OfflineShopPurchases.IsBusy,
				cancellationToken: this.GetCancellationTokenOnDestroy());
			if (_delegate != null && _delegate.IsLimitShowProduct(product))
			{
				RefreshShop();
			}
		}

		private void OnClickMoreOffer(string type)
		{
			Dismiss(RESULT_MORE_OFFER);
		}

		private void PlayItemsEnterAnimation()
		{
			if (_items == null || _items.Count == 0) return;
			StopItemAnimations(true);

			float screenWidth = Screen.width;
			RectTransform rect = _canvas == null ? transform as RectTransform : _canvas.rootCanvas.transform as RectTransform;
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

				item.transform.DOKill();
				float delay = item.Index * 0.066f;
				var seq = DOTween.Sequence();
				seq.Append(item.transform.DOLocalMoveX(targetX + 74f, 0.37f).SetEase(Ease.OutCubic));
				seq.Append(item.transform.DOLocalMoveX(targetX, 0.28f).SetEase(Ease.InOutSine));
				seq.SetDelay(delay);
				seq.SetLink(gameObject);
				_itemAnimations.Add(seq);
			}
		}
	}
}
