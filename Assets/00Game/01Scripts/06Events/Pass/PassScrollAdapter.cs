using System;
using Com.ForbiddenByte.OSA.Core;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using UnityEngine;
using UnityEngine.UI;

// Ticket, reward rows and final chest share one virtualized list, with separate pools.
public sealed class PassScrollAdapter : OSA<PassScrollParams, PassViewsHolder>
{
    private BasePassManager _manager;
    private RectTransform _background;
    private PassLockLine _lockLine;
    protected override void Start() { }

    public void Configure(ScrollRect scroll, BasePassManager manager, PassTicketCell ticket,
        PassRewardsCell reward, PassFinalBundleCell final)
    {
        if (IsInitialized) return;
        scroll.StopMovement(); scroll.enabled = false;
        _manager = manager;
        _Params = new PassScrollParams { Content = scroll.content, Viewport = scroll.viewport,
            Ticket = (RectTransform)ticket.transform, Reward = (RectTransform)reward.transform,
            Final = (RectTransform)final.transform, ScrollSensivity = Mathf.Max(100, scroll.scrollSensitivity) };
        _background = new GameObject("PassBackground", typeof(RectTransform)).GetComponent<RectTransform>();
        _background.SetParent(scroll.viewport, false);
        _background.anchorMin = new Vector2(0, 1); _background.anchorMax = Vector2.one;
        _background.pivot = new Vector2(.5f, 1);
        _background.sizeDelta = Vector2.zero;
        _background.SetAsFirstSibling();
        // OSA keeps Content at viewport size. Move the authored path to its own full-height plane.
        while (scroll.content.childCount > 0) scroll.content.GetChild(0).SetParent(_background, false);
        _lockLine = _background.GetComponentInChildren<PassLockLine>(true);
        var rewardsBg = HierarchyReferenceCache.ChildComponent<RectTransform>(_background, "RewardsBg");
        if (rewardsBg != null) rewardsBg.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, manager.GetNormalStageCount() * PassRewardsCell.Height);
        Canvas.ForceUpdateCanvases();
        Init(); ResetItems(manager.GetNormalStageCount() + 2);
        RefreshRewards();
    }

    public void RefreshRewards()
    {
        if (!IsInitialized) return;
        for (int i = 0; i < VisibleItemsCount; i++)
        {
            var holder = GetItemViewsHolder(i);
            holder.Reward?.Refresh(); holder.Final?.Init(); holder.Ticket?.Refresh();
        }
        _lockLine?.Refresh();
        UpdateBackground();
    }

    public PassRewardsCell GetRewardCell(int stage) => IsInitialized ? GetItemViewsHolderIfVisible(stage + 1)?.Reward : null;
    public void FocusCurrentReward()
    {
        if (!IsInitialized) return;
        if (_manager.GetCurrentStage() < 2) SetNormalizedPosition(1);
        else if (_manager.IsFinalRewardUnlocked()) ScrollTo(GetItemsCount() - 1, 1, 1);
        else ScrollTo(Mathf.Clamp(_manager.GetCurrentStage(), 1, GetItemsCount() - 2));
    }

    public void PlayVipUnlock()
    {
        for (int i = 0; i < VisibleItemsCount; i++)
        {
            var cell = GetItemViewsHolder(i).Reward;
            if (cell != null && cell.Stage > 0) cell.PlayUnlockVipAnim().Forget();
        }
    }

    protected override void CollectItemsSizes(ItemCountChangeMode mode, int count, int index, ItemsDescriptor sizes)
    {
        base.CollectItemsSizes(mode, count, index, sizes);
        if (sizes.itemsCount == 0) return;
        sizes.BeginChangingItemsSizes(0); sizes[0] = PassTicketCell.Height; sizes.EndChangingItemsSizes();
        if (sizes.itemsCount > 1)
        {
            int last = sizes.itemsCount - 1;
            sizes.BeginChangingItemsSizes(last); sizes[last] = PassFinalBundleCell.Height; sizes.EndChangingItemsSizes();
        }
    }

    private int Kind(int index) => index == 0 ? 0 : index == _manager.GetNormalStageCount() + 1 ? 2 : 1;
    protected override PassViewsHolder CreateViewsHolder(int index)
    {
        int kind = Kind(index);
        var holder = new PassViewsHolder { Kind = kind };
        holder.Init(kind == 0 ? _Params.Ticket : kind == 2 ? _Params.Final : _Params.Reward, _Params.Content, index);
        return holder;
    }
    protected override bool IsRecyclable(PassViewsHolder holder, int index, double size) => holder.Kind == Kind(index);
    protected override void UpdateViewsHolder(PassViewsHolder holder)
    {
        holder.root.localScale = Vector3.one;
        if (holder.Reward != null) holder.Reward.Init(holder.ItemIndex - 1);
        holder.Final?.Init(); holder.Ticket?.Refresh();
    }
    protected override void OnBeforeRecycleOrDisableViewsHolder(PassViewsHolder holder, int index)
    {
        foreach (var node in holder.root.GetComponentsInChildren<Transform>(true)) node.DOKill();
        base.OnBeforeRecycleOrDisableViewsHolder(holder, index);
    }
    protected override void OnScrollPositionChanged(double position)
    {
        base.OnScrollPositionChanged(position); UpdateBackground();
    }
    private void UpdateBackground()
    {
        if (_background == null || !IsInitialized) return;
        _background.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, (float)GetContentSize());
        _background.anchoredPosition = new Vector2(0, (float)-ContentVirtualInsetFromViewportStart);
    }
}

[Serializable]
public sealed class PassScrollParams : BaseParams
{
    public RectTransform Ticket, Reward, Final;
    public PassScrollParams() { DefaultItemSize = PassRewardsCell.Height; ItemTransversalSize = 0; }
}

public sealed class PassViewsHolder : BaseItemViewsHolder
{
    public int Kind;
    public PassRewardsCell Reward;
    public PassTicketCell Ticket;
    public PassFinalBundleCell Final;
    public override void CollectViews()
    {
        base.CollectViews();
        Reward = root.GetComponent<PassRewardsCell>();
        Ticket = root.GetComponent<PassTicketCell>();
        Final = root.GetComponent<PassFinalBundleCell>();
    }
}
