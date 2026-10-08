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
		[SerializeField] private RectTransform _startNode;
		[SerializeField] private RectTransform _endNode;

		private int _currentLevel;

		public static bool ShouldShow()
		{
			return Managers.StreakLine != null && Managers.StreakLine.IsOngoing && Managers.StreakLine.GetTempViewLevel() > 0;
		}

		public static async UniTask<StreakLineBanner> Show(Transform parent)
		{
			var settings = StreakLineSettings.Instance;
			if (settings != null && settings.BannerPrefab != null)
			{
				var go = Instantiate(settings.BannerPrefab, parent, false);
				var banner = go.GetComponent<StreakLineBanner>();
				if (banner != null)
				{
					await banner.PlayAnimation();
				}
				return banner;
			}
			return null;
		}

		private void Awake()
		{
			if (_titleText != null && Managers.StreakLine != null)
			{
				_titleText.text = Managers.StreakLine.GetTitleI18n();
			}
		}

		private void Start()
		{
			if (Managers.StreakLine != null)
			{
				_currentLevel = Managers.StreakLine.GetPrevViewLevel();
				UpdateLevelDisplay(_currentLevel);
			}
		}

		public async UniTask PlayAnimation()
		{
			if (Managers.StreakLine == null) return;
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
			while (_currentLevel < targetLevel)
			{
				_currentLevel++;
				PlayProgressAudio();
				await DOBalloonMove(_currentLevel);
				DOIncreaseLevelText(_currentLevel);
			}
		}

		public virtual async UniTask DOBalloonMove(int level)
		{
			if (_balloon != null)
			{
				Vector2 targetPos = GetLevelLocalPosition(level);
				await _balloon.DOAnchorPos(targetPos, 0.35f).SetEase(Ease.OutQuad).SetLink(gameObject).ToUniTask();
			}
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
			if (_balloon != null)
			{
				_balloon.anchoredPosition = GetLevelLocalPosition(level);
			}
		}

		public Vector2 GetLevelLocalPosition(int level)
		{
			if (_startNode != null && _endNode != null && _balloon != null)
			{
				var range = GetDisplayRange(level);
				var point = Vector3.Lerp(_startNode.position, _endNode.position, Mathf.InverseLerp(range.start, range.end, level));
				return _balloon.parent.InverseTransformPoint(point);
			}
			if (_levelNodes != null && level >= 0 && level < _levelNodes.Length && _levelNodes[level] != null)
			{
				return _levelNodes[level].anchoredPosition;
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
			var chests = Managers.StreakLine?.GetChests();
			if (chests != null)
				foreach (var chest in chests)
				{
					if (level <= chest.Level) return (previous, chest.Level);
					previous = chest.Level;
				}
			return (0, Mathf.Max(1, previous));
		}

		public float GetHeight()
		{
			var rect = GetComponent<RectTransform>();
			return rect != null ? rect.sizeDelta.y : 120f;
		}

		public void PlayProgressAudio()
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
