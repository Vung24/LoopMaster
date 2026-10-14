using UnityEngine;
using UnityEngine.UI;

namespace Snui.Team
{
	public class EditLogoDialogContent : DialogContent
	{
		[SerializeField] private ScrollRect _scrollView;

		protected override void Awake()
		{
			base.Awake();
			if (_scrollView == null) _scrollView = GetComponentInChildren<ScrollRect>(true);
		}
	}
}