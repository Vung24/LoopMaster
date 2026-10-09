using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Lava
{
	public class LavaHint : MonoBehaviour
	{
		[SerializeField] private CanvasGroup _canvasGroup;
		[SerializeField] private Image _arrow;
		[SerializeField] private TMP_Text _text;
		private bool _enableTouch;

		private void Awake()
		{
			if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
			if (_arrow == null)
			{
				Transform arr = HierarchyReferenceCache.Child(transform, "Arrow");
				if (arr != null) _arrow = arr.GetComponent<Image>();
			}
			if (_text == null) _text = GetComponentInChildren<TMP_Text>();
		}

		private void Start()
		{
			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = 0f;
				_canvasGroup.DOFade(1f, 0.2f).SetLink(gameObject);
			}
			Invoke(nameof(Dismiss), 3f);
			_enableTouch = true;
		}

		private void Update()
		{
			if (_enableTouch && Input.GetMouseButtonDown(0))
			{
				Dismiss();
			}
		}

		private void Dismiss()
		{
			CancelInvoke(nameof(Dismiss));
			_enableTouch = false;
			if (_canvasGroup != null)
			{
				_canvasGroup.DOFade(0f, 0.2f).SetLink(gameObject).OnComplete(() =>
				{
					if (gameObject != null) Destroy(gameObject);
				});
			}
			else
			{
				Destroy(gameObject);
			}
		}

		public void SetLocalPosition(Vector3 localPos, bool flipY)
		{
			if (_arrow != null)
			{
				Vector3 arrowPos = _arrow.transform.localPosition;
				arrowPos.x = localPos.x;
				_arrow.transform.localPosition = arrowPos;
			}
			transform.localPosition = new Vector3(0f, localPos.y + (flipY ? -200f : 180f), localPos.z);
			if (flipY)
			{
				FlipY(transform);
				if (_text != null) FlipY(_text.transform);
			}
		}

		private void FlipY(Transform tf)
		{
			Vector3 scale = tf.localScale;
			scale.y = -Mathf.Abs(scale.y);
			tf.localScale = scale;
		}

		public void SetStringKey(string key, object[] arguments)
		{
			if (_text != null)
			{
				string format = key == "Lava-TutorialDesc1" ? "{0} players are still in the challenge."
					: key == "Lava-TutorialDesc2" ? "Win a level to jump to the next platform. Losing ends your round."
					: key == "Lava-TutorialDesc3" ? "Reach the final platform to share the prize with the surviving players."
					: key;
				_text.text = arguments != null && arguments.Length > 0 ? string.Format(format, arguments) : format;
			}
		}
	}
}
