using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;

public class Cube : MonoBehaviour
{
	public AssetReference SecretParticle;

	private CubeInfo _info;
	private Box _box;
	[SerializeField] private SkinnedMeshRenderer _meshRenderer;
	[SerializeField] private Animation _anim;
	[SerializeField] private SpriteRenderer _square;
	[SerializeField] private Transform _cubeRoot;
	private MaterialPropertyBlock _outlinePropBlock;
	[SerializeField] private GroupColorLockKey _groupKey;
	[SerializeField] private CubeNumLock _numLock;
	private GameLoader _gameLoader;
	private float _sizeInterval = 0.1f;
	private CancellationToken _token;
	private Coroutine _sizeAnimation;
	private bool _isDismissing;
	private bool _secretRevealPending;
	private SecretBlockMarker _secretMarker;
	private readonly CubeArrivalState _arrivals = new CubeArrivalState();
	private CancellationTokenSource _surpriseLifetime;
	private UniTask _surpriseTask;
	private int _visualVersion;
	private readonly List<GameObject> _secretParticles = new List<GameObject>();

	public CubeInfo Info => _info;
	public SecretBlockMarker SecretMarker => _secretMarker;
	public bool IsNumLockAnimationPlaying => _numLock != null && _numLock.IsUnlockAnimationPlaying;
	public int VisibleSize => _info == null ? 0 : _arrivals.VisibleSize(_info.Size);
	internal bool IsSurpriseTransformationPlaying => _surpriseLifetime != null;
	public bool IsFinishAnimationPlaying => isActiveAndEnabled &&
		(IsSurpriseTransformationPlaying || _sizeAnimation != null || (_anim != null && _anim.IsPlaying("Skeleton|cube_all")));

	private void Awake()
	{
		_token = destroyCancellationToken;
		EnsureCubeRoot();
	}

	private Transform CubeRootTransform => EnsureCubeRoot();

	private Transform EnsureCubeRoot()
	{
		if (_cubeRoot != null) return _cubeRoot;
		if (_anim != null) return _cubeRoot = _anim.transform;
		return _cubeRoot = HierarchyReferenceCache.Child(transform, "CubeRoot");
	}

	private void RefreshVisibility()
	{
		Transform root = CubeRootTransform;
		bool shouldBeVisible = _info == null || VisibleSize > 0;
		if (root != null)
		{
			if (root.gameObject.activeSelf != shouldBeVisible)
				root.gameObject.SetActive(shouldBeVisible);
		}
		else if (_meshRenderer != null)
		{
			if (_meshRenderer.enabled != shouldBeVisible)
				_meshRenderer.enabled = shouldBeVisible;
		}
	}

	public void Init(CubeInfo info, Box box, GameLoader gameLoader, string anim = "")
	{
		CancelSurpriseTransformation();
		if (_sizeAnimation != null) StopCoroutine(_sizeAnimation);
		_sizeAnimation = null;
		_isDismissing = false;
		_arrivals.Reset();
		if (_info != null && _info != info && _info.CubeSc == this) _info.CubeSc = null;
		_info = info;
		_secretRevealPending = info != null && info.IsSecret;
		if (_secretRevealPending)
			_secretMarker = GetComponent<SecretBlockMarker>() ?? gameObject.AddComponent<SecretBlockMarker>();
		_box = box;
		_gameLoader = gameLoader;
		if (_info == null || _box == null) return;

		_info.CubeSc = this;
		transform.position = _box.GetCubePos(_info, visibleSize: VisibleSize);
		transform.rotation = _box.transform.rotation;
		InitSpecial();
		RefreshSize();
		if (!string.IsNullOrEmpty(anim)) PlayAnim(anim);
		RefreshMaterial();
		RefreshSquareSprite();
		if (_groupKey != null && _info.GroupKeyId > 0) _groupKey.Init(_info.GroupKeyColor);
		if (_numLock != null) _numLock.Init(_info, _box.Info).Forget();
	}

	private void InitSpecial()
	{
		if (_info == null || _gameLoader == null) return;
		if (_info.GroupKeyId > 0 && _groupKey == null && _gameLoader.GroupKey != null)
		{
			GameObject key = Instantiate(_gameLoader.GroupKey, transform);
			_groupKey = key.GetComponent<GroupColorLockKey>() ?? key.AddComponent<GroupColorLockKey>();
		}
		if (_info.NumLock > 0 && _numLock == null && _gameLoader.CubeNumLock != null)
		{
			GameObject lockObject = Instantiate(_gameLoader.CubeNumLock, transform);
			lockObject.transform.localPosition = Vector3.zero;
			lockObject.transform.localRotation = Quaternion.identity;
			_numLock = lockObject.GetComponent<CubeNumLock>() ?? lockObject.AddComponent<CubeNumLock>();
		}
		RefreshGroupKey();
	}

	private void RefreshGroupKey()
	{
		if (_info == null) return;
		if (_info.GroupKeyId <= 0) DestroyGroupKey();
		else if (_groupKey != null) _groupKey.Init(_info.GroupKeyColor);
	}

	private void DestroyGroupKey()
	{
		if (_groupKey == null) return;
		_groupKey.DestroySelf();
		_groupKey = null;
	}

	public void SetCubeOutline(bool enable)
	{
		if (_meshRenderer == null) return;
		if (_outlinePropBlock == null) _outlinePropBlock = new MaterialPropertyBlock();
		_meshRenderer.GetPropertyBlock(_outlinePropBlock);
		_outlinePropBlock.SetFloat("_Outline", enable ? 1f : 0f);
		_outlinePropBlock.SetFloat("_OutlineEnabled", enable ? 1f : 0f);
		_meshRenderer.SetPropertyBlock(_outlinePropBlock);
	}

	public async UniTask TryDescNumLock()
	{
		if (_info == null || _info.NumLock <= 0 || _box == null || _box.Info == null) return;
		CubeInfo info = _info;
		CubeNumLock numLock = _numLock;
		if (numLock != null) await numLock.DecNumLock();
		else if (!_box.Info.HasBlockingLock()) info.NumLock--;
		if (this == null || _info != info || _numLock != numLock) return;
		if (info.NumLock <= 0)
		{
			_numLock = null;
			_box.TryHandleFinish();
		}
	}

	public void PlayAnim(string animName)
	{
		if (_info != null && _info.NumLock > 0) return;
		if (_anim != null && !string.IsNullOrEmpty(animName) && _anim.GetClip(animName) != null)
			_anim.Play(animName);
	}

	public GroupColorLockKey TakeGroupColorLockKey()
	{
		GroupColorLockKey result = _groupKey;
		_groupKey = null;
		if (_info != null) _info.ClearGroupColorLockKey();
		RefreshSquareSprite();
		return result;
	}

	public void InitFinish(int type)
	{
		if (_meshRenderer != null)
		{
			Material material = GameAssetsConfig.GetMatByType(type);
			if (material != null) _meshRenderer.material = material;

			if (_outlinePropBlock == null) _outlinePropBlock = new MaterialPropertyBlock();
			_meshRenderer.GetPropertyBlock(_outlinePropBlock);
			Color color = GameAssetsConfig.GetCubeColor(type);
			_outlinePropBlock.SetColor("_BaseColor", color);
			_outlinePropBlock.SetColor("_Color", color);
			_meshRenderer.SetPropertyBlock(_outlinePropBlock);
		}

		if (_square == null) return;
		Sprite finishSprite = GameAssetsConfig.GetCubeFinishSprite();
		if (finishSprite != null) _square.sprite = finishSprite;
		_square.color = Color.white;
		_square.gameObject.SetActive(_square.sprite != null);
	}

	public UniTask HiddenSecret()
	{
		if (_info == null || !_secretRevealPending) return UniTask.CompletedTask;
		_secretRevealPending = false;
		return RevealSecretAsync();
	}

	private async UniTask RevealSecretAsync()
	{
		CubeInfo info = _info;
		GameData data = GameData.Current;
		int version = _visualVersion;

		_info.IsSecret = false;
		if (GameData.Current != null) GameData.Current.SecretClearCount++;
		PlaySecretParticle().Forget();
		await UniTask.WaitForSeconds(0.15f, false, PlayerLoopTiming.Update, _token);
		if (!IsCurrentVisual(info, data, version)) return;
		RefreshMaterial();
		RefreshSquareSprite();
	}

	private async UniTask PlaySecretParticle()
	{
		await PlaySecretParticle(_info, GameData.Current, _visualVersion);
	}

	private async UniTask PlaySecretParticle(CubeInfo info, GameData data, int version)
	{
		if (SecretParticle == null || !SecretParticle.RuntimeKeyIsValid()) return;

		var handle = AddressableUtils.AsyncLoadAsGameObject(SecretParticle, transform);
		GameObject particle = await handle.Task;
		if (particle == null) return;
		if (!IsCurrentVisual(info, data, version)) { Destroy(particle); return; }
		particle.transform.SetParent(transform, false);
		particle.transform.localPosition = new Vector3(0f, 0.7f, 0f);
		_secretParticles.RemoveAll(item => item == null);
		_secretParticles.Add(particle);
	}

	public UniTask UnlockSurpriseCube(int type, bool isSecret)
	{
		return StartSurpriseTransformation(type, isSecret, null);
	}

	internal UniTask UnlockSurpriseCube(CubeInfo replacement)
	{
		return StartSurpriseTransformation(replacement.Type, replacement.IsSecret, replacement);
	}

	private UniTask StartSurpriseTransformation(int type, bool isSecret, CubeInfo replacement)
	{
		if (IsSurpriseTransformationPlaying) return _surpriseTask;
		if (_info == null || !isActiveAndEnabled ||
			(!CubeData.IsSurpriseColor(_info.Type) && _info.Type == type && _info.IsSecret == isSecret))
			return UniTask.CompletedTask;
		var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_token);
		_surpriseLifetime = lifetime;
		var completion = new UniTaskCompletionSource();
		_surpriseTask = completion.Task;
		CompleteSurpriseTask(TransformSurpriseAsync(_info, GameData.Current, _visualVersion,
			type, isSecret, replacement, lifetime), completion).Forget();
		return _surpriseTask;
	}

	internal static async UniTaskVoid CompleteSurpriseTask(UniTask work, UniTaskCompletionSource completion)
	{
		try { await work; completion.TrySetResult(); }
		catch (System.OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
		catch (System.Exception error) { completion.TrySetException(error); }
	}

	private bool IsCurrentVisual(CubeInfo info, GameData data, int version) =>
		this != null && isActiveAndEnabled && _info == info && _visualVersion == version && GameData.Current == data;

	private async UniTask TransformSurpriseAsync(CubeInfo info, GameData data, int version,
		int type, bool isSecret, CubeInfo replacement, CancellationTokenSource lifetime)
	{
		CancellationToken token = lifetime.Token;
		try
		{
			await UniTask.WaitForSeconds(0.5f, false, PlayerLoopTiming.Update, token);
			if (!IsCurrentVisual(info, data, version)) throw new System.OperationCanceledException(token);
			CommitSurpriseData(info, data, type, isSecret, replacement);
			_secretRevealPending = isSecret;
			if (isSecret) _secretMarker = GetComponent<SecretBlockMarker>() ?? gameObject.AddComponent<SecretBlockMarker>();
			PlaySecretParticle(info, data, version).Forget();
			await UniTask.WaitForSeconds(0.15f, false, PlayerLoopTiming.Update, token);
			if (!IsCurrentVisual(info, data, version)) throw new System.OperationCanceledException(token);
			RefreshMaterial();
			RefreshSquareSprite();
		}
		finally
		{
			if (_surpriseLifetime == lifetime) _surpriseLifetime = null;
			lifetime.Dispose();
		}
	}

	internal static void CommitSurpriseData(CubeInfo info, GameData data, int type, bool isSecret, CubeInfo replacement)
	{
		if (isSecret && !info.IsSecret && data != null) data.SecretCount++;
		info.Type = type;
		info.IsSecret = isSecret;
		if (replacement == null) return;
		info.GroupKeyId = replacement.GroupKeyId;
		info.GroupKeyColor = replacement.GroupKeyColor;
		info.NumLock = replacement.NumLock;
	}

	internal void CancelSurpriseTransformation()
	{
		_visualVersion++;
		CancellationTokenSource lifetime = _surpriseLifetime;
		_surpriseLifetime = null;
		lifetime?.Cancel();
		foreach (GameObject particle in _secretParticles) if (particle != null) Destroy(particle);
		_secretParticles.Clear();
		// If data was committed before cancellation, keep a reused visual consistent.
		RefreshMaterial();
		RefreshSquareSprite();
	}

	public void ReserveIncomingSlice(bool newCube)
	{
		_arrivals.Reserve();
		if (newCube)
		{
			// Reserve capacity at launch; keep the new renderer empty until landing.
			transform.localScale = GetTargetScale();
			RefreshPosition();
		}
		RefreshVisibility();
	}

	public void CompleteIncomingSlice()
	{
		if (_info == null || _isDismissing || !_arrivals.Arrive()) return;
		RefreshVisibility();
		// Exclude every slice still flying from the visual target.
		AnimateSize(GetTargetScale(), _box == null ? transform.position :
			_box.GetCubePos(_info, visibleSize: VisibleSize), 0f, 0.12f, false, false);
	}
	public void PlayDescAnim(float interval = 0.2f)
	{
		if (_info == null || _isDismissing) return;
		AnimateSize(GetTargetScale(), _box == null ? transform.position : _box.GetCubePos(_info, visibleSize: VisibleSize),
			0f, Mathf.Max(0.01f, interval), false, true);
	}

	public void CheckEmpty()
	{
		if (_info == null || _info.Size > 0) return;
		if (_box != null) _box.Cubes.Remove(this);
		if (_info.CubeSc == this) _info.CubeSc = null;
		gameObject.SetActive(false);
	}

	public void PlayDismissAnim()
	{
		if (_isDismissing) return;
		_isDismissing = true;
		AnimateSize(Vector3.one * 0.01f, transform.position, 0f, 0.1f, true, false);
	}

	public void PlayAddAnim(float delay, float interval = 0.12f)
	{
		if (_info == null || _isDismissing) return;
		Vector3 target = GetTargetScale();
		Vector3 current = transform.localScale;

		// A freshly created cube has already been initialized at its target size,
		// so grow it from zero. When a slice is added to an existing cube, preserve
		// its current size and animate only the newly added thickness.
		if (Mathf.Approximately(current.z, target.z)) current.z = 0f;
		transform.localScale = current;
		AnimateSize(target, _box == null ? transform.position : _box.GetCubePos(_info, visibleSize: VisibleSize),
			Mathf.Max(0f, delay), Mathf.Max(0.01f, interval), false, false);
	}

	public void PlayShuffleMove(float duration = 0.6f)
	{
		if (_info == null || _box == null || _isDismissing) return;
		AnimateSize(GetTargetScale(), _box.GetCubePos(_info, visibleSize: VisibleSize), 0f,
			Mathf.Max(0.01f, duration), false, false);
	}

	private void AnimateSize(Vector3 scale, Vector3 position, float delay, float duration,
		bool deactivate, bool checkEmpty)
	{
		if (_sizeAnimation != null) StopCoroutine(_sizeAnimation);
		_sizeAnimation = StartCoroutine(AnimateSizeRoutine(scale, position, delay, duration, deactivate, checkEmpty));
	}

	private IEnumerator AnimateSizeRoutine(Vector3 target, Vector3 position, float delay,
		float duration, bool deactivate, bool checkEmpty)
	{
		if (delay > 0f) yield return new WaitForSeconds(delay);
		Vector3 start = transform.localScale;
		Vector3 startPosition = transform.position;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = 1f - (1f - t) * (1f - t);
			transform.localScale = Vector3.Lerp(start, target, eased);
			transform.position = Vector3.Lerp(startPosition, position, eased);
			yield return null;
		}
		transform.localScale = target;
		transform.position = position;
		_sizeAnimation = null;
		if (deactivate) gameObject.SetActive(false);
		else if (checkEmpty) CheckEmpty();
	}

	private void OnDisable()
	{
		CancelSurpriseTransformation();
		if (_sizeAnimation != null) StopCoroutine(_sizeAnimation);
		_sizeAnimation = null;
	}

	private void RefreshSize()
	{
		if (_info == null) return;
		transform.localScale = GetTargetScale();
		RefreshPosition();
		RefreshVisibility();
	}

	private Vector3 GetTargetScale()
	{
		Vector3 scale = transform.localScale;
		if (_info != null) scale.z = Mathf.Max(0f, _sizeInterval * VisibleSize);
		return scale;
	}

	private void RefreshPosition()
	{
		if (_info != null && _box != null)
			transform.position = _box.GetCubePos(_info, visibleSize: VisibleSize);
	}

	private void RefreshMaterial()
	{
		if (_meshRenderer == null || _info == null) return;
		Material material = _info.IsSecret ? GameAssetsConfig.GetSecretMat() : GameAssetsConfig.GetMatByType(_info.Type);
		if (material != null) _meshRenderer.material = material;

		if (_outlinePropBlock == null) _outlinePropBlock = new MaterialPropertyBlock();
		_meshRenderer.GetPropertyBlock(_outlinePropBlock);
		if (_info.IsSecret)
		{
			// Restore authored colors instead of retaining the surprise material's tint.
			_outlinePropBlock.SetColor("_BaseColor", material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white);
			_outlinePropBlock.SetColor("_Color", material != null && material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white);
		}
		else
		{
			Color color = GameAssetsConfig.GetCubeColor(_info.Type);
			_outlinePropBlock.SetColor("_BaseColor", color);
			_outlinePropBlock.SetColor("_Color", color);
		}
		_meshRenderer.SetPropertyBlock(_outlinePropBlock);
	}

	private void RefreshSquareSprite()
	{
		if (_square == null || _info == null) return;
		_square.color = Color.white;
		_square.sprite = _info.IsSecret ? GameAssetsConfig.GetCubeSecretSprite() :
			CubeData.IsSurpriseColor(_info.Type) ? GameAssetsConfig.GetCubeSurpriseSprite() :
			GameAssetsConfig.GetCubeSpriteByType(_info.Type);
		_square.gameObject.SetActive(_square.sprite != null);
	}

	private void OnDestroy()
	{
		CancelSurpriseTransformation();
		if (_info != null && _info.CubeSc == this) _info.CubeSc = null;
	}
}
