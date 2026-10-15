using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class CubeNumLock : MonoBehaviour
{
	[SerializeField] private AudioClip _iceBlastClip;
	[SerializeField] private TMP_Text _countText;
	[SerializeField] private Transform _canvas;
	[SerializeField] private ParticleSystem _decParticle;
	private CubeInfo _cubeInfo;
	private BoxInfo _boxInfo;
	private bool _onPlayingUnlockAnim;
	private bool _isLoaded;
	private CancellationToken _token;
	public bool IsUnlockAnimationPlaying => _onPlayingUnlockAnim;

	private void Awake()
	{
		EnsureLoaded();
	}

	private void EnsureLoaded()
	{
		if (_isLoaded) return;
		_token = destroyCancellationToken;
		if (_canvas == null) _canvas = HierarchyReferenceCache.Child(transform, "Canvas");
		if (_countText == null && _canvas != null)
			_countText = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Child(_canvas, "Count"));
		if (_decParticle == null)
			_decParticle = HierarchyReferenceCache.Component<ParticleSystem>(HierarchyReferenceCache.Child(transform, "IceCubeSmall"));
		EnsureDefaultLayer();
		_isLoaded = true;
	}

	private void EnsureDefaultLayer()
	{
		int targetLayer = gameObject.layer != 5 ? gameObject.layer : 0;
		if (_canvas != null)
		{
			_canvas.gameObject.layer = targetLayer;
			for (int i = 0; i < _canvas.childCount; i++)
			{
				_canvas.GetChild(i).gameObject.layer = targetLayer;
			}
		}
	}

	public async UniTaskVoid Init(CubeInfo info, BoxInfo boxInfo)
	{
		_cubeInfo = info;
		_boxInfo = boxInfo;
		EnsureLoaded();
		if (!_isLoaded)
			await UniTask.WaitUntil(() => _isLoaded, cancellationToken: this.GetCancellationTokenOnDestroy());
		if (_canvas != null && transform.parent != null)
		{
			Vector3 angles = _canvas.eulerAngles;
			_canvas.rotation = Quaternion.Euler(angles.x, transform.parent.rotation.y, angles.z);
		}
		RefreshCount();
		gameObject.SetActive(_cubeInfo != null && _cubeInfo.NumLock > 0);
	}

	public async UniTask DecNumLock()
	{
		if (!_isLoaded || _cubeInfo == null || _boxInfo == null || _onPlayingUnlockAnim ||
			_cubeInfo.NumLock <= 0 || _boxInfo.HasBlockingLock()) return;
		CubeInfo info = _cubeInfo;
		UpdateLockState(info.NumLock - 1).Forget();
		if (info.NumLock == 1) await UnLock();
		else GameVibrationHelper.PlayLockDecrement();
		info.NumLock--;
	}

	private async UniTaskVoid UpdateLockState(int curLockNum)
	{
		await UniTask.WaitForSeconds(0.45f, cancellationToken: _token);
		SetDisplayedCount(curLockNum);
		if (_countText != null) await ScaleCount(1.85f, 0.2f);
		await UniTask.WaitForSeconds(0.02f, cancellationToken: _token);
		if (_countText != null) await ScaleCount(0.8f, 0.1f);
		if (_decParticle != null) _decParticle.Play();
		if (_countText != null) _countText.transform.DOScale(1f, 0.1f).SetLink(gameObject);
		if (_iceBlastClip != null)
		{
			if (GameAudioPlayer.Current != null) GameAudioPlayer.Current.PlayNumLockBlast(_iceBlastClip);
			else AudioSource.PlayClipAtPoint(_iceBlastClip, transform.position);
		}
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
		GameVibrationHelper.PlayLockUnlock();
		await UniTask.WaitForSeconds(0.35f, cancellationToken: _token);
		if (_canvas != null) _canvas.gameObject.SetActive(false);
		await UniTask.WaitForSeconds(0.4f, cancellationToken: _token);
		Destroy(gameObject);
	}

	private void RefreshCount()
	{
		SetDisplayedCount(_cubeInfo == null ? 0 : _cubeInfo.NumLock);
	}

	private void SetDisplayedCount(int value)
	{
		if (_countText != null) _countText.text = value.ToString();
	}

}
