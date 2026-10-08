using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public enum RewardDetailToastStyle { Light, Dark }
	public class RewardDetailToast : MonoBehaviour
	{
		public Sprite LightBg, LightArrow, DarkBg, DarkArrow;
		private CanvasGroup _canvasGroup;
		private RectTransform _bg, _rewardNode;
		private Image _arrow;
		private List<RewardItem> _items;
		private Vector2 _offset;
		private RewardDetailToastStyle _style;
		private float _topPadding;
		private Tween _animation;
		private bool _visible, _closing;

		public static UniTask<RewardDetailToast> Show(List<RewardItem> rewards, Transform parent, float x = 0f, float y = 0f)
			=> Show(rewards, parent, new Vector2(x, y));

		public static UniTask<RewardDetailToast> Show(List<RewardItem> rewards, Transform parent, Vector2 offset, RewardDetailToastStyle style = RewardDetailToastStyle.Light)
		{
			if (parent == null || rewards == null || rewards.Count == 0)
			{
				return UniTask.FromResult<RewardDetailToast>(null);
			}

			var prefab = PrefabLibrary.Current?.RewardDetailToastPrefab;
			if (prefab == null) return UniTask.FromResult<RewardDetailToast>(null);
			var toast = Instantiate(prefab, parent, false).GetComponent<RewardDetailToast>();
			toast._items = new List<RewardItem>(rewards);
			toast._offset = offset;
			toast._style = style;
			return UniTask.FromResult(toast);
		}

		public static UniTask<RewardDetailToast> Show(List<RewardItem> rewards, Transform parent, Vector3 position)
		{
			var local = parent != null ? parent.InverseTransformPoint(position) : position;
			return Show(rewards, parent, local.x, local.y);
		}

		public void SetTopPadding(float padding) => _topPadding = padding;

		private void Awake()
		{
			_canvasGroup = GetComponent<CanvasGroup>();
			_bg = HierarchyReferenceCache.ChildComponent<RectTransform>(transform, "Bg");
			_arrow = HierarchyReferenceCache.ChildComponent<Image>(transform, "Arrow");
			_rewardNode = HierarchyReferenceCache.ChildComponent<RectTransform>(transform, "RewardNode");
			_canvasGroup.alpha = 0;
			_canvasGroup.interactable = false;
			_canvasGroup.blocksRaycasts = false;
		}

		private void Start() => StartAsync().Forget();

		private async UniTaskVoid StartAsync()
		{
			if (_items == null || _items.Count == 0) { Destroy(gameObject); return; }
			transform.localPosition = _offset;
			_bg.GetComponent<Image>().sprite = _style == RewardDetailToastStyle.Light ? LightBg : DarkBg;
			_arrow.sprite = _style == RewardDetailToastStyle.Light ? LightArrow : DarkArrow;
			var layout = RewardLayoutHelper.GetRewardsLayoutPos(_items.Count, RewardLayoutType.RewardDetailToast);
			var size = RewardLayoutHelper.GetLayoutSize(_items.Count, RewardLayoutType.RewardDetailToast) + Vector2.one * 50;
			_bg.sizeDelta = _rewardNode.sizeDelta = size;
			_bg.localPosition = _rewardNode.localPosition = new Vector3(0, size.y * .5f, 0);
			FitToCanvas(size);
			for (int i = 0; i < _items.Count; i++)
			{
				var cell = await RewardCell.Show(_items[i], _rewardNode, false, false, true);
				if (this == null || cell == null) return;
				cell.transform.localPosition = layout.Item2[i];
				cell.transform.localScale = Vector3.one * layout.Item1;
			}
			Snui_Text.EnsureFontAssets(transform);
			_animation = DOTween.Sequence().SetLink(gameObject)
				.Append(_canvasGroup.DOFade(1, .2f)).AppendCallback(() => _visible = true)
				.AppendInterval(2.8f).AppendCallback(() => _visible = false)
				.Append(_canvasGroup.DOFade(0, .2f)).OnComplete(() => Destroy(gameObject));
		}

		private void FitToCanvas(Vector2 size)
		{
			var canvas = GetComponentInParent<Canvas>();
			if (canvas == null || !(canvas.transform is RectTransform rect)) return;
			var position = rect.InverseTransformPoint(_rewardNode.position);
			float shift = Mathf.Clamp(position.x, rect.rect.xMin + size.x * .5f, rect.rect.xMax - size.x * .5f) - position.x;
			_bg.localPosition += Vector3.right * shift;
			_rewardNode.localPosition += Vector3.right * shift;
			if (position.y + size.y * .5f > rect.rect.yMax - _topPadding - ScreenHelper.GetCanvasNotchHeight(canvas))
			{
				transform.localScale = new Vector3(1, -1, 1);
				_rewardNode.localScale = new Vector3(1, -1, 1);
				transform.localPosition = new Vector3(_offset.x, -_offset.y, 0);
			}
		}

		private void Update()
		{
			if (_visible && (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))) FadeOut();
		}

		public void FadeOut()
		{
			if (_closing) return;
			_closing = true;
			_visible = false;
			_animation?.Kill();
			_animation = _canvasGroup.DOFade(0, .2f).SetLink(gameObject).OnComplete(() => Destroy(gameObject));
		}

		private void OnDestroy() => _animation?.Kill();
		private void OnDisable() { _animation?.Kill(); Destroy(gameObject); }
	}
}
