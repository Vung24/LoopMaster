using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Snui
{
	[RequireComponent(typeof(CanvasGroup))]
	public class AddNumberToast : MonoBehaviour
	{
		private Sequence _animation;

		public static AddNumberToast Show(Transform parent, string text, Vector2 offset, float duration = 1f, float distance = 48f)
		{
			GameObject prefab = PrefabLibrary.Current != null ? PrefabLibrary.Current.AddNumberToastPrefab : null;
			if (parent == null || prefab == null) return null;
			GameObject instance = Instantiate(prefab, parent, false);
			AddNumberToast toast = instance.GetComponent<AddNumberToast>();
			if (toast == null) { Destroy(instance); return null; }
			instance.transform.localPosition = offset;
			instance.SetActive(true);
			Snui_Text.EnsureFontAssets(instance.transform);
			TMP_Text label = instance.GetComponentInChildren<TMP_Text>(true);
			if (label != null) label.SetText(text);
			foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
			CanvasGroup group = instance.GetComponent<CanvasGroup>();
			group.blocksRaycasts = false;
			group.interactable = false;
			group.alpha = 0f;
			duration = Mathf.Max(0.48f, duration);
			toast._animation = DOTween.Sequence().SetUpdate(true).SetLink(instance);
			toast._animation.Append(group.DOFade(1f, 0.24f));
			toast._animation.AppendInterval(duration - 0.48f);
			toast._animation.Append(group.DOFade(0f, 0.24f));
			toast._animation.Insert(0f, instance.transform.DOBlendableLocalMoveBy(Vector3.up * distance, duration));
			toast._animation.OnComplete(() => Destroy(instance));
			return toast;
		}

		private void OnDisable()
		{
			_animation?.Kill();
			_animation = null;
			Destroy(gameObject);
		}
	}
}
