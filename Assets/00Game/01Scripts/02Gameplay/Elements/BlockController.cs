using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Snui
{
    public class BlockController : MonoBehaviour
    {
        public static readonly int BLOCK_SIZE = 100;
        public delegate void OnUseScrewdriverDelegate();
        public OnUseScrewdriverDelegate OnUseScrewdriverHandler;
        public UnityAction<BlockController> OnOpenBlock;
        public UnityAction OnShowUsedFailToast;
        [SerializeField] private Image _glow;
        [SerializeField] private Image _shape;
        private List<ScrewController> _screws = new List<ScrewController>();
        private BlockData _data;
        private KeyHunterBoardContext _context;
        private bool _dropping;

        public bool IsPrize() => _data != null && _data.IsPrize;
        public bool IsSinglePrizeBlock() => IsPrize() && _screws.Count == 1;
        public ScrewController GetTutorialScrew() => _screws.FirstOrDefault();
        public int GetScrewCount() => _screws.Count;
        public int ScrewDataCount => _data?.BlockCount ?? 0;
        public int GetCoin()
        {
            if (_context?.GetCoinPerCell == null) throw new InvalidOperationException("Key Hunter reward coefficient is unavailable.");
            return _data.BlockCount * _context.GetCoinPerCell();
        }

        public static BlockController Create(BlockData data, Transform parent)
            => Create(data, parent, BoardController.DefaultContext);
        public static BlockController Create(BlockData data, Transform parent, KeyHunterBoardContext context)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            BlockController prefab = context?.GetBlockPrefab?.Invoke(data.Shape);
            if (prefab == null) throw new InvalidOperationException("Missing Key Hunter block prefab for shape: " + data.Shape);
            if (context.ScrewPrefab == null || context.GetColor == null)
                throw new InvalidOperationException("Key Hunter screw prefab and color provider must be supplied.");
            BlockController block = Instantiate(prefab, parent);
            block._context = context;
            block.Init(data);
            return block;
        }

        private void Awake()
        {
            if (_glow == null)
                _glow = HierarchyReferenceCache.Component<Image>(HierarchyReferenceCache.Child(transform, "Glow"));
            if (_shape == null)
                _shape = HierarchyReferenceCache.Component<Image>(HierarchyReferenceCache.Child(transform, "Shape"));
        }
        private void Start()
        {
            if (_glow == null) return;
            _glow.gameObject.SetActive(true);
            Color color = _glow.color;
            color.a = 0f;
            _glow.color = color;
        }
        private void Init(BlockData data)
        {
            _data = data;
            transform.localPosition = new Vector3(data.PosX, data.PosY, 0f);
            if (_shape != null) _shape.color = _context.GetColor(data.Color);
            if (data.Screws == null) data.InitScrews();
            CreateScrews();
        }
        public Vector2 GetCenterPosition() => transform.TransformPoint(GetLocalCenterPos());
        private Vector2 GetLocalCenterPos() => ((RectTransform)transform).rect.center;

        private void CreateScrews()
        {
            _screws.Clear();
            int rows = _data.Rows, cols = _data.Cols;
            if (_data.Screws.Length != rows * cols) throw new ArgumentException("Key Hunter screw grid does not match its shape.");
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                int index = row * cols + col;
                if (!_data.Screws[index]) continue;
                ScrewController screw = Instantiate(_context.ScrewPrefab, transform);
                screw.Init(_data, index, _context);
                screw.transform.localPosition = new Vector3(col * BLOCK_SIZE + 50f, (rows - row) * BLOCK_SIZE - 50f, 0f);
                screw.OnUsedScrewdriver += OnUsedScrewdriver;
                screw.OnScrewDropped += OnScrewDropped;
                screw.OnShowUsedFailToast += ShowFailToast;
                _screws.Add(screw);
            }
        }
        private void ShowFailToast() => OnShowUsedFailToast?.Invoke();
        private void OnUsedScrewdriver()
        {
            PlayGlowLight();
            OnUseScrewdriverHandler?.Invoke();
        }
        private void OnScrewDropped(ScrewController screw)
        {
            if (!_screws.Remove(screw) || _screws.Count != 0) return;
            OnOpenBlock?.Invoke(this);
            PlayDropAnimation();
        }
        private Tween PlayGlowLight()
        {
            if (_glow == null) return null;
            _glow.DOKill();
            return DOTween.Sequence().Append(_glow.DOFade(1f, 0.37f)).Append(_glow.DOFade(0f, 0.37f)).SetLink(gameObject);
        }
        public Tween PlayDropAnimation()
        {
            if (_dropping) return null;
            _dropping = true;
            DisableClick();
            transform.DOKill();
            if (transform is RectTransform rect)
            {
                Vector3 center = rect.TransformPoint(rect.rect.center);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.position += center - rect.TransformPoint(rect.rect.center);
            }
            if (transform.parent != null) transform.SetParent(transform.parent.parent, true);
            return DOTween.Sequence()
                .Append(transform.DOLocalRotate(new Vector3(0f, 0f, 8f), 0.53f).SetEase(Ease.InSine))
                .Append(transform.DOBlendableLocalMoveBy(new Vector3(0f, -650f, 0f), 0.4f).SetEase(Ease.InSine))
                .OnComplete(() => Destroy(gameObject)).SetLink(gameObject);
        }
        public void AutoScrewing()
        {
            foreach (ScrewController screw in _screws.ToArray()) if (screw != null) screw.Use(false).Forget();
        }
        public void DisableClick() { foreach (ScrewController screw in _screws) if (screw != null) screw.DisableClick(); }
        public void EnableClick() { foreach (ScrewController screw in _screws) if (screw != null) screw.EnableClick(); }
    }
}
