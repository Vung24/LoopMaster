using Cysharp.Threading.Tasks;
using ShopUI;
using Snui;
using Sugar.UI;
using UnityEngine;

public class ShopPage : MonoBehaviour
{
	[SerializeField] private Transform _safeArea;
	private ShopDialogContent _content;
	private bool _contentLoaded;
	private bool _loading;
	private PageViewCell _pageView;

	private void Awake()
	{
		_pageView = GetComponent<PageViewCell>();
		if (_pageView == null)
		{
			_pageView = gameObject.AddComponent<PageViewCell>();
		}
		_pageView.OnEnterPage -= OnEnterPage;
		_pageView.OnEnterPage += OnEnterPage;
	}

	private void OnEnable()
	{
		if (_content == null)
		{
			_contentLoaded = false;
		}
	}

	private void OnDestroy()
	{
		if (_pageView != null) _pageView.OnEnterPage -= OnEnterPage;
	}

	public async UniTask PreloadAsync()
	{
		if (_loading || (_contentLoaded && _content != null)) return;
		await LoadContentInternalAsync();
		if (_content != null)
		{
			await _content.EnsureInitializedAsync(this.GetCancellationTokenOnDestroy());
		}
	}

	private void OnEnterPage()
	{
		LoadContentAsync().Forget();
	}

	private async UniTaskVoid LoadContentAsync()
	{
		if (_loading || (_contentLoaded && _content != null)) return;
		await LoadContentInternalAsync();
	}

	private async UniTask LoadContentInternalAsync()
	{
		if (_loading || (_contentLoaded && _content != null)) return;
		_loading = true;
		try
		{
			if (_safeArea == null)
			{
				_safeArea = HierarchyReferenceCache.Child(transform, "SafeArea")
					?? HierarchyReferenceCache.Descendant(transform, "SafeArea")
					?? transform;
			}

			_content = await ShopDialogContent.Show(_safeArea, new ShopDelegate(), "home_shop");
			if (this == null) { if (_content != null) Destroy(_content.gameObject); return; }
			if (_content != null)
			{
				_contentLoaded = true;
				_content.HideCloseBtn();
				float navBottom = NavigationBar.GetNavigationBottomOffset();
				_content.SetContentBottom((int)navBottom);
			}
		}
		finally { _loading = false; }
	}
}
