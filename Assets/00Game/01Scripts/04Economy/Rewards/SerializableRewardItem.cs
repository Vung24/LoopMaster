using System;

[Serializable]
public class SerializableRewardItem
{
	public string Key;
	public int Count;
	public static implicit operator RewardItem(SerializableRewardItem value) => value == null ? null : new RewardItem(RewardType.FromString(value.Key), value.Count);
}
