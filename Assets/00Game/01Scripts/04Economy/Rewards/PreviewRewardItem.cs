using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class PreviewRewardItem : MonoBehaviour
	{
		public const string PATH = "Snui/Common/Prefabs/PreviewRewardItem.prefab";
		public RewardItem RewardItem;
		[SerializeField] private Image _icon;
		[SerializeField] private TMP_Text _count;
		[SerializeField] private CanvasGroup _canvasGroup;

		private void Awake()
		{
			if (_icon == null) _icon = HierarchyReferenceCache.ChildComponent<Image>(transform, "Icon");
			if (_count == null) _count = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Count");
			if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
			if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
		}

		public static async UniTask<PreviewRewardItem> Show(RewardItem item, Transform parent)
		{
			if (parent == null || item == null) return null;
			var prefab = PrefabLibrary.Current?.Resolve(PATH);
			if (prefab == null) return null;
			var go = Instantiate(prefab, parent, false);
			var preview = go.GetComponent<PreviewRewardItem>() ?? go.AddComponent<PreviewRewardItem>();
			preview.Init(item);
			return preview;
		}

		public void Init(RewardItem item)
		{
			RewardItem = item;
			UpdateIcon();
			UpdateTmp();
		}

		private void UpdateIcon()
		{
			if (_icon == null || RewardItem?.Type == null) return;
			_icon.sprite = PrefabLibrary.Current?.ResolveRewardIcon(RewardItem.Type.GetIconPath());
			_icon.enabled = _icon.sprite != null;
		}

		private void UpdateTmp()
		{
			if (_count == null || RewardItem?.Type == null) return;
			if (RewardItem.Type.IsTimeLimited())
			{
				_count.text = TimeFormatter.FormatRewardDuration(RewardItem.Count);
			}
			else if (RewardItem.Type == RewardType.Coin)
			{
				_count.text = RewardItem.Count.ToString("N0");
			}
			else
			{
				_count.text = "x" + RewardItem.Count;
			}
		}

		public Tweener FadeIn(float duration = 0.2f)
		{
			if (_canvasGroup == null) return null;
			_canvasGroup.alpha = 0f;
			return _canvasGroup.DOFade(1f, duration).SetUpdate(true);
		}

		public Tweener FadeOut(float duration = 0.2f)
		{
			if (_canvasGroup == null) return null;
			return _canvasGroup.DOFade(0f, duration).SetUpdate(true);
		}

		public void SetAlpha(float a)
		{
			if (_canvasGroup != null) _canvasGroup.alpha = a;
		}
	}
}
