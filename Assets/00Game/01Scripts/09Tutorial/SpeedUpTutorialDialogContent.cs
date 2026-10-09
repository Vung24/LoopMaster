using Cysharp.Threading.Tasks;
using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeedUpTutorialDialogContent : DialogContent
{
	[SerializeField] private Button _btnClose;
	[SerializeField] private TMP_Text _descText;

	public static UniTask<SpeedUpTutorialDialogContent> Show(Transform parent)
	{
		return Dialog.ShowContent<SpeedUpTutorialDialogContent>(parent,
			"SpeedUpTutorialDialogContent");
	}

	protected override void Awake()
	{
		base.Awake();
		if (_btnClose == null)
		{
			Transform close = HierarchyReferenceCache.Descendant(transform, "CloseButton");
			_btnClose = close == null ? null : close.GetComponent<Button>();
		}
		if (_btnClose != null && _btnClose != _closeBtn)
			_btnClose.onClick.AddListener(OnClickCloseBtn);
		if (_descText == null)
		{
			Transform desc = HierarchyReferenceCache.Descendant(transform, "Desc");
			_descText = desc == null ? null : desc.GetComponent<TMP_Text>();
		}
		if (_descText != null) _descText.text = "Tap the speed button to switch between x1 and x2.";
		Snui_Text.EnsureFontAssets(transform);
	}

	private void OnClickCloseBtn() => Dismiss(RESULT_CLOSE);

	private void OnDestroy()
	{
		if (_btnClose != null && _btnClose != _closeBtn)
			_btnClose.onClick.RemoveListener(OnClickCloseBtn);
	}
}
