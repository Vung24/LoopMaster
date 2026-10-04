using Cysharp.Threading.Tasks;
using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeedUpTutorialDialogContent : DialogContent
{
	private Button _btnClose;

	public static UniTask<SpeedUpTutorialDialogContent> Show(Transform parent)
	{
		return Dialog.ShowContent<SpeedUpTutorialDialogContent>(parent,
			"Game/Prefab/UI/SpeedUpTutorialDialogContent.prefab");
	}

	protected override void Awake()
	{
		base.Awake();
		Transform close = HierarchyReferenceCache.Descendant(transform, "CloseButton");
		_btnClose = close == null ? null : close.GetComponent<Button>();
		if (_btnClose != null && _btnClose != _closeBtn)
			_btnClose.onClick.AddListener(OnClickCloseBtn);
		Transform desc = HierarchyReferenceCache.Descendant(transform, "Desc");
		TMP_Text label = desc == null ? null : desc.GetComponent<TMP_Text>();
		if (label != null) label.text = "Tap the speed button to switch between 2x and 4x.";
		Snui_Text.EnsureFontAssets(transform);
	}

	private void OnClickCloseBtn() => Dismiss(RESULT_CLOSE);

	private void OnDestroy()
	{
		if (_btnClose != null && _btnClose != _closeBtn)
			_btnClose.onClick.RemoveListener(OnClickCloseBtn);
	}
}
