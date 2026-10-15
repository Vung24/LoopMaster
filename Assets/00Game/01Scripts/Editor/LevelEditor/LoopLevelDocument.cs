#if UNITY_EDITOR
using UnityEngine;

public sealed class LoopLevelDocument : ScriptableObject
{
    [SerializeField, TextArea] private string _json;
    public string Json { get => _json; set => _json = value; }
}
#endif
