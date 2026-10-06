using UnityEngine;
using TMPro;

public class ShopPage : MonoBehaviour
{
	[SerializeField] private RectTransform _safeArea;
	[SerializeField] private RectTransform _offlineMessageTemplate;
	[SerializeField] private TMP_Text _titleTemplate;
	private void Awake()
	{
		// The recovered shop delegate has no offline products. Keep its tab navigable
		// and use the scene's authored connection message instead of an empty page.
		if (_safeArea == null) _safeArea = HierarchyReferenceCache.Child(transform, "SafeArea") as RectTransform;
		if (_safeArea == null) return;
		if (_titleTemplate != null)
		{
			var title = Instantiate(_titleTemplate, _safeArea, false);
			title.text = "SHOP";
			title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
			title.rectTransform.anchoredPosition = new Vector2(0f, -75f);
		}
		if (_offlineMessageTemplate != null)
		{
			var message = Instantiate(_offlineMessageTemplate, _safeArea, false);
			message.gameObject.SetActive(true);
			foreach (var label in message.GetComponentsInChildren<TMP_Text>(true))
				label.text = label.name == "Title" ? "Unavailable offline" : "The shop is unavailable in offline mode.";
		}
	}
}
