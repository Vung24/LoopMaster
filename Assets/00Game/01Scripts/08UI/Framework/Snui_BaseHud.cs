using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class Snui_BaseHud : MonoBehaviour
	{
		[HideInInspector] public string InventoryKey;
		protected int _currentNumber;
		protected CanvasGroup _canvasGroup;
		[SerializeField] protected TMP_Text _text;
		[SerializeField] protected Transform _bgNode;
		[SerializeField] protected Image _icon;
		[SerializeField] private Transform _sparksNode;
		protected float _increaseAnimTime = 0.3f;
		private Tween _numTween;
		private Canvas _sortingCanvas;
		private bool _originalOverrideSorting;
		private int _originalSortingOrder;
		private int _originalSortingLayer;

		public bool AutoUpdate { get; protected set; } = true;

		protected virtual void Awake()
		{
			_canvasGroup = GetComponent<CanvasGroup>();
			if (_text == null)
			{
				Transform textNode = HierarchyReferenceCache.Path(transform, "Count", "Text")
					?? HierarchyReferenceCache.Child(transform, "Text")
					?? HierarchyReferenceCache.Descendant(transform, "Text");
				_text = HierarchyReferenceCache.Component<TMP_Text>(textNode);
			}
			if (_bgNode == null)
				_bgNode = HierarchyReferenceCache.Child(transform, "Bg")
					?? HierarchyReferenceCache.Child(transform, "Background")
					?? HierarchyReferenceCache.Descendant(transform, "Bg")
					?? HierarchyReferenceCache.Descendant(transform, "Background");
			if (_icon == null)
				_icon = HierarchyReferenceCache.Component<Image>(HierarchyReferenceCache.Descendant(transform, "Icon"));
			if (_sparksNode == null)
				_sparksNode = HierarchyReferenceCache.Descendant(transform, "HudSparks")
					?? HierarchyReferenceCache.Descendant(transform, "Sparks");
		}

		protected virtual void Start()
		{
			if (!string.IsNullOrEmpty(InventoryKey))
				SetNumber(Mathf.Max(0, GamePrefs.GetInt(GamePrefKeys.Inventory(InventoryKey), ReadDisplayedNumber())));
			else SetNumber(ReadDisplayedNumber());
		}

		public virtual void PlayShakeAnimation()
		{
			Transform target = _bgNode != null ? _bgNode : transform;
			target.DOKill();
			target.DOPunchScale(Vector3.one * 0.12f, 0.3f, 7, 0.5f).SetUpdate(true);
		}

		public virtual void SetAutoUpdate(bool isAuto) => AutoUpdate = isAuto;
		public Transform GetIconNode() => _icon != null ? _icon.transform : transform;
		public Vector3 GetIconPos() => GetIconNode().position;

		public void SetNumber(int num)
		{
			_currentNumber = Mathf.Max(0, num);
			if (_text != null) _text.SetText(_currentNumber.ToString());
		}

		public Tween IncreaseTo(int num, float duration, Action callback = null)
		{
			_numTween?.Kill();
			int target = Mathf.Max(0, num);
			_numTween = DOTween.To(() => _currentNumber, SetNumber, target, Mathf.Max(0.01f, duration))
				.SetUpdate(true).OnComplete(() => callback?.Invoke());
			return _numTween;
		}

		protected void StopNumberAnimation()
		{
			_numTween?.Kill();
			_numTween = null;
		}

		public Tween IncreaseBy(int num, float duration = 0.3f, Action callback = null) =>
			IncreaseTo(_currentNumber + num, duration, callback);

		public void OnInventoryChanged(string key, int amount)
		{
			if (!AutoUpdate || (!string.IsNullOrEmpty(InventoryKey) && key != InventoryKey)) return;
			SetNumber(amount);
		}

		public void PlaySparks()
		{
			Transform sparks = _sparksNode;
			if (sparks == null) return;
			sparks.gameObject.SetActive(true);
			ParticleSystem[] particles = sparks.GetComponentsInChildren<ParticleSystem>(true);
			for (int i = 0; i < particles.Length; i++) particles[i].Play(true);
		}

		public virtual void SetTop(int order = 1)
		{
			if (_sortingCanvas == null)
			{
				_sortingCanvas = GetComponent<Canvas>();
				if (_sortingCanvas == null) _sortingCanvas = gameObject.AddComponent<Canvas>();
				_originalOverrideSorting = _sortingCanvas.overrideSorting;
				_originalSortingOrder = _sortingCanvas.sortingOrder;
				_originalSortingLayer = _sortingCanvas.sortingLayerID;
			}
			Canvas parentCanvas = transform.parent == null ? null : transform.parent.GetComponentInParent<Canvas>();
			if (parentCanvas != null) _sortingCanvas.sortingLayerID = parentCanvas.sortingLayerID;
			_sortingCanvas.overrideSorting = true;
			_sortingCanvas.sortingOrder = order;
		}

		public virtual void RevertToOriginLayer()
		{
			if (_sortingCanvas == null) return;
			_sortingCanvas.overrideSorting = _originalOverrideSorting;
			_sortingCanvas.sortingOrder = _originalSortingOrder;
			_sortingCanvas.sortingLayerID = _originalSortingLayer;
		}

		public virtual void PlayAddNumberAnimation(int num) => IncreaseBy(num, _increaseAnimTime, PlaySparks);

		protected virtual void OnDestroy()
		{
			_numTween?.Kill();
		}

		private int ReadDisplayedNumber() { int value; return _text != null && int.TryParse(_text.text, out value) ? value : 0; }
	}
}
