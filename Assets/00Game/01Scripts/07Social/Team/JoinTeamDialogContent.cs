using UnityEngine;
using UnityEngine.UI;

namespace Snui.Team
{
	public class JoinTeamDialogContent : DialogContent
	{
		[SerializeField] private Image _headImg;
		[SerializeField] private Button _joinBtn;

		protected override void Awake()
		{
			base.Awake();
			if (_headImg == null) _headImg = HierarchyReferenceCache.DescendantComponent<Image>(transform, "HeadImg");
			if (_joinBtn == null) _joinBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "JoinButton");
		}
	}
}