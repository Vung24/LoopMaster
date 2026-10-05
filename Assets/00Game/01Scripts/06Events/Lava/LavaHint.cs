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
				_canvasGroup.DOFade(1f, 0.2f);
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
				_canvasGroup.DOFade(0f, 0.2f).OnComplete(() =>
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
			transform.localPosition = localPos;
			if (flipY)
			{
				FlipY(transform);
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
				if (arguments != null && arguments.Length > 0)
				{
					_text.text = string.Format(key, arguments);
				}
				else
				{
					_text.text = key;
				}
			}
		}
	}
}