using System;

[Serializable]
public sealed class RewardItem
{
    public int Count;
    public RewardType Type;
    public RewardItem() { Type = RewardType.Unknown; }
    public RewardItem(RewardType type, int count) { Type = type; Count = count; }
    public static RewardItem Coin(int count) => new RewardItem(RewardType.Coin, count);
    public static RewardItem Heart(int count) => new RewardItem(RewardType.Heart, count);
    // Native RVA 0x1270444. LevelBox uses the journaled batch API instead.
    public bool Claim(string way = "")
    {
        if (Type == null || Count < 0) return false;
        if (Type == RewardType.Heart || Type == RewardType.InfinityHeart) return true; // unlimited offline
        return InventoryManager.Ensure().Add(Type.ToInventoryKey(), Count, way);
    }
}
