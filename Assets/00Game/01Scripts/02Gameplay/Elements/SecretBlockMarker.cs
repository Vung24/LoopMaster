using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Cube))]
public sealed class SecretBlockMarker : MonoBehaviour
{
    private Cube _cube;
    private Cube Owner => _cube != null ? _cube : (_cube = GetComponent<Cube>());

    public bool IsSecret => Owner != null && Owner.Info != null && Owner.Info.IsSecret;

    public UniTask Reveal() => Owner != null ? Owner.HiddenSecret() : UniTask.CompletedTask;
}
