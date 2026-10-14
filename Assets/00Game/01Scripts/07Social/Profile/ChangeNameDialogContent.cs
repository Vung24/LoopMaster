using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Profile
{
	public class ChangeNameDialogContent : DialogContent
	{
		public const int RESULT_NAME_OK = 1;

		[SerializeField] private TMP_Text _desc;
		[SerializeField] private TMP_InputField _input;
		[SerializeField] private Button _freeBtn;
		[SerializeField] private Button _continueBtn;

		protected override void Awake()
		{
			base.Awake();
			if (_desc == null) _desc = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Desc");
			if (_input == null) _input = HierarchyReferenceCache.DescendantComponent<TMP_InputField>(transform, "InputField");
			if (_freeBtn == null) _freeBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "FreeButton");
			if (_continueBtn == null) _continueBtn = HierarchyReferenceCache.DescendantComponent<Button>(transform, "ContinueButton");
		}
	}
}