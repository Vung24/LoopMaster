using Cysharp.Threading.Tasks;
using UnityEngine;

public class ColorLock : MonoBehaviour
{
	private const float UnlockEffectDelay = 0.5f;
	private const float UnlockEffectDuration = 1.55f;
	[SerializeField] private MeshRenderer _topMeshRenderer;
	[SerializeField] private MeshRenderer _hand1MeshRenderer;
	[SerializeField] private MeshRenderer _hand2MeshRenderer;
	[SerializeField] private Animation _anim;
	[SerializeField] private Transform _tick;
	[SerializeField] private Transform _effect;
	private bool _onPlayingUnlockAnim;
	private GameLoader _gameLoader;
	private float _rotationY;

	private void Awake()
	{
		if (_topMeshRenderer == null)
		{
			Transform top = HierarchyReferenceCache.Path(transform, "ColorLock", "Point", "ColorLockTop");
			if (top != null) _topMeshRenderer = top.GetComponent<MeshRenderer>();
		}
	}

	public void Init(int type, Vector3 rotation, GameLoader gameLoader)
	{
		_gameLoader = gameLoader;
		_rotationY = rotation.y;
		Material material = GameAssetsConfig.GetLockMatByType(type);
		ApplyMaterial(_topMeshRenderer, material);
		ApplyMaterial(_hand1MeshRenderer, material);
		ApplyMaterial(_hand2MeshRenderer, material);
		if (_tick != null)
		{
			Vector3 angles = _tick.rotation.eulerAngles;
			_tick.rotation = Quaternion.Euler(angles.x, angles.y, _rotationY);
		}
	}

	public UniTask UnLock()
	{
		if (_onPlayingUnlockAnim) return default;
		return UnlockRoutine();
	}

	private async UniTask UnlockRoutine()
	{
		_onPlayingUnlockAnim = true;
		if (GameData.Current != null) GameData.Current.ColorLockClearCount++;
		string clip = GetUnlockAnimName();
		if (_anim != null && _anim.GetClip(clip) != null) _anim.Play(clip);

		var token = this.GetCancellationTokenOnDestroy();
		await UniTask.WaitForSeconds(UnlockEffectDelay, false, PlayerLoopTiming.Update, token);
		if (_effect != null) _effect.gameObject.SetActive(true);
		if (_gameLoader != null && GameAudioPlayer.Current != null)
			GameAudioPlayer.Current.PlayColorLockOpen(_gameLoader.ColorLockOpen);
		await UniTask.WaitForSeconds(UnlockEffectDuration, false, PlayerLoopTiming.Update, token);
		Destroy(gameObject);
	}

	private string GetUnlockAnimName()
	{
		return _rotationY > 0f && _rotationY < 180f
			? "ColorBoxUnlockLeft" : "ColorBoxUnlockRight";
	}

	private static void ApplyMaterial(Renderer renderer, Material material)
	{
		if (renderer != null && material != null) renderer.sharedMaterial = material;
	}
}
