using Cysharp.Threading.Tasks;
using Snui;
using UnityEngine;

public sealed class HudContainer : MonoBehaviour
{
	public const string PATH = "Common/Prefab/HudContainer.prefab";
	public readonly string pathHudContainer = PATH;
	public static HudContainer Current;
	public CoinHud CoinHud { get; private set; }
	public Snui_HeartHud HeartHud { get; private set; }
	private bool _claimPresentation;
	private bool _heartWasActive;
	private Vector2 _coinAnchorMin, _coinAnchorMax, _coinPosition;

	public void BeginCoinClaim(Canvas overlay)
	{
		if (CoinHud == null) return;
		RectTransform rect = CoinHud.transform as RectTransform;
		if (!_claimPresentation)
		{
			_claimPresentation = true;
			_heartWasActive = HeartHud != null && HeartHud.gameObject.activeSelf;
			if (rect != null) { _coinAnchorMin = rect.anchorMin; _coinAnchorMax = rect.anchorMax; _coinPosition = rect.anchoredPosition; }
		}
		if (HeartHud != null) HeartHud.gameObject.SetActive(false);
		if (rect != null) { rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.anchoredPosition = new Vector2(150f, -50f); }
		int order = 1100;
		if (overlay != null)
		{
			foreach (Canvas layer in overlay.rootCanvas.GetComponentsInChildren<Canvas>(true))
				if (layer.isActiveAndEnabled && !layer.transform.IsChildOf(transform)) order = Mathf.Max(order, layer.sortingOrder + 1);
		}
		CoinHud.SetTop(order);
	}

	private void RestoreClaimPresentation()
	{
		if (!_claimPresentation) return;
		_claimPresentation = false;
		RectTransform rect = CoinHud == null ? null : CoinHud.transform as RectTransform;
		if (rect != null) { rect.anchorMin = _coinAnchorMin; rect.anchorMax = _coinAnchorMax; rect.anchoredPosition = _coinPosition; }
		if (HeartHud != null) HeartHud.gameObject.SetActive(_heartWasActive);
	}

	public static UniTask<HudContainer> Show(Transform parent)
	{
		if (Current == null && parent != null) Current = parent.GetComponentInChildren<HudContainer>(true);
		if (Current == null)
		{
			GameObject prefab = PrefabLibrary.Current == null ? null : PrefabLibrary.Current.Resolve(PATH);
			if (prefab != null)
			{
				GameObject instance = Instantiate(prefab, parent, false);
				Current = instance.GetComponent<HudContainer>();
			}
		}
		if (Current != null)
		{
			Current.gameObject.SetActive(true);
			Current.RestoreVisibility();
			Snui_Text.EnsureFontAssets(Current.transform);
		}
		return UniTask.FromResult(Current);
	}

	public static void SetCurrentTop(int order = 1100)
	{
		if (Current == null) return;
		Current.RestoreVisibility();
		Current.CoinHud?.SetTop(order);
		if (Current.HeartHud != null && Current.HeartHud.gameObject.activeSelf)
		{
			Current.HeartHud.SetTop(order);
		}
	}

	private void RestoreVisibility()
	{
		gameObject.SetActive(true);
		CanvasGroup group = GetComponent<CanvasGroup>();
		if (group != null) group.alpha = 1f;

		if (CoinHud != null)
		{
			CoinHud.gameObject.SetActive(true);
			CanvasGroup coinGroup = CoinHud.GetComponent<CanvasGroup>();
			if (coinGroup != null) coinGroup.alpha = 1f;
		}

		if (HeartHud != null && HeartHud.gameObject.activeSelf)
		{
			CanvasGroup heartGroup = HeartHud.GetComponent<CanvasGroup>();
			if (heartGroup != null) heartGroup.alpha = 1f;
		}
	}

	public static void RevertCurrentToOriginLayer()
	{
		if (Current == null) return;
		Current.CoinHud?.RevertToOriginLayer();
		Current.RestoreClaimPresentation();
		if (Current.HeartHud != null && Current.HeartHud.gameObject.activeSelf)
		{
			Current.HeartHud.RevertToOriginLayer();
		}
	}

	private static void ShowHud(bool show) { if (Current != null) Current.gameObject.SetActive(show); }

	private void Awake()
	{
		if (Current != null && Current != this && Current.gameObject.scene == gameObject.scene)
		{
			gameObject.SetActive(false);
			Destroy(gameObject);
			return;
		}
		Current = this;
		CoinHud = GetComponentInChildren<CoinHud>(true);
		HeartHud = GetComponentInChildren<Snui_HeartHud>(true);
	}

	private void OnDisable()
	{
		RestoreClaimPresentation();
		CoinHud?.RevertToOriginLayer();
		if (HeartHud != null && HeartHud.gameObject.activeSelf)
		{
			HeartHud.RevertToOriginLayer();
		}
	}
	private void OnDestroy() { if (Current == this) Current = null; }

	public void DisableClick()
	{
		CoinHud?.DisableClick();
		foreach (UnityEngine.UI.Button button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
		{
			if (CoinHud != null && button.transform.IsChildOf(CoinHud.transform)) continue;
			button.interactable = false;
		}
	}
}
