using Cysharp.Threading.Tasks;
using UnityEngine;

public class AdjacentLock : MonoBehaviour
{
	[SerializeField] private AudioClip _blastClip;
	[SerializeField] private Transform _unlockEffect;
	[SerializeField] private Transform _lockObject;
	private bool _onPlayingUnlockAnim;
	public bool IsUnlockAnimationPlaying => _onPlayingUnlockAnim;

	private void Awake()
	{
		if (_unlockEffect == null) _unlockEffect = HierarchyReferenceCache.Child(transform, "UnlockEffect");
		if (_lockObject == null) _lockObject = HierarchyReferenceCache.Child(transform, "AdjacentLock");
	}

	public UniTask UnLock()
	{
		if (_onPlayingUnlockAnim) return default;
		return UnlockRoutine();
	}

	private async UniTask UnlockRoutine()
	{
		_onPlayingUnlockAnim = true;
		var token = this.GetCancellationTokenOnDestroy();
		await UniTask.WaitForSeconds(0.6f, cancellationToken: token);
		if (_unlockEffect != null) _unlockEffect.gameObject.SetActive(true);
		GameVibrationHelper.PlayLockUnlock();
		if (_blastClip != null)
		{
			if (GameAudioPlayer.Current != null) GameAudioPlayer.Current.PlayAdjacentLockBlast(_blastClip);
			else AudioSource.PlayClipAtPoint(_blastClip, transform.position);
		}
		await UniTask.WaitForSeconds(0.1f, cancellationToken: token);
		if (_lockObject != null) _lockObject.gameObject.SetActive(false);
		await UniTask.WaitForSeconds(0.6f, cancellationToken: token);
		Destroy(gameObject);
	}
}
