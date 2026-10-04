using UnityEngine;
public sealed class GroupDoorVisualReferences : MonoBehaviour
{
	[SerializeField] private Animation _unlockAnimation;
	[SerializeField] private Transform _keyLockRoot;
	[SerializeField] private Transform _keyHole;
	[SerializeField] private Renderer[] _renderers;

	public Animation UnlockAnimation => _unlockAnimation;
	public Transform KeyLockRoot => _keyLockRoot;
	public Transform KeyHole => _keyHole;
	public Renderer[] Renderers => _renderers;
}
