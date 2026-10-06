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
    public bool Claim(string way = "")
    {
        if (Type == null || Count < 0) return false;
        if (Type == RewardType.InfinityHeart)
        {
            Managers.Heart?.AddInfinite(Count);
            return true;
        }
        if (Type == RewardType.Heart)
        {
            Managers.Heart?.AddFreeHeart(FreeHeartType.LevelChest, null, Count);
            return true;
        }
        return InventoryManager.Ensure().Add(Type.ToInventoryKey(), Count, way);
    }
}
