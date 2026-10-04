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

	public static UniTask<HudContainer> Show(Transform parent)
	{
		if (Current == null && parent != null) Current = parent.GetComponentInChildren<HudContainer>(true);
		if (Current == null)
		{
			GameObject prefab = DataPrefabLibrary.Current == null ? null : DataPrefabLibrary.Current.Resolve(PATH);
			if (prefab != null)
			{
				GameObject instance = Instantiate(prefab, parent, false);
				Current = instance.GetComponent<HudContainer>();
			}
		}
		if (Current != null)
		{
			if (parent != null && !Current.transform.IsChildOf(parent))
			{
				Current.transform.SetParent(parent, false);
				// Scene instances can override counter anchors for the Home header.
				// Restore the prefab layout when that same HUD moves to another canvas.
				GameObject prefab = DataPrefabLibrary.Current == null ? null : DataPrefabLibrary.Current.HudContainerPrefab;
				if (prefab != null)
				{
					CopyLayout(Current.transform, prefab.transform);
					CopyLayout(Current.CoinHud == null ? null : Current.CoinHud.transform, prefab.GetComponentInChildren<CoinHud>(true)?.transform);
					CopyLayout(Current.HeartHud == null ? null : Current.HeartHud.transform, prefab.GetComponentInChildren<Snui_HeartHud>(true)?.transform);
				}
			}
			Current.gameObject.SetActive(true);
			Current.RestoreVisibility();
			Snui_Text.EnsureFontAssets(Current.transform);
		}
		return UniTask.FromResult(Current);
	}

	private static void CopyLayout(Transform target, Transform source)
	{
		RectTransform rect = target as RectTransform, authored = source as RectTransform;
		if (rect == null || authored == null) return;
		rect.anchorMin = authored.anchorMin; rect.anchorMax = authored.anchorMax;
		rect.pivot = authored.pivot; rect.sizeDelta = authored.sizeDelta;
		rect.anchoredPosition3D = authored.anchoredPosition3D;
		rect.localScale = authored.localScale; rect.localRotation = authored.localRotation;
	}

	public static void TrySetTop(int order = 1100)
	{
		Current?.RestoreVisibility();
		Current?.CoinHud?.SetTop(order);
		Current?.HeartHud?.SetTop(order);
	}
	private void RestoreVisibility()
	{
		foreach (Transform node in new[] { transform, CoinHud == null ? null : CoinHud.transform, HeartHud == null ? null : HeartHud.transform })
		{
			if (node == null) continue;
			// Reused scene counters may have been hidden individually.
			node.gameObject.SetActive(true);
			CanvasGroup group = node.GetComponent<CanvasGroup>();
			if (group != null) group.alpha = 1f;
		}
	}

	public static void TryRevertToOriginLayer()
	{
		Current?.CoinHud?.RevertToOriginLayer();
		Current?.HeartHud?.RevertToOriginLayer();
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

	private void OnDisable() { CoinHud?.RevertToOriginLayer(); HeartHud?.RevertToOriginLayer(); }
	private void OnDestroy() { if (Current == this) Current = null; }

	public void DisableClick()
	{
		CoinHud?.DisableClick();
		foreach (UnityEngine.UI.Button button in GetComponentsInChildren<UnityEngine.UI.Button>(true)) button.interactable = false;
	}
}
