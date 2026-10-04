using System;
using System.Collections.Generic;

// RewardType..cctor RVA 0x126FCD0. This is a native data class, not a component.
public sealed class RewardType
{
    public readonly string Key;
    public readonly int Value;
    private RewardType(int value, string key) { Value = value; Key = key; }
    public static readonly RewardType Undo = new RewardType(11, "undo");
    public static readonly RewardType Shuffle = new RewardType(12, "shuffle");
    public static readonly RewardType Path = new RewardType(13, "path");
    public static readonly RewardType Unknown = new RewardType(-1, "unknown");
    public static readonly RewardType Coin = new RewardType(0, "coin");
    public static readonly RewardType Heart = new RewardType(1, "heart");
    public static readonly RewardType InfinityHeart = new RewardType(31, "inf_heart");
    public static readonly RewardType CardPack1 = new RewardType(61, "card_1");
    public static readonly RewardType CardPack2 = new RewardType(62, "card_2");
    public static readonly RewardType CardPack3 = new RewardType(63, "card_3");
    public static readonly RewardType CardPack4 = new RewardType(64, "card_4");
    public static readonly RewardType CardPack5 = new RewardType(65, "card_5");
    private static readonly RewardType[] All = { Undo, Shuffle, Path, Unknown, Coin, Heart, InfinityHeart,
        CardPack1, CardPack2, CardPack3, CardPack4, CardPack5 };
    public static RewardType FromInt(int value) { foreach (var type in All) if (type.Value == value) return type; return Unknown; }
    public static RewardType FromString(string key) { foreach (var type in All) if (type.Key == key) return type; return Unknown; }
    public static IReadOnlyList<RewardType> GetAllRewardTypes() => Array.AsReadOnly(All);
    public bool IsShowInfinityIcon() => Value >= 31 && Value <= 60;
    public bool IsTimeLimited() => IsShowInfinityIcon();
    public bool IsCardPack() => Value >= 61 && Value <= 65;
    public string ToInventoryKey() => Key;
    public string GetIconPath()
    {
        if (this == Coin) return "Snui/Common/Sprites/Coin/img_coin_2.png";
        if (this == Heart) return "Snui/Common/Sprites/Heart/img_common_heart_normal.png";
        if (this == InfinityHeart) return "Snui/Common/Sprites/Heart/img_common_heart_infinity.png";
        if (this == Undo || this == Shuffle || this == Path) return "Common/Sprite/icon_" + Key + ".png";
        return null;
    }
}
