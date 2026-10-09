using System;
using Com.ForbiddenByte.OSA.Core;
using Com.ForbiddenByte.OSA.CustomParams;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Snui.StreakLine
{
    public class StreakLineScrollAdapter : OSA<StreakLineScrollParams, StreakLineViewsHolder>
    {
        [SerializeField] private RectTransform _backgroundContent;
        [SerializeField] private RectTransform _balloon;
        private BaseStreakLineManager _manager;
        private float _displayLevel;
        private Tween _riseTween;
        private float _balloonOffset;
        public float RiseDip { get; private set; }
        public float BalloonOffset => _balloonOffset;
        public void SetBalloonOffset(float offset) { _balloonOffset = offset; UpdateBalloon(); }
        public event Action<StreakLineProgress> Claimed;
        public RectTransform BackgroundContent => _backgroundContent;

        public void Prepare(BaseStreakLineManager manager, RectTransform balloon)
        {
            _manager = manager;
            _balloon = balloon;
            if (_backgroundContent != null && manager != null)
                _backgroundContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    manager.GetChests().Count * _Params.DefaultItemSize + _Params.ContentPadding.top + _Params.ContentPadding.bottom);
            if (_balloon != null)
            {
                float x = _balloon.anchoredPosition.x;
                _balloon.SetParent(_Params.Viewport, false);
                _balloon.anchorMin = _balloon.anchorMax = new Vector2(0.5f, 1);
                _balloon.anchoredPosition = new Vector2(x, 0);
                _balloon.SetAsLastSibling();
            }
        }

        protected override void Start() { }

        public void RefreshRewards(BaseStreakLineManager manager, bool focusCurrentLevel = false)
        {
            if (manager == null) return;
            if (_manager != manager || !IsInitialized) _displayLevel = manager.GetPrevViewLevel();
            _manager = manager;
            if (!IsInitialized) Init();
            int count = manager.GetChests().Count;
            if (GetItemsCount() != count) ResetItems(count);
            else RefreshVisibleItems();
            UpdateBackground();
            if (focusCurrentLevel) FocusLevel(_displayLevel);
            UpdateVisibleItemSiblingIndices();
        }

        public async UniTask IncreaseToLevel(int targetLevel, AnimationCurve curve)
        {
            if (!IsInitialized || _manager == null || GetItemsCount() == 0) return;
            _riseTween?.Kill();
            targetLevel = Mathf.Clamp(targetLevel, 0, _manager.GetFinalGoal());
            float distance = Mathf.Abs(GetLevelContentY(targetLevel) - GetLevelContentY(_displayLevel));
            float duration = targetLevel == _displayLevel ? 0f : Mathf.Max(.75f, distance / 700f);
            RiseDip = Mathf.Max(-20, distance * -.08f);
            if (duration > 0f)
            {
                var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
                sequence.Append(DOVirtual.Float(0, RiseDip, .33f, SetBalloonOffset).SetEase(Ease.Linear));
                sequence.Append(DOVirtual.Float(_displayLevel, targetLevel, duration, level =>
                {
                    _displayLevel = level;
                    RefreshVisibleItems();
                }).SetEase(curve));
                sequence.Join(DOVirtual.Float(RiseDip, Mathf.Min(25, distance * .1f), duration, SetBalloonOffset).SetEase(curve));
                float height = (float)GetContentSize();
                float range = height - (float)GetViewportSize();
                float balloonHeight = Mathf.Min(1000f, (float)GetViewportSize() * .75f);
                float targetScroll = range > 0 ? Mathf.Clamp01((height + GetLevelContentY(targetLevel) - balloonHeight) / range) : 0;
                float currentScroll = (float)GetVirtualAbstractNormalizedScrollPosition();
                if (targetScroll > currentScroll)
                    sequence.Join(DOVirtual.Float(currentScroll, targetScroll, duration,
                        position => SetVirtualAbstractNormalizedScrollPosition(position, true, out _)).SetEase(curve));
                _riseTween = sequence;
                await _riseTween.ToUniTask().AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
            }
            _displayLevel = targetLevel;
            RefreshVisibleItems();
            UpdateBalloon();
        }

        public void PlayClaimAnimation(StreakLineChest chest)
        {
            for (int i = 0; i < VisibleItemsCount; i++)
            {
                var cell = GetItemViewsHolder(i).Cell;
                if (cell.Chest != chest) continue;
                cell.DOClaimButtonSlideOut().Forget();
                cell.PlayChestAnim(chest).Forget();
            }
        }

        public StreakLineProgress GetProgressByLevel(int level)
        {
            if (!IsInitialized || _manager == null || GetItemsCount() == 0) return null;
            return GetItemViewsHolderIfVisible(GetItemsCount() - 1 - GetChestIndex(level))?.Cell;
        }

        private int GetChestIndex(float level)
        {
            var chests = _manager.GetChests();
            int index = 0;
            while (index < chests.Count - 1 && level > chests[index].Level) index++;
            return index;
        }

        public float GetLevelContentY(float level)
        {
            if (_manager == null || _manager.GetChests().Count == 0) return 0;
            int index = GetChestIndex(level);
            int from = index == 0 ? 0 : _manager.GetChests()[index - 1].Level;
            float progress = Mathf.InverseLerp(from, _manager.GetChests()[index].Level, level);
            return -_Params.ContentPadding.top - (_manager.GetChests().Count - index) * _Params.DefaultItemSize + progress * _Params.DefaultItemSize;
        }

        public void UpdateVisibleItemSiblingIndices()
        {
            if (!IsInitialized) return;
            for (int i = 0; i < VisibleItemsCount; i++)
            {
                var vh = GetItemViewsHolder(i);
                if (vh != null && vh.root != null && vh.root.GetSiblingIndex() != i)
                {
                    vh.root.SetSiblingIndex(i);
                }
            }
        }

        private void RefreshVisibleItems()
        {
            for (int i = 0; i < VisibleItemsCount; i++) GetItemViewsHolder(i).Cell.SetDisplayLevel(_displayLevel);
            UpdateBalloon();
            UpdateVisibleItemSiblingIndices();
        }

        private void FocusLevel(float level)
        {
            var chests = _manager.GetChests();
            if (chests.Count == 0) return;
            int index = GetChestIndex(level);
            int from = index == 0 ? 0 : chests[index - 1].Level;
            float progress = Mathf.InverseLerp(from, chests[index].Level, level);
            float viewportPivot = Mathf.Clamp01(1f - 400f / (float)GetViewportSize());
            ScrollTo(chests.Count - 1 - index, viewportPivot, 1f - progress);
            UpdateBalloon();
            UpdateVisibleItemSiblingIndices();
        }

        protected override StreakLineViewsHolder CreateViewsHolder(int itemIndex)
        {
            var holder = new StreakLineViewsHolder();
            holder.Init(_Params.ItemPrefab, _Params.Content, itemIndex);
            holder.Cell.OffsetContent(_Params.SegmentOffsetX);
            holder.Cell.Claimed += OnClaimed;
            return holder;
        }

        private void OnClaimed(StreakLineProgress cell) => Claimed?.Invoke(cell);

        protected override void UpdateViewsHolder(StreakLineViewsHolder holder)
        {
            holder.Cell.Bind(_manager, GetItemsCount() - 1 - holder.ItemIndex, _displayLevel);
            UpdateVisibleItemSiblingIndices();
        }

        protected override void OnBeforeRecycleOrDisableViewsHolder(StreakLineViewsHolder holder, int newItemIndex)
        {
            holder.Cell.Release();
            base.OnBeforeRecycleOrDisableViewsHolder(holder, newItemIndex);
        }

        protected override void OnScrollPositionChanged(double normalizedPosition)
        {
            base.OnScrollPositionChanged(normalizedPosition);
            UpdateBackground();
            UpdateBalloon();
            UpdateVisibleItemSiblingIndices();
        }

        private void UpdateBalloon()
        {
            if (_balloon == null || !IsInitialized || _manager == null || GetItemsCount() == 0) return;
            int index = GetChestIndex(_displayLevel);
            var holder = GetItemViewsHolderIfVisible(GetItemsCount() - 1 - index);
            if (holder != null)
            {
                int lower = Mathf.FloorToInt(_displayLevel), upper = Mathf.CeilToInt(_displayLevel);
                var worldPosition = Vector3.Lerp(holder.Cell.GetLevelPosition(lower), holder.Cell.GetLevelPosition(upper), _displayLevel - lower);
                var localPosition = _balloon.localPosition;
                localPosition.y = _Params.Viewport.InverseTransformPoint(worldPosition).y + _balloonOffset;
                _balloon.localPosition = localPosition;
                return;
            }
            int from = index == 0 ? 0 : _manager.GetChests()[index - 1].Level;
            float progress = Mathf.InverseLerp(from, _manager.GetChests()[index].Level, _displayLevel);
            float y = (float)(-ContentVirtualInsetFromViewportStart - _Params.ContentPadding.top
                - (GetItemsCount() - index) * _Params.DefaultItemSize + progress * _Params.DefaultItemSize);
            var position = _balloon.anchoredPosition;
            position.y = y + _balloonOffset;
            _balloon.anchoredPosition = position;
        }

        private void UpdateBackground()
        {
            if (_backgroundContent == null || !IsInitialized) return;
            float height = (float)GetContentSize();
            _backgroundContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            _backgroundContent.anchoredPosition = new Vector2(0,
                (float)(GetViewportSize() - height - ContentVirtualInsetFromViewportStart));
        }

        protected override void OnDisable()
        {
            _riseTween?.Kill();
            base.OnDisable();
        }
    }

    [Serializable]
    public class StreakLineScrollParams : BaseParamsWithPrefab
    {
        public float SegmentOffsetX = -320f;
        public StreakLineScrollParams()
        {
            PrefabControlsDefaultItemSize = false;
            DefaultItemSize = 546f;
            ItemTransversalSize = 100f;
        }
    }

    public class StreakLineViewsHolder : BaseItemViewsHolder
    {
        public StreakLineProgress Cell { get; private set; }

        public override void CollectViews()
        {
            base.CollectViews();
            Cell = root.GetComponent<StreakLineProgress>();
        }
    }
}
