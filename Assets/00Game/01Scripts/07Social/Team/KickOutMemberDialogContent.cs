using UnityEngine;
using UnityEngine.UI;

namespace Snui.Team
{
	public class KickOutMemberDialogContent : DialogContent
	{
		[SerializeField] private Button _yesBtn;
		[SerializeField] private Button _noBtn;

		protected override void Awake()
		{
			base.Awake();
			if (_yesBtn == null) _yesBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "YesButton");
			if (_noBtn == null) _noBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "NoButton");
		}
	}
}