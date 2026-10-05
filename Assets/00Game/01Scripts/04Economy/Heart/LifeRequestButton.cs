using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LifeRequestButton : MonoBehaviour
{
	[SerializeField] private RawImage _requestHeart;
	[SerializeField] private TMP_Text _requestText;
	[SerializeField] private Transform _timeNode;
	[SerializeField] private TMP_Text _requestTime;
	[SerializeField] private Texture _normalIconTexture;
	[SerializeField] private Texture _disableIconTexture;
	private Button _button;

	public Button.ButtonClickedEvent onClick => _button != null ? _button.onClick : null;

	private void Awake()
	{
		_button = GetComponent<Button>();
	}

	private void Start()
	{
		UpdateContent();
	}

	public void UpdateContent()
	{
		long remainTime = Managers.Team != null ? Managers.Team.RequestRemainTime : 0;
		if (remainTime <= 0)
		{
			if (_button != null)
			{
				_button.interactable = true;
			}
			if (_timeNode != null)
			{
				_timeNode.gameObject.SetActive(false);
			}
			if (_requestText != null)
			{
				_requestText.gameObject.SetActive(true);
			}
			if (_requestHeart != null && _normalIconTexture != null)
			{
				_requestHeart.texture = _normalIconTexture;
			}
			CancelInvoke(nameof(CountdownTime));
		}
		else
		{
			if (_button != null)
			{
				_button.interactable = false;
			}
			if (_timeNode != null)
			{
				_timeNode.gameObject.SetActive(true);
			}
			if (_requestText != null)
			{
				_requestText.gameObject.SetActive(false);
			}
			if (_requestHeart != null && _disableIconTexture != null)
			{
				_requestHeart.texture = _disableIconTexture;
			}
			if (_requestTime != null)
			{
				_requestTime.text = TimeFormatter.FormatSeconds(remainTime);
			}
			CancelInvoke(nameof(CountdownTime));
			Invoke(nameof(CountdownTime), 1f);
		}
	}

	private void CountdownTime()
	{
		UpdateContent();
	}

	private void OnDestroy()
	{
		CancelInvoke(nameof(CountdownTime));
	}
}