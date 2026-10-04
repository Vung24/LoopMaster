using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Cube))]
public sealed class SecretBlockMarker : MonoBehaviour
{
    private Cube _cube;
    private Cube Owner => _cube != null ? _cube : (_cube = GetComponent<Cube>());

    public bool IsSecret => Owner != null && Owner.Info != null && Owner.Info.IsSecret;

    // Box clears IsSecret before calling this, so do not gate on that data flag.
    public UniTask Reveal() => Owner != null ? Owner.HiddenSecret() : UniTask.CompletedTask;
}
