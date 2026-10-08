using System;
using Com.ForbiddenByte.OSA.Core;
using Com.ForbiddenByte.OSA.CustomParams;
using UnityEngine;

public class SpecialStreakScrollAdapter : OSA<SpecialStreakScrollParams, SpecialStreakViewsHolder>
{
    // The dialog initializes OSA after applying the safe area and updating the canvas.
    protected override void Start() { }

    public void RefreshRewards(bool focusCurrentReward = false)
    {
        if (!IsInitialized) Init();

        var manager = SpecialStreakManager.Ensure();
        int count = manager.GetRewardInfosConfig()?.Count ?? 0;
        if (GetItemsCount() != count)
        {
            ResetItems(count);
        }
        else
        {
            for (int i = 0; i < VisibleItemsCount; i++)
                UpdateViewsHolder(GetItemViewsHolder(i));
        }

        if (focusCurrentReward && count > 0)
        {
            // Rows run from the grand prize at the top to the first reward at the bottom.
            int row = count - 1 - Mathf.Clamp(manager.ClaimedIndex, 0, count - 1);
            ScrollTo(row, 0.5f, 0.5f);
        }
    }

    protected override SpecialStreakViewsHolder CreateViewsHolder(int itemIndex)
    {
        var holder = new SpecialStreakViewsHolder();
        holder.Init(_Params.ItemPrefab, _Params.Content, itemIndex);
        return holder;
    }

    protected override void UpdateViewsHolder(SpecialStreakViewsHolder holder)
    {
        holder.Cell.Init(holder.ItemIndex);
    }
}

[Serializable]
public class SpecialStreakScrollParams : BaseParamsWithPrefab
{
    public SpecialStreakScrollParams()
    {
        // The authored cell stretches horizontally, so its prefab width can be zero.
        PrefabControlsDefaultItemSize = false;
        DefaultItemSize = 220f;
    }
}

public class SpecialStreakViewsHolder : BaseItemViewsHolder
{
    public SpecialStreakCell Cell { get; private set; }

    public override void CollectViews()
    {
        base.CollectViews();
        Cell = root.GetComponent<SpecialStreakCell>();
    }
}
