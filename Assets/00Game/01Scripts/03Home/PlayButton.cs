using UnityEngine;
using UnityEngine.UI;

public class PlayButton : MonoBehaviour
{
	private HomePage _homePage;
	private Button _button;
	private void Awake()
	{
		_button = GetComponent<Button>();
		_homePage = GetComponentInParent<HomePage>(true);
		if (_button != null) _button.onClick.AddListener(OnClick);
	}
	public void Bind(HomePage home) => _homePage = home;
	public void OnClick() { if (isActiveAndEnabled && (_button == null || _button.IsInteractable())) _homePage?.OnClickPlay(); }
	private void OnDestroy() { if (_button != null) _button.onClick.RemoveListener(OnClick); }
}
