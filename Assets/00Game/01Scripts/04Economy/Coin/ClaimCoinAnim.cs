using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Snui
{
	public class ClaimCoinAnim : MonoBehaviour
	{
		public AudioClip ShowSound;
		public AudioClip CollectSound;

		[SerializeField, Min(0f)] private float _coinScatterRadius = 180f;
		[SerializeField, Min(0f)] private float _coinScatterForce = 1.6f;
		[SerializeField, Min(0.01f)] private float _coinScatterDrag = 3.5f;
		[SerializeField, Min(0f)] private float _coinScatterGravity = 900f;
		[SerializeField, Min(0.01f)] private float _coinScatterDuration = 0.3f;
		[SerializeField, Min(0f)] private float _coinScatterHoldDuration = 0.08f;
		[SerializeField, Min(0f)] private float _coinRewardPauseDuration = 0.25f;
		[SerializeField, Min(0.01f)] private float _coinFlightDuration = 0.45f;

		private Transform _parent;
		private UnityAction _onAnimationCompleted;
		private Snui_CoinHud _coinHud;
		private readonly List<Sequence> _flights = new List<Sequence>();
		private GameObject _flightRoot;
		private bool _isFlying;

		public static async UniTask Play(int count, Vector2 startPos, UnityAction callback)
		{
			if (Snui_CoinHud.Current == null || count < 1)
			{
				callback?.Invoke();
				return;
			}

			Canvas canvas = Snui_CoinHud.Current.GetComponentInParent<Canvas>();
			Transform parent = canvas != null ? canvas.rootCanvas.transform : Snui_CoinHud.Current.transform.parent;

			GameObject host = new GameObject("ClaimCoinAnimHost", typeof(RectTransform), typeof(Canvas));
			host.transform.SetParent(parent, false);
			host.transform.localPosition = Vector3.zero;
			host.transform.localScale = Vector3.one;
			Canvas hostCanvas = host.GetComponent<Canvas>();
			hostCanvas.overrideSorting = true;
			hostCanvas.sortingLayerID = canvas != null ? canvas.sortingLayerID : 0;
			hostCanvas.sortingOrder = canvas != null ? canvas.sortingOrder + 1 : 1101;
			var anim = host.AddComponent<ClaimCoinAnim>();
			anim.SetCoinHud(Snui_CoinHud.Current);

			Vector3 targetPos = Snui_CoinHud.Current != null
				? Snui_CoinHud.Current.GetIconPos()
				: new Vector3(Screen.width * 0.15f, Screen.height * 0.92f, 0f);
			anim.CoinsFly(count, host.transform, new Vector3(startPos.x, startPos.y, targetPos.z), targetPos, () =>
			{
				callback?.Invoke();
				if (host != null) Destroy(host);
			});
			await UniTask.CompletedTask;
		}

		public void SetCoinHud(Snui_CoinHud coinHud)
		{
			_coinHud = coinHud;
		}

		public void CoinsFly(int count, Transform parent, Vector3 startPos, Vector3 targetPos, UnityAction callback)
		{
			_parent = parent;
			_onAnimationCompleted = callback;
			CoinsFlyAsync(count, parent, startPos, targetPos).Forget();
		}

		private async UniTaskVoid CoinsFlyAsync(int count, Transform parent, Vector3 startPos, Vector3 targetPos)
		{
			CancellationToken token = this.GetCancellationTokenOnDestroy();
			int totalCoins = Mathf.Clamp(count, 1, 8);
			GameObject coinPrefab = PrefabLibrary.Current != null ? PrefabLibrary.Current.FlyCoinPrefab : null;
			if (coinPrefab == null)
			{
				_onAnimationCompleted?.Invoke();
				return;
			}

			Sprite[] sprites = await FlyCoin.LoadSprites(coinPrefab);
			if (token.IsCancellationRequested || this == null || !isActiveAndEnabled || parent == null) return;

			if (_coinHud != null)
			{
				_coinHud.SetAutoUpdate(false);
			}

			if (AudioManager.Current != null)
			{
				if (ShowSound != null) AudioManager.Current.PlayOneShot(ShowSound, gameObject);
				else AudioManager.Current.PlayOneShot("coin_show", gameObject);
			}

			Vector3 localStart = parent.InverseTransformPoint(startPos);
			Vector3 localTarget = parent.InverseTransformPoint(targetPos);

			_isFlying = true;
			List<UniTask> flightTasks = new List<UniTask>();
			for (int i = 0; i < totalCoins; i++)
			{
				if (parent == null) break;
				GameObject coin = Instantiate(coinPrefab, parent, false);
				coin.SetActive(true);
				foreach (var img in coin.GetComponentsInChildren<Graphic>(true)) img.raycastTarget = false;

				FlyCoin fly = coin.GetComponent<FlyCoin>();
				if (fly != null && sprites != null)
				{
					// Native fly coins animate their sprite frames during travel.
					fly.SetSprites(sprites);
					fly.Play(true);
				}

				Sequence flight = CreateCoinClaimTween(coin, localStart, localTarget, i);
				_flights.Add(flight);
				flightTasks.Add(flight.ToUniTask().AttachExternalCancellation(token));
			}

			if (flightTasks.Count > 0)
			{
				try
				{
					await UniTask.WhenAll(flightTasks);
				}
				catch (OperationCanceledException)
				{
					return;
				}
			}

			if (token.IsCancellationRequested || this == null || !isActiveAndEnabled) return;

			_isFlying = false;
			if (_coinHud != null)
			{
				_coinHud.SetAutoUpdate(true);
			}
			_onAnimationCompleted?.Invoke();
		}

		private void PlayCoinCollectFeedback()
		{
			if (_coinHud != null)
			{
				_coinHud.PlaySparks();
				_coinHud.PlayShakeAnimation();
			}
			if (AudioManager.Current != null)
			{
				// The temporary animation host can be destroyed before the clip ends.
				if (CollectSound != null) AudioManager.Current.PlayOneShot(CollectSound);
				else AudioManager.Current.PlayOneShot("coin_collect");
			}
		}

		private Sequence CreateCoinClaimTween(GameObject coin, Vector3 origin, Vector3 target, int index)
		{
			Transform node = coin.transform;
			Vector3 scale = node.localScale;
			Vector3 scatter = origin + new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-60, 60), 0f);
			node.localPosition = origin;
			node.localScale = Vector3.zero;

			// Native CoinFly uses a scale bounce and short scatter before accelerating to the HUD.
			Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(coin);
			sequence.AppendInterval(index * 0.07f);
			sequence.Append(node.DOScale(scale * 1.2f, 0.16f));
			sequence.Append(node.DOScale(scale * 0.9f, 0.1f));
			sequence.Append(node.DOScale(scale, 0.1f));
			sequence.Insert(index * 0.07f, node.DOLocalMove(scatter, _coinScatterDuration));
			sequence.Append(node.DOLocalMove(target, _coinFlightDuration).SetEase(Ease.InCubic));
			sequence.Join(node.DOScale(scale * 0.7f, _coinFlightDuration).SetEase(Ease.InQuart));
			if (index == 0) sequence.AppendCallback(PlayCoinCollectFeedback);
			sequence.OnComplete(() => Destroy(coin));
			return sequence;
		}

		public IEnumerator Play(int count, Transform parent, Vector3 startWorld, Snui_CoinHud hud, GameObject prefab = null, int sortingOrder = 1101)
		{
			Cancel();
			if (count < 1 || parent == null) yield break;
			// The caller's canvas can sit below the popup. Give the entire flight
			// its own layer, rather than spawning coins behind the reward receipt.
			_flightRoot = new GameObject("RewardCoinFlightRoot", typeof(RectTransform), typeof(Canvas));
			_flightRoot.transform.SetParent(parent, false);
			_flightRoot.transform.localPosition = Vector3.zero;
			_flightRoot.transform.localScale = Vector3.one;
			Canvas flightCanvas = _flightRoot.GetComponent<Canvas>();
			Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
			Canvas hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
			flightCanvas.overrideSorting = true;
			flightCanvas.sortingLayerID = hudCanvas != null ? hudCanvas.sortingLayerID : (parentCanvas != null ? parentCanvas.sortingLayerID : 0);
			flightCanvas.sortingOrder = sortingOrder > 0 ? sortingOrder : (hudCanvas != null ? hudCanvas.sortingOrder + 1 : 1101);
			_coinHud = hud;
			bool done = false;
			Vector3 targetPos = hud != null ? hud.GetIconPos() : startWorld;
			CoinsFly(count, _flightRoot.transform, startWorld, targetPos, () => done = true);
			while (!done) yield return null;
			Cancel();
		}

		public void Cancel()
		{
			foreach (Sequence flight in _flights) flight.Kill();
			_flights.Clear();
			if (_flightRoot != null) Destroy(_flightRoot);
			_flightRoot = null;
			_isFlying = false;
		}

		private void OnDisable()
		{
			if (_isFlying && _coinHud != null) _coinHud.SetAutoUpdate(true);
			Cancel();
		}

		private void OnDestroy()
		{
			if (_isFlying && _coinHud != null) _coinHud.SetAutoUpdate(true);
			Cancel();
		}
	}
}
