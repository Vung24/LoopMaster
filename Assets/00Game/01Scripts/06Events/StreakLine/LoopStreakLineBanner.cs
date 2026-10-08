using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;

namespace Snui.StreakLine
{
	public class LoopStreakLineBanner : StreakLineBanner
	{
		[SerializeField] private float _maxWidth = 600f;
		[SerializeField] private float _minWidth = 310f;
		[SerializeField] private float _maxHeight = 0f;
		[SerializeField] private float _minHeight = 0f;
		[SerializeField] public AudioClip ProgressAudioClip;

		private CanvasGroup _canvasGroup;
		private GameObject _startProgressGo;
		private GameObject _endProgressGo;
		private RawImage _startNodeRawImage;
		private Image _midNodeImage;
		private RawImage _endNodeRawImage;
		private GameObject _startStarGo;
		private TMP_Text _startText;
		private TMP_Text _midText;
		private TMP_Text _endText;
		private TMP_Text _cursorText;
		private RectTransform _cursor;
		private GameObject _highlight;
		private ProgressBar _midProgress1;
		private ProgressBar _midProgress2;
		private SkeletonGraphic _balloonSkeleton;

		protected override void Awake()
		{
			base.Awake();
			_canvasGroup = GetComponent<CanvasGroup>();
			_titleText = HierarchyReferenceCache.PathComponent<TMP_Text>(transform, "Frame", "Top", "Title");

			var startProgNode = HierarchyReferenceCache.Descendant(transform, "StartProgress");
			_startProgressGo = startProgNode != null ? startProgNode.gameObject : null;

			var endProgNode = HierarchyReferenceCache.Descendant(transform, "EndProgress");
			_endProgressGo = endProgNode != null ? endProgNode.gameObject : null;

			_startNodeRawImage = HierarchyReferenceCache.DescendantComponent<RawImage>(transform, "StartNode");
			_midNodeImage = HierarchyReferenceCache.DescendantComponent<Image>(transform, "MidNode");
			_endNodeRawImage = HierarchyReferenceCache.DescendantComponent<RawImage>(transform, "EndNode");

			var starNode = HierarchyReferenceCache.Path(transform, "StartNode", "Star");
			_startStarGo = starNode != null ? starNode.gameObject : null;

			_startText = HierarchyReferenceCache.PathComponent<TMP_Text>(transform, "StartNode", "Text");
			_midText = HierarchyReferenceCache.PathComponent<TMP_Text>(transform, "MidNode", "Text");
			_endText = HierarchyReferenceCache.PathComponent<TMP_Text>(transform, "EndNode", "Text");
			_cursorText = HierarchyReferenceCache.PathComponent<TMP_Text>(transform, "Cursor", "Text");

			_midProgress1 = HierarchyReferenceCache.DescendantComponent<ProgressBar>(transform, "MidProgress1");
			_midProgress2 = HierarchyReferenceCache.DescendantComponent<ProgressBar>(transform, "MidProgress2");
			if (_midProgress1 != null) _midProgress1.SetFillBounds(_midProgress1.GetComponent<RectTransform>());
			if (_midProgress2 != null) _midProgress2.SetFillBounds(_midProgress2.GetComponent<RectTransform>());

			_balloon = HierarchyReferenceCache.DescendantComponent<RectTransform>(transform, "Cursor");
			_cursor = HierarchyReferenceCache.DescendantComponent<RectTransform>(transform, "Cursor");

			var highlightNode = HierarchyReferenceCache.Descendant(transform, "Highlight");
			_highlight = highlightNode != null ? highlightNode.gameObject : null;

			if (_balloon != null)
			{
				_balloonSkeleton = _balloon.GetComponentInChildren<SkeletonGraphic>(true);
			}
		}

		private void Start()
		{
			RelayoutTitle();
			var locEvent = _titleText != null ? _titleText.GetComponent<LocalizeStringEvent>() : null;
			if (locEvent != null)
			{
				locEvent.OnUpdateString.AddListener(_ => RelayoutTitle());
			}

			if (Managers.StreakLine != null)
			{
				UpdateLevelDisplay(_currentLevel);
			}
		}

		public void RelayoutTitle()
		{
			if (_titleText == null) return;
			var parent = _titleText.transform.parent as RectTransform;
			if (parent != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
			}
		}

		public override void PlayProgressAudio()
		{
			if (ProgressAudioClip != null && AudioManager.Current != null) AudioManager.Current.PlayOneShot(ProgressAudioClip);
			else base.PlayProgressAudio();
		}

		protected override void UpdateLevelDisplay(int level)
		{
			base.UpdateLevelDisplay(level);
			if (_cursorText != null)
			{
				_cursorText.text = level.ToString();
			}

			var range = GetActiveRange(level);
			float middle = (range.start + range.end) * .5f;
			if (_startText != null) _startText.text = range.start.ToString();
			if (_midText != null) _midText.text = middle.ToString("0.#");
			if (_endText != null) _endText.text = range.end.ToString();
		}

		protected override void UpdateProgress(float level)
		{
			base.UpdateProgress(level);
			var range = GetActiveRange(level);
			float middle = (range.start + range.end) * .5f;
			if (_midProgress1 != null) _midProgress1.SetProgress(Mathf.InverseLerp(range.start, middle, level));
			if (_midProgress2 != null) _midProgress2.SetProgress(Mathf.InverseLerp(middle, range.end, level));
		}

		protected override Vector2 GetLevelLocalPosition(float level)
		{
			if (_startNode == null || _midNodeImage == null || _endNode == null || _balloon == null)
				return base.GetLevelLocalPosition(level);
			var range = GetActiveRange(level);
			float middle = (range.start + range.end) * .5f;
			Vector3 point = level <= middle
				? Vector3.Lerp(_startNode.position, _midNodeImage.transform.position, Mathf.InverseLerp(range.start, middle, level))
				: Vector3.Lerp(_midNodeImage.transform.position, _endNode.position, Mathf.InverseLerp(middle, range.end, level));
			return _balloon.parent.InverseTransformPoint(point);
		}
	}
}
