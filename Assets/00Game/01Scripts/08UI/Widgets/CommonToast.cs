using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	[RequireComponent(typeof(CanvasGroup))]
	public class CommonToast : MonoBehaviour
	{
		[SerializeField] private TMP_Text _text;
		[SerializeField] private RectTransform _arrow;
		[SerializeField] private CanvasGroup _canvasGroup;

		private CancellationTokenSource _cts;

		public static CommonToast Instance { get; private set; }

		private void Awake()
		{
			Instance = this;
			if (_canvasGroup == null)
			{
				_canvasGroup = GetComponent<CanvasGroup>();
			}
			if (_text == null)
			{
				_text = GetComponentInChildren<TMP_Text>(true);
			}
			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = 0f;
			}
			gameObject.SetActive(false);
		}

		public void Show(string message, float duration = 2f)
		{
			ShowAsync(message, duration).Forget();
		}

		public async UniTask ShowAsync(string message, float duration = 2f)
		{
			_cts?.Cancel();
			_cts?.Dispose();
			_cts = new CancellationTokenSource();
			var token = _cts.Token;

			if (_text != null)
			{
				_text.text = message;
			}
			gameObject.SetActive(true);

			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = 0f;
				transform.localScale = Vector3.one * 0.8f;
				transform.DOScale(1f, 0.2f).SetLink(gameObject);
				_canvasGroup.DOFade(1f, 0.2f).SetLink(gameObject);
				await UniTask.WaitForSeconds(0.2f, cancellationToken: token);

				await UniTask.WaitForSeconds(duration, cancellationToken: token);

				transform.DOScale(0.8f, 0.2f).SetLink(gameObject);
				_canvasGroup.DOFade(0f, 0.2f).SetLink(gameObject);
				await UniTask.WaitForSeconds(0.2f, cancellationToken: token);
			}

			gameObject.SetActive(false);
		}

		private void OnDestroy()
		{
			_cts?.Cancel();
			_cts?.Dispose();
			if (Instance == this)
			{
				Instance = null;
			}
		}
	}
}