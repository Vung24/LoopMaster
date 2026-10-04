using System;
using Snui;
using UnityEngine;

public class BuyToolDialog : MonoBehaviour
{
	public BuyToolDialogContent Content { get; private set; }
	public event Action<int> Closed;
	public static BuyToolDialog Open(string key, Transform parent, Func<bool> isValid)
	{
		var library = RecoveryPrefabLibrary.Current;
		if (library == null || library.BuyToolDialogPrefab == null || parent == null) return null;
		GameObject root = Instantiate(library.BuyToolDialogPrefab, parent, false);
		root.SetActive(true);
		BuyToolDialog dialog = root.GetComponent<BuyToolDialog>();
		if (dialog == null) dialog = root.AddComponent<BuyToolDialog>();
		dialog.Content = root.GetComponentInChildren<BuyToolDialogContent>(true);
		if (dialog.Content == null) { Destroy(root); return null; }
		CanvasGroup group = root.GetComponent<CanvasGroup>();
		if (group != null) { group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true; }
		dialog.Content.Init(key, isValid);
		dialog.Content.Dismissed += dialog.OnDismissed;
		dialog.Content.Show();
		return dialog;
	}
	private void OnDismissed(int result)
	{
		var callback = Closed;
		Closed = null;
		gameObject.SetActive(false);
		Destroy(gameObject);
		callback?.Invoke(result);
	}
	public void Cancel()
	{
		Closed = null;
		if (Content != null) Content.Hide();
		gameObject.SetActive(false);
		Destroy(gameObject);
	}
	private void OnDestroy()
	{
		if (Content != null) Content.Dismissed -= OnDismissed;
		Closed = null;
	}
}
