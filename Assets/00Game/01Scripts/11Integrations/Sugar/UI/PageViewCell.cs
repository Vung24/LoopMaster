using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Sugar.UI
{
	[RequireComponent(typeof(RectTransform))]
	public class PageViewCell : MonoBehaviour
	{
		public UnityAction OnEnterPage;
		private ScrollRect _scrollRect;

		private void Awake()
		{
			_scrollRect = GetComponentInChildren<ScrollRect>(true);
		}

		public void HideContent()
		{
			gameObject.SetActive(false);
		}

		public void ShowContent()
		{
			gameObject.SetActive(true);
			OnEnterPage?.Invoke();
		}
	}
}
