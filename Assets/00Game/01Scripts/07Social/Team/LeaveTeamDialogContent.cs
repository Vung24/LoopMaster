using UnityEngine;
using UnityEngine.UI;

namespace Snui.Team
{
	public class LeaveTeamDialogContent : DialogContent
	{
		[SerializeField] private Button _leaveBtn;
		[SerializeField] private Button _cancelBtn;

		protected override void Awake()
		{
			base.Awake();
			if (_leaveBtn == null) _leaveBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "LeaveButton");
			if (_cancelBtn == null) _cancelBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "CancelButton");
		}
	}
}