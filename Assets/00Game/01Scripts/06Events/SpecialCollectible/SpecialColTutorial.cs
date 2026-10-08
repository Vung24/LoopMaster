using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.SpecialCol
{
	public class SpecialColTutorial : MonoBehaviour
	{
		[SerializeField]
		private TMP_Text _descText;
		[SerializeField]
		private Button _continueBtn;

		public static UniTask<SpecialColTutorial> Show(Transform parent) => EventPrefabUI.Show<SpecialColTutorial>(parent, "SpecialColTutorial");

		private void Start()
		{
			if (Managers.SpecialCol != null)
			{
				Managers.SpecialCol.SetTutorialShown();
				if (_descText != null)
				{
					_descText.text = "Collect items while playing levels to reach milestones and earn rewards.";
				}
			}

			if (_continueBtn == null)
			{
				_continueBtn = GetComponentInChildren<Button>(true);
			}

			if (_continueBtn != null)
			{
				_continueBtn.onClick.AddListener(OnClickClose);
			}
		}

		private void OnClickClose()
		{
			Destroy(gameObject);
		}
	}
}