using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.StreakLine
{
	public class StreakLineProgress : MonoBehaviour
	{
		[SerializeField]
		private RectTransform _cursor;
		[SerializeField]
		private TMP_Text _levelText;
		[SerializeField]
		private Button _claimButton;
		[SerializeField]
		private RectTransform _claimButtonNode;
		[SerializeField]
		private List<RectTransform> _levelNodes = new List<RectTransform>();
		[SerializeField]
		private List<Button> _chestButtons = new List<Button>();

		private BaseStreakLineManager _manager;
		private int _currentLevel;
		private float _displayLevel;
		[SerializeField] private int _index;
		[SerializeField] private RectTransform _head;
		[SerializeField] private RectTransform _next;
		[SerializeField] private ProgressBar _bar;
		[SerializeField] private Image _chestImage;
		[SerializeField] private GameObject _tick;
		[SerializeField] private Button _rewardButton;
		[SerializeField] private AudioClip _showButton, _clickButton, _showTick;
		private readonly List<Image> _lines = new List<Image>();
		[SerializeField] private Image _platformImage;
		[SerializeField] private Image _nextImage;
		[SerializeField] private TMP_Text _endText;
		private Tween _rise, _claimTween, _chestTween;
		private Vector3 _claimPosition, _chestPosition;
		private Transform _claimParent;
		[SerializeField] private Transform _claimMask;
		private int _claimSibling;
		private int _bindingVersion;
		public event Action<StreakLineProgress> Claimed;

		public int Index { get => _index; set => _index = value; }
		public StreakLineChest Chest => _manager != null && Index >= 0 && Index < _manager.GetChests().Count ? _manager.GetChests()[Index] : null;
		public int FromLevel => Index <= 0 || _manager == null || Index > _manager.GetChests().Count ? 0 : _manager.GetChests()[Index - 1].Level;

		public virtual float GetHeight()
		{
			var rect = GetComponent<RectTransform>();
			return rect != null ? rect.rect.height : 546f;
		}

		private void EnsureReferences()
		{
			if (_head == null) _head = HierarchyReferenceCache.ChildComponent<RectTransform>(transform, "Head");
			if (_next == null) _next = HierarchyReferenceCache.ChildComponent<RectTransform>(transform, "Next");
			if (_cursor == null) _cursor = HierarchyReferenceCache.ChildComponent<RectTransform>(transform, "Cursor");
			if (_levelText == null && _cursor != null) _levelText = _cursor.GetComponentInChildren<TMP_Text>(true);
			if (_bar == null) _bar = HierarchyReferenceCache.ChildComponent<ProgressBar>(transform, "Progress");
			if (_chestImage == null) _chestImage = HierarchyReferenceCache.DescendantComponent<Image>(transform, "Chest");
			if (_chestImage != null && _chestPosition == Vector3.zero) _chestPosition = _chestImage.transform.localPosition;
			if (_tick == null) _tick = HierarchyReferenceCache.Child(transform, "Tick")?.gameObject;
			if (_platformImage == null) _platformImage = HierarchyReferenceCache.DescendantComponent<Image>(transform, "Platform");
			if (_nextImage == null && _next != null) _nextImage = _next.GetComponent<Image>();
			if (_endText == null && _next != null) _endText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(_next, "Text");
			if (_claimButton == null) _claimButton = HierarchyReferenceCache.DescendantComponent<Button>(transform, "ClaimButton");
			if (_claimButtonNode == null && _claimButton != null) _claimButtonNode = _claimButton.GetComponent<RectTransform>();
			if (_claimButtonNode != null && _claimParent == null)
			{
				_claimPosition = _claimButtonNode.localPosition;
				_claimParent = _claimButtonNode.parent;
				_claimSibling = _claimButtonNode.GetSiblingIndex();
			}
			if (_claimMask == null) _claimMask = HierarchyReferenceCache.Child(transform, "Mask");
			if (_rewardButton == null && _chestImage != null) _rewardButton = _chestImage.GetComponent<Button>();
		}

		private void Awake()
		{
			EnsureReferences();
			var extraLabel = HierarchyReferenceCache.Child(transform, "GoalLabel");
			if (extraLabel != null) extraLabel.gameObject.SetActive(false);
			if (_rewardButton != null) _rewardButton.onClick.AddListener(() => ShowRewardDetail(Chest).Forget());
			if (_claimButton != null)
			{
				_claimButton.onClick.AddListener(OnClickClaimButton);
			}
		}

		public void Init(BaseStreakLineManager manager)
		{
			EnsureReferences();
			_manager = manager;
			if (_manager == null) return;

			if (Chest == null) return;
			_currentLevel = Mathf.Clamp(_manager.GetPrevViewLevel(), FromLevel, Chest.Level);
			_displayLevel = _currentLevel;
			if (_head != null) _head.gameObject.SetActive(Index == 0);
			if (_endText != null) _endText.text = Chest.Level.ToString();
			InitLines();
			UpdateCursor(_currentLevel);
			UpdateNode();

		}

		public void Bind(BaseStreakLineManager manager, int index, float displayLevel)
		{
			Release();
			Index = index;
			Init(manager);
			SetDisplayLevel(displayLevel, false);
		}

		public void OffsetContent(float offsetX)
		{
			for (int i = 0; i < transform.childCount; i++)
				transform.GetChild(i).localPosition += Vector3.right * offsetX;
			if (_claimButtonNode != null) _claimPosition = _claimButtonNode.localPosition;
			if (_chestImage != null) _chestPosition = _chestImage.transform.localPosition;
		}

		public void Release() => Release(true);

		private void Release(bool restoreHierarchy)
		{
			_bindingVersion++;
			_rise?.Kill();
			_claimTween?.Kill();
			_chestTween?.Kill();
			if (_cursor != null) _cursor.DOKill();
			if (_levelText != null)
			{
				_levelText.transform.DOKill();
				_levelText.transform.localScale = Vector3.one;
			}
			if (_chestImage != null)
			{
				_chestImage.transform.DOKill();
				_chestImage.transform.localScale = Vector3.one;
				_chestImage.transform.localPosition = _chestPosition;
			}
			if (_claimButtonNode != null)
			{
				if (restoreHierarchy && _claimParent != null)
				{
					if (_claimButtonNode.parent != _claimParent) _claimButtonNode.SetParent(_claimParent, true);
					if (_claimButtonNode.GetSiblingIndex() != _claimSibling) _claimButtonNode.SetSiblingIndex(_claimSibling);
				}
				_claimButtonNode.localPosition = _claimPosition;
			}
		}

		public void SetDisplayLevel(float level, bool animateClaim = true)
		{
			var chest = Chest;
			if (chest == null) return;
			level = Mathf.Clamp(level, FromLevel, chest.Level);
			_displayLevel = level;
			int previousLevel = _currentLevel;
			_currentLevel = Mathf.FloorToInt(level);
			UpdateNode();
			if (_cursor != null)
			{
				int lower = Mathf.FloorToInt(level), upper = Mathf.CeilToInt(level);
				var position = _cursor.localPosition;
				position.y = Mathf.Lerp(GetLevelLocalPosition(lower).y, GetLevelLocalPosition(upper).y, level - lower);
				_cursor.localPosition = position;
				_cursor.gameObject.SetActive(level > FromLevel && level < chest.Level);
			}
			if (_levelText != null) _levelText.text = _currentLevel.ToString();
			if (animateClaim && previousLevel < chest.Level && _currentLevel >= chest.Level && _manager.CanClaimChest(chest.Level))
				DOClaimButtonSlideIn().Forget();
		}

		private void InitLines()
		{
			var template = HierarchyReferenceCache.PathComponent<Image>(transform, "Progress", "Lines", "Line");
			if (template == null || _bar == null) return;
			if (_lines.Count == 0) _lines.Add(template);
			int count = Mathf.Max(0, Chest.Level - FromLevel - 1);
			int span = Mathf.Max(1, Chest.Level - FromLevel);
			while (_lines.Count < count) _lines.Add(Instantiate(template, template.transform.parent, false));
			float width = ((RectTransform)_bar.transform).rect.width;
			for (int i = 0; i < _lines.Count; i++)
			{
				_lines[i].gameObject.SetActive(i < count);
				_lines[i].rectTransform.anchoredPosition = new Vector2(width * (i + 1) / span - width * .5f, 0);
			}
		}

		public void UpdateNode()
		{
			if (_manager == null) return;
			var milestone = Chest;
			if (milestone != null)
			{
				bool claimed = _manager.IsChestClaimed(milestone.Level);
				if (_tick != null) _tick.SetActive(claimed);
				if (_claimButtonNode != null && (_claimTween == null || !_claimTween.IsActive())) _claimButtonNode.gameObject.SetActive(_currentLevel >= milestone.Level && _manager.CanClaimChest(milestone.Level));
				var cache = _manager.AssetsCache;
				if (_chestImage != null && cache != null)
					_chestImage.sprite = cache.GetChestSprite(Index, _manager.GetChests().Count, claimed);
				if (_platformImage != null && cache != null) _platformImage.sprite = cache.GetPlatformSprite(Index, _manager.GetChests().Count);
				if (_nextImage != null && cache != null) _nextImage.sprite = _currentLevel >= milestone.Level ? cache.ProgressNodeGreenSprite : cache.ProgressNodeBlueSprite;
				if (_bar != null)
				{
					_bar.SetProgress(_currentLevel - FromLevel, milestone.Level - FromLevel);
					_bar.SetPercent((_displayLevel - FromLevel) / Mathf.Max(1, milestone.Level - FromLevel));
				}
				if (cache != null) for (int i = 0; i < _lines.Count; i++) _lines[i].sprite = i < _currentLevel - FromLevel ? cache.ProgressLineGreenSprite : cache.ProgressLineBlueSprite;
			}
			var chests = _manager.GetChests();
			for (int i = 0; i < _chestButtons.Count && i < chests.Count; i++)
			{
				var chest = chests[i];
				var btn = _chestButtons[i];
				if (btn != null && chest != null)
				{
					bool claimed = _manager.IsChestClaimed(chest.Level);
					bool canClaim = _manager.CanClaimChest(chest.Level);
					btn.interactable = !claimed;
					int level = chest.Level;
					btn.onClick.RemoveAllListeners();
					btn.onClick.AddListener(() => ShowRewardDetail(chest).Forget());
				}
			}
		}

		public void UpdateCursor(int level)
		{
			if (_cursor != null)
			{
				var position = _cursor.localPosition;
				position.y = GetLevelLocalPosition(level).y;
				_cursor.localPosition = position;
				_cursor.gameObject.SetActive(Chest != null && level > FromLevel && level < Chest.Level);
			}
			if (_levelText != null)
			{
				_levelText.text = level.ToString();
			}
		}

		public Vector2 GetLevelLocalPosition(int level)
		{
			if (Chest != null && _head != null && _next != null)
			{
				return transform.InverseTransformPoint(GetLevelPosition(level));
			}
			if (_levelNodes != null && level >= 0 && level < _levelNodes.Count && _levelNodes[level] != null)
			{
				return _levelNodes[level].anchoredPosition;
			}
			return new Vector2(0, level * 60f);
		}

		public Vector3 GetLevelPosition(int level)
		{
			if (Chest != null && _head != null && _next != null)
			{
				if (level <= FromLevel) return _head.position;
				if (level >= Chest.Level) return _next.position;
				int line = level - FromLevel - 1;
				return line < _lines.Count ? _lines[line].transform.position : Vector3.Lerp(_head.position, _next.position, Mathf.InverseLerp(FromLevel, Chest.Level, level));
			}
			if (_levelNodes != null && level >= 0 && level < _levelNodes.Count && _levelNodes[level] != null)
			{
				return _levelNodes[level].position;
			}
			return transform.position + new Vector3(0, level * 0.5f, 0);
		}

		public async UniTask IncreaseToLevel(int targetLevel)
		{
			await IncreaseToLevel(targetLevel, .75f).ToUniTask();
		}

		public Tween IncreaseToLevel(int targetLevel, float duration)
		{
			_rise?.Kill();
			float start = _displayLevel;
			int end = Chest != null ? Mathf.Clamp(targetLevel, FromLevel, Chest.Level) : Mathf.FloorToInt(start);
			float fraction = 0;
			_rise = DOTween.To(() => fraction, value =>
			{
				fraction = value;
				SetDisplayLevel(Mathf.Lerp(start, end, value));
			}, 1, duration).SetEase(Ease.Linear).SetLink(gameObject).OnComplete(() =>
			{
				SetDisplayLevel(end);
			});
			return _rise;
		}

		public async UniTask DoCursorRise(int level)
		{
			if (_cursor != null)
			{
				await _cursor.DOLocalMoveY(GetLevelLocalPosition(level).y, .4f).SetEase(Ease.OutQuad).SetLink(gameObject).ToUniTask();
			}
		}

		public void DOIncreaseLevelText(int level)
		{
			if (_levelText != null)
			{
				_levelText.text = level.ToString();
				_levelText.transform.DOComplete();
				_levelText.transform.DOPunchScale(Vector3.one * 0.3f, 0.25f).SetLink(gameObject);
			}
		}

		public async UniTask PlayChestAnim(StreakLineChest chest)
		{
			int version = _bindingVersion;
			if (_tick != null)
			{
				_tick.SetActive(true);
				var graphic = _tick.GetComponent<SkeletonGraphic>();
				if (graphic != null) { graphic.Initialize(false); graphic.AnimationState.SetAnimation(0, "tick_done", false); }
			}
			AudioManager.Current?.PlayOneShot(_showTick, gameObject);
			if (_chestImage != null)
			{
				_chestTween?.Kill();
				var chestTransform = _chestImage.transform;
				chestTransform.localScale = Vector3.one;
				chestTransform.localPosition = _chestPosition;
				_chestTween = DOTween.Sequence().SetLink(gameObject)
					.Append(chestTransform.DOScale(new Vector3(.9f, .72f, .9f), .1f))
					.Join(chestTransform.DOBlendableLocalMoveBy(Vector3.down * 18, .1f))
					.Append(chestTransform.DOScale(Vector3.one * 1.1f, .13f))
					.Join(chestTransform.DOBlendableLocalMoveBy(Vector3.up * 28, .13f))
					.Append(chestTransform.DOScale(Vector3.one, .23f))
					.Join(chestTransform.DOBlendableLocalMoveBy(Vector3.down * 10, .23f));
				await _chestTween.ToUniTask();
			}
			if (this != null && version == _bindingVersion) UpdateNode();
		}

		public async UniTaskVoid ShowRewardDetail(StreakLineChest chest)
		{
			if (chest != null)
			{
				var toast = await RewardDetailToast.Show(chest.Items(), _chestImage != null ? _chestImage.transform : transform, new Vector2(0, 90));
				if (toast != null) toast.SetTopPadding(200);
			}
		}

		private void OnClickClaimButton()
		{
			if (_manager != null)
			{
				var chest = Chest;
				if (chest == null || !_manager.CanClaimChest(chest.Level)) return;
				_manager.ClaimRewards(chest.Level);
				if (!_manager.IsChestClaimed(chest.Level)) return;
				AudioManager.Current?.PlayOneShot(_clickButton, gameObject);
				DOClaimButtonSlideOut().Forget();
				PlayChestAnim(chest).Forget();
				UpdateNode();
				Claimed?.Invoke(this);
			}
		}

		public async UniTask DOClaimButtonSlideIn()
		{
			if (_claimButtonNode != null)
			{
				_claimTween?.Kill();
				_claimButtonNode.gameObject.SetActive(true);
				if (_claimMask != null) { _claimMask.gameObject.SetActive(true); _claimButtonNode.SetParent(_claimMask, true); }
				_claimButtonNode.localPosition = new Vector3(-210, 0, 0);
				AudioManager.Current?.PlayOneShot(_showButton, gameObject);
				int version = _bindingVersion;
				_claimTween = DOTween.Sequence().SetLink(gameObject)
					.Append(_claimButtonNode.DOLocalMoveX(-3, .23f).SetEase(Ease.OutSine))
					.Append(_claimButtonNode.DOLocalMoveX(-12, .13f).SetEase(Ease.InSine));
				await _claimTween.ToUniTask();
				if (this != null && version == _bindingVersion) { _claimButtonNode.SetParent(_claimParent, true); _claimButtonNode.SetSiblingIndex(_claimSibling); }
			}
		}

		public async UniTask DOClaimButtonSlideOut()
		{
			int version = _bindingVersion;
			if (_claimButtonNode != null)
			{
				_claimTween?.Kill();
				if (_claimMask != null) { _claimMask.gameObject.SetActive(true); _claimButtonNode.SetParent(_claimMask, true); }
				_claimTween = _claimButtonNode.DOLocalMoveX(-210, .3f).SetEase(Ease.InBack).SetLink(gameObject);
				await _claimTween.ToUniTask();
				if (this != null && version == _bindingVersion && _claimButtonNode != null)
				{
					_claimButtonNode.gameObject.SetActive(false);
					_claimButtonNode.SetParent(_claimParent, true);
					_claimButtonNode.SetSiblingIndex(_claimSibling);
					_claimButtonNode.localPosition = _claimPosition;
				}
			}
		}

		private void OnDisable() => Release(false);
		private void OnDestroy() => Release(false);
	}
}
