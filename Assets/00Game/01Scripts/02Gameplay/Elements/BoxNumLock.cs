using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class BoxNumLock : MonoBehaviour
{
	[SerializeField] private AudioClip _snowBlastClip;
	[SerializeField] private TMP_Text _countText;
	[SerializeField] private Transform _canvas;
	[SerializeField] private GameObject _numLockObj;
	[SerializeField] private ParticleSystem _decParticle;
	[SerializeField] private ParticleSystem _unlockParticle;
	private BoxInfo _boxInfo;
	private bool _onPlayingUnlockAnim;
	private CancellationToken _token;
	public bool IsUnlockAnimationPlaying => _onPlayingUnlockAnim;

	private void Awake()
	{
		_token = destroyCancellationToken;
		if (_numLockObj == null)
		{
			Transform node = HierarchyReferenceCache.Child(transform, "NumLock");
			if (node != null) _numLockObj = node.gameObject;
		}
		if (_canvas == null) _canvas = HierarchyReferenceCache.Child(transform, "Canvas");
		if (_countText == null)
			_countText = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Child(_canvas, "Count"));
		_decParticle = HierarchyReferenceCache.Component<ParticleSystem>(HierarchyReferenceCache.Child(transform, "IceCubeSmall")) ?? _decParticle;
		_unlockParticle = HierarchyReferenceCache.Component<ParticleSystem>(HierarchyReferenceCache.Child(transform, "IceCube")) ?? _unlockParticle;
	}

	public void Init(BoxInfo info)
	{
		_boxInfo = info;
		if (_canvas != null && info != null)
		{
			Vector3 angles = _canvas.eulerAngles;
			_canvas.rotation = Quaternion.Euler(angles.x, angles.y, info.Rotation.y);
		}
		RefreshCount();
		if (_numLockObj != null) _numLockObj.SetActive(_boxInfo != null && _boxInfo.NumLock > 0);
	}

	public async UniTask DecNumLock()
	{
		if (_boxInfo == null || _onPlayingUnlockAnim || _boxInfo.NumLock <= 0 ||
			_boxInfo.HasBlockingLockExceptNumLock()) return;
		BoxInfo info = _boxInfo;
		UpdateLockState(info.NumLock - 1).Forget();
		// Native DecNumLock keeps the final logical lock until UnLock completes.
		if (info.NumLock == 1) await UnLock();
		else GameVibrationHelper.PlayLockDecrement();
		info.NumLock--;
	}

	private async UniTaskVoid UpdateLockState(int curLockNum)
	{
		await UniTask.WaitForSeconds(0.45f, cancellationToken: _token);
		SetDisplayedCount(curLockNum);
		if (_countText != null)
			await ScaleCount(1.85f, 0.2f);
		await UniTask.WaitForSeconds(0.02f, cancellationToken: _token);
		if (_countText != null)
			await ScaleCount(0.8f, 0.1f);
		if (curLockNum > 0 && _decParticle != null) _decParticle.Play();
		if (_countText != null) _countText.transform.DOScale(1f, 0.1f).SetLink(gameObject);
	}

	private async UniTask ScaleCount(float scale, float duration)
	{
		Tween tween = _countText.transform.DOScale(scale, duration).SetLink(gameObject);
		try { await UniTask.WaitUntil(() => !tween.IsActive() || tween.IsComplete(), cancellationToken: _token); }
		finally { if (tween.IsActive()) tween.Kill(); }
	}

	private async UniTask UnLock()
	{
		_onPlayingUnlockAnim = true;
		await UniTask.WaitForSeconds(0.45f, cancellationToken: _token);
		if (_snowBlastClip != null)
		{
			if (GameAudioPlayer.Current != null) GameAudioPlayer.Current.PlayNumLockBlast(_snowBlastClip);
			else AudioSource.PlayClipAtPoint(_snowBlastClip, transform.position);
		}
		GameVibrationHelper.PlayLockUnlock();
		await UniTask.WaitForSeconds(0.3f, cancellationToken: _token);
		if (_unlockParticle != null)
		{
			_unlockParticle.gameObject.SetActive(true);
			_unlockParticle.Play();
		}
		await UniTask.WaitForSeconds(0.05f, cancellationToken: _token);
		if (_numLockObj != null) _numLockObj.SetActive(false);
		if (_canvas != null) _canvas.gameObject.SetActive(false);
		await UniTask.WaitForSeconds(0.9f, cancellationToken: _token);
		Destroy(gameObject);
	}

	private void RefreshCount()
	{
		SetDisplayedCount(_boxInfo == null ? 0 : _boxInfo.NumLock);
	}

	private void SetDisplayedCount(int value)
	{
		if (_countText != null) _countText.text = value.ToString();
	}

}
