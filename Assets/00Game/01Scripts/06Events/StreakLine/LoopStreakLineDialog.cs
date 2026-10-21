using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;

namespace Snui.StreakLine
{
	public class LoopStreakLineDialog : StreakLineDialog
	{
		[SerializeField] private float _blockHeight = 232.5f;
		[SerializeField] private AudioClip _balloonUpAudio;
		[SerializeField] private AudioClip _balloonCompleteAudio;
		[SerializeField] private GameObject _bgBlocksPrefab;
		[SerializeField] private GameObject _bgBlockTopPrefab;
		[SerializeField] private AnimationCurve _balloonRiseCurve;
		[SerializeField] private List<RectTransform> _scrollableElements = new List<RectTransform>();

		private Transform _topNode;
		private Timer _timer;
		private AudioSource _audioSource;
		private RectTransform _dog;
		private readonly List<RectTransform> _backgrounds = new List<RectTransform>();
		private SkeletonGraphic _dogSpine;
		protected override AnimationCurve RiseCurve => _balloonRiseCurve;

		protected override void Awake()
		{
			base.Awake();

			_topNode = HierarchyReferenceCache.Descendant(transform, "TopNode");
			if (_topNode != null)
			{
				_timer = HierarchyReferenceCache.PathComponent<Timer>(_topNode, "Tips", "Timer");
			}

			_dog = HierarchyReferenceCache.DescendantComponent<RectTransform>(transform, "Dog");
			if (_dog != null)
			{
				_audioSource = _dog.GetComponent<AudioSource>();
				GameAudioSettings.RegisterEffectsSource(_audioSource);
				_dogSpine = _dog.GetComponent<SkeletonGraphic>() ?? _dog.GetComponentInChildren<SkeletonGraphic>(true);
				_dog.gameObject.SetActive(true);
			}

			var canvas = GetComponentInParent<Canvas>();
			float notchHeight = ScreenHelper.GetCanvasNotchHeight(canvas);
			if (_topNode != null && notchHeight > 0)
			{
				var topRect = _topNode.GetComponent<RectTransform>();
				if (topRect != null)
				{
					topRect.sizeDelta = new Vector2(topRect.sizeDelta.x, topRect.sizeDelta.y + notchHeight);
				}
				if (ScrollView != null)
				{
					var scrollRect = (RectTransform)ScrollView.transform;
					scrollRect.offsetMax -= new Vector2(0, notchHeight);
				}
			}
			CreateBgBlocks();
			LayoutBg();
		}

		private void CreateBgBlocks()
		{
			if (ScrollView == null || ScrollView.BackgroundContent == null) return;
			var node = HierarchyReferenceCache.Child(ScrollView.BackgroundContent, "BlocksNode");
			if (node == null || _bgBlocksPrefab == null || _bgBlockTopPrefab == null) return;
			var top = Instantiate(_bgBlockTopPrefab, node, false).GetComponent<RectTransform>();
			top.anchorMin = top.anchorMax = new Vector2(.5f, 1);
			top.anchoredPosition = new Vector2(-412, -270);
			int count = Mathf.CeilToInt(ScrollView.BackgroundContent.rect.height / Mathf.Max(1, _blockHeight * 4));
			for (int i = 0; i < count; i++)
			{
				var block = Instantiate(_bgBlocksPrefab, node, false).GetComponent<RectTransform>();
				block.anchorMin = block.anchorMax = new Vector2(.5f, 1);
				block.anchoredPosition = new Vector2(-412, -400 - _blockHeight * 4 * (i + 1) + 130);
			}
			var grass = HierarchyReferenceCache.Child(node, "Grass");
			if (grass != null) grass.SetAsLastSibling();
		}

		private void LayoutBg()
		{
			if (ScrollView == null || ScrollView.Parameters.Viewport == null) return;
			string[] names = { "Bg_Near", "Bg_Mid1", "Bg_Mid2", "Bg_Far" };
			float[] scales = { 1, .96f, .92f, .7f };
			for (int i = 0; i < names.Length; i++)
			{
				var bg = HierarchyReferenceCache.ChildComponent<RectTransform>(ScrollView.Parameters.Viewport, names[i]);
				if (bg == null) continue;
				bg.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ScrollView.BackgroundContent.rect.height * scales[i]);
				_backgrounds.Add(bg);
			}
			ScrollView.ScrollPositionChanged += UpdateBackgrounds;
		}

		private void UpdateBackgrounds(double normalized)
		{
			if (ScrollView == null || ScrollView.Parameters.Viewport == null) return;
			foreach (var background in _backgrounds)
			{
				var position = background.anchoredPosition;
				position.y = Mathf.Max(0, background.rect.height - ScrollView.Parameters.Viewport.rect.height) * (1 - (float)normalized);
				background.anchoredPosition = position;
			}
		}

		protected override async UniTask PlayBalloonEnter()
		{
			if (_dog == null) return;
			_dog.DOKill();
			var sequence = DOTween.Sequence().SetLink(gameObject);
			_dog.localPosition += Vector3.right * 328;
			sequence.Append(DOVirtual.Float(0, -60, .67f, ScrollView.SetBalloonOffset).SetEase(Ease.Linear));
			sequence.Append(DOVirtual.Float(-60, 90, .5f, ScrollView.SetBalloonOffset).SetEase(Ease.OutQuad));
			sequence.Append(DOVirtual.Float(90, 0, .67f, ScrollView.SetBalloonOffset).SetEase(Ease.OutQuad));
			sequence.Insert(0, _dog.DOBlendableLocalMoveBy(Vector3.left * 328, 1.2f).SetEase(Ease.OutQuad));
			await sequence.ToUniTask();
		}

		protected override void OnEntryStarted(bool firstEntry) => PlayFadeDogSound(firstEntry ? 3.5f : .5f);

		private void PlayFadeDogSound(float duration)
		{
			if (_audioSource == null) return;
			_audioSource.DOKill();
			GameAudioSettings.ApplyEffectsPreference();
			_audioSource.volume = 0;
			_audioSource.Play();
			_audioSource.DOFade(1, duration).SetLink(gameObject);
		}

		protected override void OnBalloonRiseStarted()
		{
			if (_dogSpine != null) { _dogSpine.Initialize(false); _dogSpine.AnimationState.SetAnimation(0, "start_up", false); }
			AudioManager.Current?.PlayOneShot(_balloonUpAudio, gameObject);
		}

		protected override async UniTask PlayBalloonRiseCompleted()
		{
			if (_dog == null) return;
			if (_dogSpine != null) _dogSpine.AnimationState.SetAnimation(0, "end_up", false);
			await DOTween.Sequence().SetLink(gameObject)
				.Append(DOVirtual.Float(ScrollView.BalloonOffset, ScrollView.RiseDip, .4f, ScrollView.SetBalloonOffset).SetEase(Ease.InOutSine))
				.Append(DOVirtual.Float(ScrollView.RiseDip, 0, .4f, ScrollView.SetBalloonOffset).SetEase(Ease.InOutSine)).ToUniTask();
			if (this == null) return;
			if (_dogSpine != null) _dogSpine.AnimationState.SetAnimation(0, "idle", true);
			AudioManager.Current?.PlayOneShot(_balloonCompleteAudio, gameObject);
		}

		protected override void OnRewardDialogVisibilityChanged(bool visible)
		{
			if (_audioSource == null) return;
			_audioSource.DOKill();
			if (visible) _audioSource.Stop();
			else PlayFadeDogSound(.5f);
		}

		protected override void Start()
		{
			base.Start();
			if (_timer != null && Managers.StreakLine != null)
			{
				CountdownHelper.CountdownUntil(_timer, Managers.StreakLine.EndTime);
			}
			if (ScrollView != null) UpdateBackgrounds(ScrollView.GetVirtualAbstractNormalizedScrollPosition());
		}

		protected override void OnDestroy()
		{
			if (ScrollView != null) ScrollView.ScrollPositionChanged -= UpdateBackgrounds;
			if (_dog != null) _dog.DOKill();
			if (_audioSource != null) _audioSource.DOKill();
			GameAudioSettings.UnregisterEffectsSource(_audioSource);
			base.OnDestroy();
		}
	}
}
