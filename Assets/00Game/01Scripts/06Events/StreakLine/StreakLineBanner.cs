using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.StreakLine
{
	public class StreakLineBanner : MonoBehaviour
	{
		[SerializeField]
		protected RectTransform _balloon;
		[SerializeField]
		protected TMP_Text _levelText;
		[SerializeField]
		protected TMP_Text _titleText;
		[SerializeField]
		protected RectTransform[] _levelNodes;
		[SerializeField]
		protected AudioClip _progressAudio;
		[SerializeField] protected RectTransform _startNode;
		[SerializeField] protected RectTransform _endNode;
		private ProgressBar _progressBar;
		private TMP_Text _rangeStartText, _rangeEndText;
		private (int start, int end)? _animationRange;

		protected int _currentLevel;

		public static bool ShouldShow()
		{
			return Managers.StreakLine != null && Managers.StreakLine.IsOngoing && Managers.StreakLine.GetTempViewLevel() > 0;
		}

		public static async UniTask<StreakLineBanner> Show(Transform parent, int? fromLevel = null)
		{
			var settings = StreakLineSettings.Instance;
			if (settings != null && settings.BannerPrefab != null)
			{
				var go = Instantiate(settings.BannerPrefab, parent, false);
				var banner = go.GetComponent<StreakLineBanner>();
				if (banner != null)
				{
					RewardDialog.PrepareOverlay(go, 1340);
					try { await banner.PlayAnimation(fromLevel); }
					catch { Destroy(go); throw; }
				}
				else Destroy(go);
				return banner;
			}
			return null;
		}

		protected virtual void Awake()
		{
			if (_balloon == null) _balloon = HierarchyReferenceCache.DescendantComponent<RectTransform>(transform, "Cursor");
			if (_levelText == null && _balloon != null) _levelText = _balloon.GetComponentInChildren<TMP_Text>(true);
			if (_startNode == null) _startNode = HierarchyReferenceCache.DescendantComponent<RectTransform>(transform, "StartNode");
			if (_endNode == null) _endNode = HierarchyReferenceCache.DescendantComponent<RectTransform>(transform, "EndNode");
			if (_titleText == null) _titleText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Title");
			_progressBar = HierarchyReferenceCache.DescendantComponent<ProgressBar>(transform, "MidProgress");
			if (_progressBar != null) _progressBar.SetFillBounds(_progressBar.GetComponent<RectTransform>());
			if (_startNode != null) _rangeStartText = _startNode.GetComponentInChildren<TMP_Text>(true);
			if (_endNode != null) _rangeEndText = _endNode.GetComponentInChildren<TMP_Text>(true);
			if (_titleText != null && string.IsNullOrEmpty(_titleText.text) && Managers.StreakLine != null)
			{
				_titleText.text = Managers.StreakLine.GetTitleI18n();
			}
		}

		public static async UniTask ShowWinProgress(Transform parent)
		{
			var manager = Managers.StreakLine;
			if (manager == null || !manager.IsOngoing || manager.IsExpired || manager.GetTempLevel() <= 0) return;
			int fromLevel = manager.GetCurrLevel();
			manager.SynchronizeTempLevel();
			StreakLineBanner banner = null;
			try
			{
				banner = await Show(parent, fromLevel);
				if (banner != null)
					await UniTask.Delay(600, ignoreTimeScale: true, cancellationToken: banner.GetCancellationTokenOnDestroy());
			}
			finally { if (banner != null) Destroy(banner.gameObject); }
		}

		public async UniTask PlayAnimation(int? fromLevel = null)
		{
			if (Managers.StreakLine == null) return;
			_currentLevel = fromLevel ?? Managers.StreakLine.GetPrevViewLevel();
			_animationRange = GetDisplayRange(_currentLevel);
			UpdateLevelDisplay(_currentLevel);
			await PlayEnterAnimation();
			int targetLevel = Managers.StreakLine.GetCurrLevel();
			await IncreaseToLevel(targetLevel);
		}

		public async UniTask PlayEnterAnimation()
		{
			transform.localScale = Vector3.zero;
			await transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetLink(gameObject).ToUniTask();
		}

		public virtual async UniTask IncreaseToLevel(int targetLevel)
		{
			if (Managers.StreakLine == null) return;
			targetLevel = Mathf.Clamp(targetLevel, 0, Managers.StreakLine.GetFinalGoal());
			while (_currentLevel < targetLevel)
			{
				int nextLevel = _currentLevel + 1;
				_animationRange = GetDisplayRange(nextLevel);
				UpdateLevelDisplay(_currentLevel);
				PlayProgressAudio();
				await DOBalloonMove(nextLevel);
				_currentLevel = nextLevel;
				DOIncreaseLevelText(_currentLevel);
			}
		}

		public virtual async UniTask DOBalloonMove(int level)
		{
			await DOVirtual.Float(_currentLevel, level, .35f, UpdateProgress)
				.SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject).ToUniTask()
				.AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
		}

		public virtual void DOIncreaseLevelText(int level)
		{
			UpdateLevelDisplay(level);
			if (_levelText != null)
			{
				_levelText.transform.DOComplete();
				_levelText.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f).SetLink(gameObject);
			}
		}

		protected virtual void UpdateLevelDisplay(int level)
		{
			if (_levelText != null)
			{
				_levelText.text = level.ToString();
			}
			var range = GetActiveRange(level);
			if (_rangeStartText != null) _rangeStartText.text = range.start.ToString();
			if (_rangeEndText != null) _rangeEndText.text = range.end.ToString();
			UpdateProgress(level);
		}

		protected (int start, int end) GetActiveRange(float level) => _animationRange ?? GetDisplayRange(Mathf.CeilToInt(level));

		protected virtual void UpdateProgress(float level)
		{
			var range = GetActiveRange(level);
			if (_progressBar != null) _progressBar.SetProgress(Mathf.InverseLerp(range.start, range.end, level));
			if (_balloon != null)
			{
				var position = _balloon.localPosition;
				var point = GetLevelLocalPosition(level);
				position.x = point.x;
				position.y = point.y;
				_balloon.localPosition = position;
			}
		}

		public Vector2 GetLevelLocalPosition(int level) => GetLevelLocalPosition((float)level);

		protected virtual Vector2 GetLevelLocalPosition(float level)
		{
			if (_startNode != null && _endNode != null && _balloon != null)
			{
				var range = GetActiveRange(level);
				var point = Vector3.Lerp(_startNode.position, _endNode.position, Mathf.InverseLerp(range.start, range.end, level));
				return _balloon.parent.InverseTransformPoint(point);
			}
			int index = Mathf.FloorToInt(level);
			if (_levelNodes != null && index >= 0 && index < _levelNodes.Length && _levelNodes[index] != null)
			{
				return _balloon != null ? (Vector2)_balloon.parent.InverseTransformPoint(_levelNodes[index].position) : _levelNodes[index].anchoredPosition;
			}
			return new Vector2(level * 40f, 0);
		}

		public Vector3 GetLevelPosition(int level)
		{
			if (_levelNodes != null && level >= 0 && level < _levelNodes.Length && _levelNodes[level] != null)
			{
				return _levelNodes[level].position;
			}
			return transform.position;
		}

		protected (int start, int end) GetDisplayRange(int level)
		{
			int previous = 0;
			int lastStart = 0;
			var chests = Managers.StreakLine?.GetChests();
			if (chests != null)
				foreach (var chest in chests)
				{
					if (chest == null || chest.Level <= previous) continue;
					if (level <= chest.Level) return (previous, chest.Level);
					lastStart = previous;
					previous = chest.Level;
				}
			return (lastStart, Mathf.Max(lastStart + 1, previous));
		}

		public float GetHeight()
		{
			var rect = GetComponent<RectTransform>();
			return rect != null ? rect.sizeDelta.y : 120f;
		}

		public virtual void PlayProgressAudio()
		{
			if (_progressAudio != null && AudioManager.Current != null)
			{
				AudioManager.Current.PlayOneShot(_progressAudio);
			}
		}

		private void OnDestroy()
		{
			transform.DOKill();
		}
	}
}
