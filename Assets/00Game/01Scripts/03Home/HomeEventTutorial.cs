using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Snui
{
	public class HomeEventTutorial : MonoBehaviour
	{
		[SerializeField]
		private CanvasGroup _mask;

		private Transform _originParent;
		private Finger _finger;
		private CancellationTokenSource _cts;

		public bool IsShowing => _mask != null && _mask.gameObject.activeSelf;

		public async UniTask ShowFinger(Transform target = null)
		{
			DismissFinger(target);

			_cts = new CancellationTokenSource();
			var ct = _cts.Token;

			_mask = CreateMask(target != null ? target : transform);
			if (_mask != null)
			{
				await _mask.DOFade(0.7f, 0.2f).SetLink(_mask.gameObject).ToUniTask().AttachExternalCancellation(ct);
			}

			Transform currentTarget = target != null ? target : transform;
			_originParent = currentTarget.parent;

			if (_mask != null)
			{
				currentTarget.SetParent(_mask.transform, true);
				_finger = await Finger.Show(_mask.transform);
				if (_finger != null)
				{
					_finger.transform.position = currentTarget.position;
				}
			}
		}

		public void DismissFinger(Transform target = null)
		{
			_cts?.Cancel();
			_cts?.Dispose();
			_cts = null;

			Transform currentTarget = target != null ? target : transform;
			if (_originParent != null && currentTarget != null)
			{
				currentTarget.SetParent(_originParent, true);
				_originParent = null;
			}

			if (_finger != null)
			{
				Destroy(_finger.gameObject);
				_finger = null;
			}

			if (_mask != null)
			{
				Destroy(_mask.gameObject);
				_mask = null;
			}
		}

		private static CanvasGroup CreateMask(Transform target)
		{
			Canvas canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
			if (canvas == null) return null;
			var mask = new GameObject("HomeTutorialMask", typeof(RectTransform), typeof(CanvasRenderer),
				typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
			mask.layer = canvas.gameObject.layer;
			mask.transform.SetParent(canvas.rootCanvas.transform, false);
			var rect = mask.GetComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.sizeDelta = Vector2.zero;
			mask.GetComponent<UnityEngine.UI.Image>().color = Color.black;
			var group = mask.GetComponent<CanvasGroup>();
			group.alpha = 0f;
			return group;
		}

		private void OnDestroy()
		{
			DismissFinger();
		}
	}
}
