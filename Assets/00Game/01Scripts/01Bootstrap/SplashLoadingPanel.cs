using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class SplashLoadingPanel : LoadingPanel
{
	// [SerializeField] private Transform _term;
	// [SerializeField] private Transform _privacy;

	protected override UniTask PlayShowAnim()
	{
		if (_canvasGroup != null) _canvasGroup.alpha = 1f;
		return UniTask.CompletedTask;
	}

	protected override float GetMinLoadingTime()
	{
		return 3.0f;
	}

	protected override void Awake()
	{
		base.Awake();
		// // if (_term == null) _term = HierarchyReferenceCache.Child(transform, "TermBtn");
		// // if (_privacy == null) _privacy = HierarchyReferenceCache.Child(transform, "PrivacyBtn");

		// // if (_term != null)
		// {
		// 	// var btn = _term.GetComponent<Button>();
		// 	// if (btn != null)
		// 	// {
		// 		// btn.onClick.AddListener(OnClickTermButton);
		// 	}
		// }

		// if (_privacy != null)
		// {
		// 	var btn = _privacy.GetComponent<Button>();
		// 	if (btn != null)
		// 	{
		// 		btn.onClick.AddListener(OnClickPrivacyButton);
		// 	}
		// }
	}

	private void OnClickTermButton()
	{
		Application.OpenURL("https://policies.google.com/terms");
	}

	private void OnClickPrivacyButton()
	{
		Application.OpenURL("https://policies.google.com/privacy");
	}
}
