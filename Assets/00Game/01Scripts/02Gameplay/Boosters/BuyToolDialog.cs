using Cysharp.Threading.Tasks;
using System;
using Snui;
using UnityEngine;

public class BuyToolDialog : MonoBehaviour
{
	public BuyToolDialogContent Content { get; private set; }
	public event Action<int> Closed;
	public static async UniTask<BuyToolDialog> Open(string key, Transform parent, Func<bool> isValid)
	{
		if (parent == null || (isValid != null && !isValid())) return null;
		GameObject root = await PopupAssets.Instantiate("BuyToolDialog", parent, parent.GetCancellationTokenOnDestroy());
		if (isValid != null && !isValid()) { Destroy(root); return null; }
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
