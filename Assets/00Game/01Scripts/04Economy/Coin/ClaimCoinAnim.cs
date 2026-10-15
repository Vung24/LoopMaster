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
			int totalCoins = Mathf.Clamp(count, 1, 20);
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
					// Keep the face sprite still throughout the scatter and flight.
					fly.SetSprites(sprites);
				}

				Sequence flight = CreateCoinClaimTween(coin, localStart, localTarget, i, totalCoins);
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
				_coinHud.PlaySparks();
				_coinHud.PlayShakeAnimation();
			}
			if (AudioManager.Current != null)
			{
				if (CollectSound != null) AudioManager.Current.PlayOneShot(CollectSound, gameObject);
				else AudioManager.Current.PlayOneShot("coin_collect", gameObject);
			}
			_onAnimationCompleted?.Invoke();
		}

		private Sequence CreateCoinClaimTween(GameObject coin, Vector3 origin, Vector3 target, int index, int count)
		{
			Transform node = coin.transform;
			Vector2 spawnOffset = UnityEngine.Random.insideUnitCircle * 20f;
			Vector3 start = origin + new Vector3(spawnOffset.x, spawnOffset.y, 0f);
			Vector3 scale = node.localScale * UnityEngine.Random.Range(0.65f, 0.95f);
			node.localPosition = start;
			node.localScale = scale * 0.35f;

			// Uneven directions and impulses avoid an evenly spaced expanding ring.
			float angle = (index + UnityEngine.Random.Range(-0.45f, 0.45f)) / count * Mathf.PI * 2f;
			float speed = _coinScatterRadius * _coinScatterForce / Mathf.Max(0.01f, _coinScatterDuration);
			speed *= UnityEngine.Random.Range(0.65f, 1.25f);
			Vector3 velocity = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + speed * 0.22f, 0f);
			Vector3 gravity = Vector3.down * _coinScatterGravity;
			float drag = Mathf.Max(0.01f, _coinScatterDrag);
			float scatterTime = Mathf.Max(0.01f, _coinScatterDuration) * UnityEngine.Random.Range(0.85f, 1.15f)
				+ _coinScatterHoldDuration + UnityEngine.Random.Range(0f, 0.12f);
			Vector3 launch = CoinScatterPosition(start, velocity, gravity, drag, scatterTime);
			Vector3 launchVelocity = velocity * Mathf.Exp(-drag * scatterTime)
				+ gravity * ((1f - Mathf.Exp(-drag * scatterTime)) / drag);
			float flightTime = Mathf.Max(0.01f, _coinFlightDuration) * UnityEngine.Random.Range(0.9f, 1.15f);
			float pauseTime = Mathf.Max(0f, _coinRewardPauseDuration);
			Vector3 flightStart = launch + launchVelocity * (pauseTime / 3f);

			// Let coins settle briefly before accelerating into the HUD.
			Vector3 control1 = pauseTime > 0f ? flightStart : launch + launchVelocity * (flightTime / 3f);
			Vector3 toTarget = target - flightStart;
			Vector3 side = new Vector3(-toTarget.y, toTarget.x, 0f).normalized;
			Vector3 control2 = Vector3.Lerp(flightStart, target, 0.3f) + side * UnityEngine.Random.Range(-45f, 45f);

			Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(coin);
			sequence.Append(DOVirtual.Float(0f, scatterTime, scatterTime, time =>
			{
				if (node != null) node.localPosition = CoinScatterPosition(start, velocity, gravity, drag, time);
			}).SetEase(Ease.Linear));
			sequence.Insert(0f, node.DOScale(scale, 0.12f).SetEase(Ease.OutCubic));
			if (pauseTime > 0f)
				sequence.Append(node.DOLocalMove(flightStart, pauseTime).SetEase(Ease.OutCubic));
			sequence.Append(DOVirtual.Float(0f, 1f, flightTime, progress =>
			{
				if (node == null) return;
				float remaining = 1f - progress;
				node.localPosition = remaining * remaining * remaining * flightStart
					+ 3f * remaining * remaining * progress * control1
					+ 3f * remaining * progress * progress * control2
					+ progress * progress * progress * target;
			}).SetEase(Ease.Linear));
			sequence.Join(node.DOScale(scale * 0.55f, flightTime).SetEase(Ease.InCubic));
			sequence.OnComplete(() =>
			{
				if (coin != null) Destroy(coin);
			});
			return sequence;
		}

		private static Vector3 CoinScatterPosition(Vector3 start, Vector3 velocity, Vector3 gravity, float drag, float time)
		{
			// Analytic drag/gravity keeps the impulse consistent across frame rates.
			float travel = (1f - Mathf.Exp(-drag * time)) / drag;
			return start + velocity * travel + gravity * ((time - travel) / drag);
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
