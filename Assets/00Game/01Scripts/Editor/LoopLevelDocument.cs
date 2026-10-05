#if UNITY_EDITOR
using UnityEngine;

// A serialized undo target keeps the draft alive across Unity domain reloads.
public sealed class LoopLevelDocument : ScriptableObject
{
    [SerializeField, TextArea] private string _json;
    public string Json { get => _json; set => _json = value; }
}
#endif
