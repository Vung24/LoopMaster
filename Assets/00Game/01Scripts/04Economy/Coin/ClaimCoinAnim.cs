using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Snui
{
	public class ClaimCoinAnim : MonoBehaviour
	{
		public AudioClip ShowSound;
		public AudioClip CollectSound;
		private readonly List<Sequence> _flights = new List<Sequence>();
		private GameObject _flightRoot;

		// Native CoinFly (0x14E0674) and CoinsFly.MoveNext (0x14E0E1C).
		// The caller owns this enumerator; coin balance has already been committed.
		public IEnumerator Play(int count, Transform parent, Vector3 startWorld, Snui_CoinHud hud, GameObject prefab = null, int sortingOrder = 1101)
		{
			Cancel();
			if (prefab == null && PrefabLibrary.Current != null) prefab = PrefabLibrary.Current.FlyCoinPrefab;
			if (parent == null || hud == null || prefab == null || count <= 0) yield break;
			_flightRoot = new GameObject("WinCoinFlights", typeof(RectTransform), typeof(Canvas));
			_flightRoot.transform.SetParent(parent, false);
			var canvas = _flightRoot.GetComponent<Canvas>();
			canvas.overrideSorting = true;
			canvas.sortingOrder = sortingOrder;
			Vector3 start = _flightRoot.transform.InverseTransformPoint(startWorld);
			Vector3 target = _flightRoot.transform.InverseTransformPoint(hud.GetIconPos());
			int visibleCount = Mathf.Min(8, count);
			if (AudioManager.Current != null) AudioManager.Current.PlayOneShot(ShowSound, gameObject);
			for (int i = 0; i < visibleCount; i++)
			{
				GameObject coin = Instantiate(prefab, _flightRoot.transform, false);
				coin.SetActive(true);
				foreach (var image in coin.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) image.raycastTarget = false;
				coin.GetComponent<FlyCoin>()?.Play(true);
				Transform node = coin.transform;
				node.localPosition = start;
				Vector3 scale = node.localScale;
				node.localScale = Vector3.zero;
				Sequence flight = DOTween.Sequence().SetUpdate(true);
				flight.AppendInterval(i * 0.07f);
				float began = i * 0.07f;
				flight.Append(node.DOScale(scale * 1.2f, 0.16f));
				flight.Append(node.DOScale(scale * 0.9f, 0.1f));
				flight.Append(node.DOScale(scale, 0.1f));
				flight.Insert(began, node.DOLocalMove(start + new Vector3(Random.Range(-100, 100), Random.Range(-60, 60)), 0.3f));
				flight.Append(node.DOLocalMove(target, 0.45f).SetEase(Ease.InQuad));
				flight.Join(node.DOScale(scale * 0.7f, 0.45f).SetEase(Ease.InCubic));
				flight.OnComplete(() => { if (coin != null) Destroy(coin); });
				_flights.Add(flight);
			}
			while (_flightRoot != null && _flights.Exists(f => f.IsActive() && !f.IsComplete())) yield return null;
			if (_flightRoot != null && hud != null)
			{
				hud.SetAutoUpdate(true);
				hud.PlaySparks();
				if (AudioManager.Current != null) AudioManager.Current.PlayOneShot(CollectSound, gameObject);
			}
			Cancel();
		}

		public void Cancel()
		{
			foreach (Sequence flight in _flights) flight.Kill();
			_flights.Clear();
			if (_flightRoot != null) Destroy(_flightRoot);
			_flightRoot = null;
		}
		private void OnDisable() { Cancel(); }
		private void OnDestroy() { Cancel(); }
	}
}
