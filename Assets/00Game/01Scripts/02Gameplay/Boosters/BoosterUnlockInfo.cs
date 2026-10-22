using UnityEngine;

public readonly struct BoosterUnlockInfo
{
    public readonly string Key;
    public readonly int UnlockLevel;
    public readonly string Title;
    public readonly string Description;
    public readonly string IconAddress;
    public readonly string IntroPreferenceKey;
    public readonly string FreeUsePreferenceKey;
    public readonly Sprite Icon;

    public BoosterUnlockInfo(string key, int unlockLevel, string title, string description, string iconAddress,
        Sprite icon = null)
    {
        Key = key;
        UnlockLevel = unlockLevel;
        Title = title;
        Description = description;
        IconAddress = iconAddress;
        Icon = icon;
        IntroPreferenceKey = "BoosterIntro_" + key;
        FreeUsePreferenceKey = "BoosterFirstUseFree_" + key;
    }
}
