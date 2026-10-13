using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Snui
{
	public class ClaimCoinAnim : MonoBehaviour
	{
		private const float SpawnInterval = 0.07f;
		private const float BurstDuration = 0.36f;
		private const float SpinHoldDuration = 0.2f;
		public AudioClip ShowSound;
		public AudioClip CollectSound;
		private Transform _parent;
		private UnityAction _onAnimationCompleted;
		private Snui_CoinHud _coinHud;
		private readonly List<Sequence> _flights = new List<Sequence>();
		private readonly List<Sequence> _pendingFlights = new List<Sequence>();
		private GameObject _flightRoot;

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
			Canvas hostCanvas = host.GetComponent<Canvas>();
			hostCanvas.overrideSorting = true;
			hostCanvas.sortingLayerID = canvas != null ? canvas.sortingLayerID : 0;
			hostCanvas.sortingOrder = canvas != null ? canvas.sortingOrder + 1 : 1101;
			var anim = host.AddComponent<ClaimCoinAnim>();
			anim.SetCoinHud(Snui_CoinHud.Current);

			Vector3 targetPos = Snui_CoinHud.Current != null
				? Snui_CoinHud.Current.GetIconPos()
				: new Vector2(Screen.width * 0.15f, Screen.height * 0.92f);
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
			int showCount = Mathf.Clamp(count, 1, 8);
			GameObject coinPrefab = PrefabLibrary.Current != null ? PrefabLibrary.Current.FlyCoinPrefab : null;
			if (coinPrefab == null)
			{
				_onAnimationCompleted?.Invoke();
				return;
			}

			Sprite[] sprites = await FlyCoin.LoadSprites(coinPrefab);

			if (AudioManager.Current != null)
			{
				if (ShowSound != null) AudioManager.Current.PlayOneShot(ShowSound, gameObject);
				else AudioManager.Current.PlayOneShot("coin_show", gameObject);
			}

			for (int i = 0; i < showCount; i++)
			{
				CoinFly(i + 1, coinPrefab, sprites, showCount, startPos, targetPos);
				if (i + 1 < showCount)
					await UniTask.Delay(TimeSpan.FromSeconds(SpawnInterval), ignoreTimeScale: true,
						cancellationToken: this.GetCancellationTokenOnDestroy());
			}
			// Wait for the last coin to finish appearing before releasing the whole batch.
			await UniTask.Delay(TimeSpan.FromSeconds(BurstDuration + SpinHoldDuration), ignoreTimeScale: true,
				cancellationToken: this.GetCancellationTokenOnDestroy());
			foreach (Sequence flight in _pendingFlights) flight.Play();
			_pendingFlights.Clear();
		}

		public void CoinFly(int index, GameObject prefab, Sprite[] sprites, int showCount, Vector3 startPos, Vector3 targetPos)
		{
			if (prefab == null || _parent == null) return;
			GameObject coin = Instantiate(prefab, _parent, false);
			coin.SetActive(true);
			foreach (var image in coin.GetComponentsInChildren<Graphic>(true)) image.raycastTarget = false;

			FlyCoin fly = coin.GetComponent<FlyCoin>();
			if (fly != null)
			{
				fly.SetSprites(sprites);
				fly.Play(true);
			}

			Transform node = coin.transform;
			node.position = startPos;
			Vector3 scale = node.localScale;
			node.localScale = Vector3.zero;

			Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(coin);
			seq.Append(node.DOScale(scale * 1.2f, 0.16f).SetEase(Ease.OutSine));
			seq.Append(node.DOScale(scale * 0.9f, 0.1f).SetEase(Ease.InSine));
			seq.Append(node.DOScale(scale, 0.1f).SetEase(Ease.OutSine));

			Vector3 burstPos = (Vector3)startPos + new Vector3(UnityEngine.Random.Range(-100f, 100f), UnityEngine.Random.Range(-60f, 60f), 0f);
			seq.Insert(0f, node.DOMove(burstPos, 0.3f).SetEase(Ease.OutQuad));
			_flights.Add(seq);

			Sequence flight = DOTween.Sequence().SetUpdate(true).SetLink(coin).Pause();
			flight.AppendInterval((index - 1) * 0.02f);
			flight.Append(node.DOMove(targetPos, 0.45f).SetEase(Ease.InQuad));
			flight.Join(node.DOScale(scale * 0.7f, 0.45f).SetEase(Ease.InCubic));
			flight.OnComplete(() => OnAnimDone(coin, index, showCount));
			_flights.Add(flight);
			_pendingFlights.Add(flight);
		}

		public void OnAnimDone(GameObject coin, int index, int showCount)
		{
			if (coin != null) Destroy(coin);
			if (index == showCount)
			{
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
		}

		public IEnumerator Play(int count, Transform parent, Vector3 startWorld, Snui_CoinHud hud, GameObject prefab = null, int sortingOrder = 1101)
		{
			Cancel();
			if (count < 1 || parent == null) yield break;
			// The caller's canvas can sit below the popup. Give the entire flight
			// its own layer, rather than spawning coins behind the reward receipt.
			_flightRoot = new GameObject("RewardCoinFlightRoot", typeof(RectTransform), typeof(Canvas));
			_flightRoot.transform.SetParent(parent, false);
			Canvas flightCanvas = _flightRoot.GetComponent<Canvas>();
			Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
			flightCanvas.overrideSorting = true;
			flightCanvas.sortingLayerID = parentCanvas != null ? parentCanvas.sortingLayerID : 0;
			flightCanvas.sortingOrder = sortingOrder;
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
			_pendingFlights.Clear();
			if (_flightRoot != null) Destroy(_flightRoot);
			_flightRoot = null;
		}

		private void OnDisable() => Cancel();
		private void OnDestroy() => Cancel();
	}
}
