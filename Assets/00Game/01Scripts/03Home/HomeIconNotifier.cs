using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomeIconNotifier : MonoBehaviour
{
	public enum Style
	{
		Count = 0,
		Exclamation = 1
	}

	[SerializeField] private Image _image;
	[SerializeField] private TMP_Text _text;
	private Style _style;

	private void Awake()
	{
		if (_image == null)
		{
			_image = GetComponent<Image>();
		}
		if (_text == null)
		{
			_text = GetComponentInChildren<TMP_Text>(true);
		}
	}

	public static async UniTask<HomeIconNotifier> Create(Transform parent)
	{
		GameObject prefab = Resources.Load<GameObject>("Events/Common/HomeIconNotifier");
		if (prefab == null)
		{
			prefab = Resources.Load<GameObject>("UI/Common/HomeIconNotifier");
		}
		if (prefab != null)
		{
			GameObject go = Instantiate(prefab, parent);
			return go.GetComponent<HomeIconNotifier>();
		}
		await UniTask.CompletedTask;
		return null;
	}

	public void SetCount(int count)
	{
		SetStyle(Style.Count);
		gameObject.SetActive(count > 0);
		if (count > 0 && _text != null)
		{
			_text.text = count > 99 ? "99+" : count.ToString();
		}
	}

	public void ShowExclamationMark(bool show)
	{
		SetStyle(Style.Exclamation);
		gameObject.SetActive(show);
		if (show && _text != null)
		{
			_text.text = "!";
		}
	}

	private void SetStyle(Style style)
	{
		_style = style;
	}
}