using DG.Tweening;
using Sugar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Snui.Profile;

namespace Snui
{
	public class RaceCell : TableViewCell
	{
		public const float HEIGHT = 112f;
		public Sprite SelfBgSprite;
		public Sprite OtherBgSprite;
		public Texture Rank1Texture;
		public Texture Rank2Texture;
		public Texture Rank3Texture;
		public Texture SelfRankTexture;
		public Texture OtherRankTexture;
		protected CanvasGroup _canvasGroup;
		protected Image _bg;
		protected RawImage _rank;
		protected TMP_Text _rankText;
		protected PlayerInfoWidget _playerInfo;
		private const float HORIZON_PADDING = 14f;
		private const float MAX_WIDTH = 878f;

		public CanvasGroup CanvasGroup => _canvasGroup;

		protected override void Awake()
		{
			base.Awake();
			if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
			if (_bg == null)
			{
				var bgTrans = HierarchyReferenceCache.Child(transform, "Bg");
				if (bgTrans != null) _bg = bgTrans.GetComponent<Image>();
				if (_bg == null) _bg = GetComponent<Image>();
			}
			if (_rank == null)
			{
				var rankTrans = HierarchyReferenceCache.Child(transform, "Rank");
				if (rankTrans != null) _rank = rankTrans.GetComponent<RawImage>();
			}
			if (_rankText == null)
			{
				var rtTrans = HierarchyReferenceCache.Child(transform, "RankText");
				if (rtTrans != null) _rankText = rtTrans.GetComponent<TMP_Text>();
				if (_rankText == null) _rankText = GetComponentInChildren<TMP_Text>(true);
			}
			if (_playerInfo == null)
			{
				_playerInfo = GetComponentInChildren<PlayerInfoWidget>(true);
			}
		}

		public virtual void Init(EventRaceOpponent data)
		{
			if (data == null) return;
			if (_bg != null)
			{
				_bg.sprite = data.IsSelf ? (SelfBgSprite ?? _bg.sprite) : (OtherBgSprite ?? _bg.sprite);
			}
			var tex = GetRankTexture(data.Rank, data.IsSelf);
			if (_rank != null)
			{
				_rank.gameObject.SetActive(tex != null);
				if (tex != null) _rank.texture = tex;
			}
			if (_rankText != null)
			{
				_rankText.gameObject.SetActive(tex == null);
				if (tex == null) _rankText.text = data.Rank.ToString();
			}
			if (_playerInfo != null)
			{
				_playerInfo.Init(data);
			}
		}

		public virtual Tween PlayEnterAnimation(float delay)
		{
			if (_canvasGroup == null) return null;
			_canvasGroup.alpha = 0f;
			transform.localScale = Vector3.one * 0.8f;
			var seq = DOTween.Sequence();
			seq.AppendInterval(delay);
			seq.Append(_canvasGroup.DOFade(1f, 0.25f));
			seq.Join(transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
			seq.SetLink(gameObject);
			return seq;
		}

		public virtual void CalculateSize(RectTransform parent)
		{
			var rt = GetComponent<RectTransform>();
			if (rt != null && parent != null)
			{
				float width = Mathf.Min(parent.rect.width - HORIZON_PADDING * 2f, MAX_WIDTH);
				rt.sizeDelta = new Vector2(width, HEIGHT);
			}
		}

		protected Texture GetRankTexture(int rank, bool isSelf)
		{
			if (rank == 1 && Rank1Texture != null) return Rank1Texture;
			if (rank == 2 && Rank2Texture != null) return Rank2Texture;
			if (rank == 3 && Rank3Texture != null) return Rank3Texture;
			if (isSelf && SelfRankTexture != null) return SelfRankTexture;
			return null;
		}
	}
}