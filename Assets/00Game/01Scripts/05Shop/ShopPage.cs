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

	private void Awake()
	{
		var pageView = GetComponent<PageViewCell>();
		if (pageView == null)
		{
			pageView = gameObject.AddComponent<PageViewCell>();
		}
		pageView.OnEnterPage -= OnEnterPage;
		pageView.OnEnterPage += OnEnterPage;
	}

	private void OnEnable()
	{
		if (_content == null)
		{
			_contentLoaded = false;
		}
		LoadContentAsync().Forget();
	}

	private void Start()
	{
		LoadContentAsync().Forget();
	}

	private void OnEnterPage()
	{
		LoadContentAsync().Forget();
	}

	private async UniTaskVoid LoadContentAsync()
	{
		if (_contentLoaded && _content != null) return;
		_contentLoaded = true;

		if (_safeArea == null)
		{
			_safeArea = HierarchyReferenceCache.Child(transform, "SafeArea")
				?? HierarchyReferenceCache.Descendant(transform, "SafeArea")
				?? transform;
		}

		if (_safeArea is RectTransform rt)
		{
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
		}

		_content = await ShopDialogContent.Show(_safeArea, new ShopDelegate(), "ShopPage");
		if (_content != null)
		{
			_content.HideCloseBtn();
			float navBottom = NavigationBar.GetNavigationBottomOffset();
			_content.SetContentBottom((int)navBottom);
		}
	}
}
