using System;
using UnityEngine;

[Serializable]
public sealed class BoosterDefinition
{
    [SerializeField] private string _key;
    [SerializeField, Min(1)] private int _unlockLevel = 1;
    [SerializeField] private string _title;
    [SerializeField, TextArea(2, 4)] private string _description;
    [SerializeField] private Sprite _icon;
    [SerializeField] private string _iconAddress;

    public string Key => _key;
    public int UnlockLevel => _unlockLevel;

    public BoosterUnlockInfo CreateInfo()
    {
        return new BoosterUnlockInfo(_key, _unlockLevel, _title, _description, _iconAddress, _icon);
    }
}
