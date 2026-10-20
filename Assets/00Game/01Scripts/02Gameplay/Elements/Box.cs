using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;
public class Box : MonoBehaviour
{
	private const float SlicePositionInterval = 0.08f;
	private const float CubeSizeCenterOffset = -0.04f;
	private const float NormalCubeBaseZ = -1.2f;
	private const float LongBoxOffsetZ = -1.6f;
	private const float SingleBoxOffsetY = 0.3f;
	public int OnPushSliceCount;
	public List<Cube> Cubes = new List<Cube>();
	[SerializeField] private GameObject _cubePrefab;
	[SerializeField] private Transform _boxCard;

	private BoxInfo _info;
	private BoxAnimator _animator;
	private GameLoader _gameLoader;
	private AudioSource _audioSource;
	[SerializeField] private Animation _anim;
	[SerializeField] private ColorLock _colorLock;
	[SerializeField] private BoxNumLock _numLock;
	[SerializeField] private AdjacentLock _adjacentLock;
	private bool _isFinishHandled;
	private bool _onUnlockingColorLock;
	private bool _onDecrementingNumLock;
	private int _routineVersion;
	private CancellationToken _routineToken;
	private CancellationTokenSource _surpriseLifetime;
	private UniTask _surpriseTask;
	private readonly List<Cube> _surpriseCubes = new List<Cube>();
	internal bool IsSurpriseTransformationPlaying => _surpriseLifetime != null;
	internal UniTask SurpriseTransformation => _surpriseTask;

	public BoxInfo Info => _info;
	public GameLoader GameLoader => _gameLoader;
	public BoxAnimator Animator => _animator;
	public Transform BoxCard => _boxCard != null ? _boxCard : (_boxCard = HierarchyReferenceCache.Child(transform, "BoxCard"));
	internal int RoutineVersion => _routineVersion;
	internal bool IsCurrentRoutine(int version) => this != null && version == _routineVersion && isActiveAndEnabled;
	public bool IsLockAnimationPlaying
	{
		get
		{
			if (IsSurpriseTransformationPlaying || _onDecrementingNumLock || _onUnlockingColorLock ||
				(_adjacentLock != null && _adjacentLock.IsUnlockAnimationPlaying)) return true;
			for (int i = 0; i < Cubes.Count; i++)
				if (Cubes[i] != null && Cubes[i].IsNumLockAnimationPlaying) return true;
			return false;
		}
	}
	public bool IsFinishAnimationPlaying => _animator != null && _animator.IsFinishAnimationPlaying;

	private void Awake()
	{
		_routineToken = destroyCancellationToken;
		if (Cubes == null) Cubes = new List<Cube>();
		_audioSource = GetComponent<AudioSource>();
		if (_audioSource == null)
		{
			_audioSource = gameObject.AddComponent<AudioSource>();
			_audioSource.playOnAwake = false;
		}
		if (_boxCard == null) _boxCard = HierarchyReferenceCache.Child(transform, "BoxCard");
		_animator = new BoxAnimator(this, _anim, BoxCard, _audioSource);
	}

	public void Init(BoxInfo info, GameLoader gameLoader)
	{
		_routineVersion++;
		_animator?.Reset();
		CancelSurpriseTransformation();
		_info = info;
		_gameLoader = gameLoader;
		_isFinishHandled = false;
		if (_info == null) return;

		transform.position = _info.Pos;
		if (_info.BoxType != BoxType.Single) transform.rotation = Quaternion.Euler(_info.Rotation);
		InitSpecial();
		CreateCube();
	}

	private void InitSpecial()
	{
		if (_info == null) return;
		if (_colorLock == null && _info.LockColor > 0) _colorLock = CreateSpecial<ColorLock>(_gameLoader == null ? null : _gameLoader.BoxLock);
		if (_numLock == null && _info.NumLock > 0) _numLock = CreateSpecial<BoxNumLock>(_gameLoader == null ? null : _gameLoader.NumLock);
		if (_adjacentLock == null && _info.IsAdjacentLocked) _adjacentLock = CreateSpecial<AdjacentLock>(_gameLoader == null ? null : _gameLoader.AdjacentLock);
		if (_colorLock != null)
		{
			_colorLock.gameObject.SetActive(_info.LockColor > 0);
			if (_info.LockColor > 0) _colorLock.Init(_info.LockColor, _info.Rotation, _gameLoader);
		}
		if (_numLock != null)
		{
			_numLock.gameObject.SetActive(_info.NumLock > 0);
			_numLock.Init(_info);
		}
		if (_adjacentLock != null) _adjacentLock.gameObject.SetActive(_info.IsAdjacentLocked);
	}

	private T CreateSpecial<T>(GameObject prefab) where T : Component
	{
		if (prefab == null) return null;
		GameObject instance = Instantiate(prefab, transform);
		instance.transform.localPosition = Vector3.zero;
		instance.transform.localRotation = Quaternion.identity;
		return instance.GetComponent<T>() ?? instance.AddComponent<T>();
	}

	public void CheckHiddenSecret()
	{
		if (_info == null) return;
		_info.ClearCubes();
		CubeInfo cubeInfo = _info.PeekTopCube();
		bool revealed = cubeInfo != null && cubeInfo.IsSecret;
		if (revealed)
		{
			cubeInfo.IsSecret = false;
			if (cubeInfo.CubeSc != null)
			{
				SecretBlockMarker marker = cubeInfo.CubeSc.SecretMarker;
				if (marker != null) marker.Reveal().Forget();
				else cubeInfo.CubeSc.HiddenSecret().Forget();
			}
		}
		_info.IsLastUnlockSecret = revealed;
	}

	public bool CheckSurpriseFinished()
	{
		if (!isActiveAndEnabled || IsSurpriseTransformationPlaying || _info == null || GameData.Current == null) return false;
		_info.ClearCubes();
		if (!_info.IsSurpriseCubeFinished()) return false;

		SurpriseInfo surprise = GameData.Current.GetSurpriseInfo(_info.GetTopType());
		if (surprise == null || surprise.NormalCubes.Count != _info.CubeInfos.Count) return false;
		for (int i = 0; i < surprise.NormalCubes.Count; i++)
			if (surprise.NormalCubes[i] == null) return false;
		var lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
		_surpriseLifetime = lifetime;
		var completion = new UniTaskCompletionSource();
		_surpriseTask = completion.Task;
		Cube.CompleteSurpriseTask(TransformSurpriseAsync(_info, GameData.Current, surprise.NormalCubes, lifetime), completion).Forget();
		return true;
	}

	private async UniTask TransformSurpriseAsync(BoxInfo info, GameData data, IReadOnlyList<CubeInfo> replacements,
		CancellationTokenSource lifetime)
	{
		try
		{
			var tasks = new UniTask[info.CubeInfos.Count];
			for (int i = 0; i < tasks.Length; i++)
			{
				CubeInfo target = info.CubeInfos[i];
				Cube visual = target.CubeSc;
				if (visual != null && visual.isActiveAndEnabled && visual.Info == target)
				{
					_surpriseCubes.Add(visual);
					tasks[i] = visual.UnlockSurpriseCube(replacements[i]);
				}
				else tasks[i] = TransformMissingSurpriseAsync(info, data, target, replacements[i], lifetime.Token);
			}
			await UniTask.WhenAll(tasks);
		}
		catch
		{
			if (_surpriseLifetime == lifetime) CancelSurpriseTransformation();
			throw;
		}
		finally
		{
			if (_surpriseLifetime == lifetime) { _surpriseLifetime = null; _surpriseCubes.Clear(); }
			lifetime.Dispose();
		}
	}

	private async UniTask TransformMissingSurpriseAsync(BoxInfo info, GameData data, CubeInfo target,
		CubeInfo replacement, CancellationToken token)
	{
		await UniTask.WaitForSeconds(0.5f, false, PlayerLoopTiming.Update, token);
		if (this == null || _info != info || GameData.Current != data)
			throw new System.OperationCanceledException(token);
		Cube.CommitSurpriseData(target, data, replacement.Type, replacement.IsSecret, replacement);
		await UniTask.WaitForSeconds(0.15f, false, PlayerLoopTiming.Update, token);
	}

	internal void CancelSurpriseTransformation()
	{
		var lifetime = _surpriseLifetime;
		_surpriseLifetime = null;
		for (int i = 0; i < _surpriseCubes.Count; i++)
		{
			Cube cube = _surpriseCubes[i];
			if (cube != null) cube.CancelSurpriseTransformation();
		}
		_surpriseCubes.Clear();
		lifetime?.Cancel();
	}

	public void PlayCubeAddEffect(Color color, int sliceCount)
	{
		if (_info == null) return;
		Vector3 targetPos = GetBoxTargetPos(sliceCount, new Vector3(0f, 0.9f, -1.65f));
		_animator?.PlayCubeAddEffect(color, targetPos);
	}

	public UniTask PlayAddCubeAnim()
	{
		return _animator != null ? _animator.PlayAddCubeAnim(Cubes, _routineVersion) : default;
	}

	public void PlayShuffleLayout(float duration = 0.6f)
	{
		_animator?.PlayShuffleLayout(Cubes, duration, _routineVersion, CheckHiddenSecret);
	}

	public void RefreshAfterUndo(bool restoredToSource, int restoredType) =>
		RefreshAfterUndo(restoredToSource, restoredType, 0f);

	public void RefreshAfterUndo(bool restoredToSource, int restoredType, float restoreDelay)
	{
		if (_info == null) return;
		for (int i = 0; i < Cubes.Count; i++)
		{
			Cube cube = Cubes[i];
			if (cube == null || cube.Info == null) continue;
			if (!_info.CubeInfos.Contains(cube.Info))
			{
				// Snapshot Undo replaces identities, so stale cubes can still have a positive size.
				Cubes.RemoveAt(i);
				i--;
				cube.PlayDismissAnim();
			}
			else if (cube.Info.Size <= 0)
				cube.PlayDescAnim(0.18f);
			else if (restoredToSource && cube.Info.Type == restoredType)
				cube.PlayAddAnim(restoreDelay, 0.18f);
		}

		foreach (CubeInfo info in _info.CubeInfos)
			if (info != null && info.Size > 0 && info.CubeSc == null)
				CreateCube(info, restoredToSource ? Mathf.Max(0.02f, restoreDelay) : 0f, "");
	}

	public bool CanPopupSlice() => !_isFinishHandled && _info != null && _info.CanPopup();

	public void OnBoxFinished(BoxInfo box)
	{
		if (box == null || _info == null) return;
		RemoveColorLock(box.GetTopType()).Forget();
		DecrementNumLock().Forget();
		DecrementCubeNumLocks();
		RemoveAdjacentLock(box);
		ScheduleNoSolutionLockCheck();
	}

	public void RemoveAdjacentLock(BoxInfo box)
	{
		if (_info == null || !_info.IsAdjacentLocked || !_info.CanUnlockAdjacentBox(box)) return;
		_info.IsAdjacentLocked = false;
		if (_adjacentLock != null) _adjacentLock.UnLock().Forget();
	}

	private UniTaskVoid ScheduleNoSolutionLockCheck()
	{
		CheckNoSolutionLockAfterDelayAsync(_routineVersion).Forget();
		return default;
	}

	private async UniTask CheckNoSolutionLockAfterDelayAsync(int version)
	{
		if (!await WaitForRoutine(0.9f, version)) return;
		CheckNoSolutionRemoveLock();
	}

	public UniTaskVoid CheckNoSolutionRemoveLock()
	{
		if (_info != null && _info.IsAdjacentLocked && _adjacentLock != null)
			CheckNoSolutionRemoveLockAsync(_routineVersion).Forget();
		return default;
	}

	private async UniTask CheckNoSolutionRemoveLockAsync(int version)
	{
		GameData data = GameData.Current;
		if (data == null) return;
		// WaitUntil checked after Update, including when all boxes were already free.
		do
		{
			if (await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, _routineToken).SuppressCancellationThrow()) return;
			if (!IsCurrentRoutine(version) || GameData.Current != data) return;
		} while (!AreBoxesFree(data));
		if (_info == null || !_info.IsAdjacentLocked) return;
		if (!data.IsNoSolution() && !data.ShouldForceUnlockAdjacentBox(_info)) return;
		_info.IsAdjacentLocked = false;
		if (_adjacentLock != null) _adjacentLock.UnLock().Forget();
	}

	private static bool AreBoxesFree(GameData data)
	{
		for (int i = 0; i < data.Boxes.Count; i++)
			if (data.Boxes[i] == null || data.Boxes[i].State != BoxState.Free) return false;
		return true;
	}

	internal async UniTask<bool> WaitForRoutine(float duration, int version)
	{
		bool canceled = await UniTask.WaitForSeconds(duration, cancellationToken: _routineToken).SuppressCancellationThrow();
		return !canceled && IsCurrentRoutine(version);
	}

	private async UniTaskVoid DecrementNumLock()
	{
		if (_info == null || _info.NumLock <= 0 || _onDecrementingNumLock) return;
		if (_numLock == null)
		{
			if (!_info.HasBlockingLockExceptNumLock()) _info.NumLock--;
			return;
		}
		BoxInfo info = _info;
		BoxNumLock numLock = _numLock;
		BoxState previousState = info.State;
		_onDecrementingNumLock = true;
		info.State = BoxState.OnUnlocking;
		try
		{
			await numLock.DecNumLock();
			if (this == null || _info != info || _numLock != numLock) return;
			if (info.NumLock <= 0) _numLock = null;
		}
		finally
		{
			_onDecrementingNumLock = false;
			if (this != null && _info == info && !_onUnlockingColorLock && info.State == BoxState.OnUnlocking)
				info.State = previousState == BoxState.OnPush && OnPushSliceCount == 0
					? BoxState.Free : previousState;
		}
	}

	private void DecrementCubeNumLocks()
	{
		foreach (Cube cube in Cubes)
			if (cube != null && cube.Info != null && cube.Info.NumLock > 0) cube.DecrementNumLock().Forget();
	}

	private async UniTask RemoveColorLock(int type)
	{
		if (_info == null || _info.LockColor <= 0 || _info.LockColor != type ||
			_colorLock == null || _onUnlockingColorLock) return;

		BoxInfo info = _info;
		ColorLock colorLock = _colorLock;
		BoxState previousState = info.State;
		_onUnlockingColorLock = true;
		info.State = BoxState.OnUnlocking;
		try
		{
			await colorLock.UnLock();
			if (this == null || _info != info || _colorLock != colorLock) return;
			_colorLock = null;
			info.LockColor = 0;
			info.State = BoxState.Free;
		}
		finally
		{
			_onUnlockingColorLock = false;
			if (this != null && _info == info && info.State == BoxState.OnUnlocking)
				info.State = previousState == BoxState.OnPush && OnPushSliceCount == 0
					? BoxState.Free : previousState;
		}
	}

	public void PlayClickedAnim(bool isPopup)
	{
		_animator?.PlayClickedAnim(isPopup, Cubes);
	}

	public bool CanJumpType(int type)
	{
		if (_info == null || (_info.State != BoxState.Free && _info.State != BoxState.OnPush)) return false;
		GameData data = GameData.Current;
		if (data != null && data.LimitedColor.Contains(type) && _info.LimitedColor != type) return false;
		return _info.CanJumpSlice(type);
	}

	public Cube ReserveIncomingSlice(int type)
	{
		return ReserveIncomingSlice(type, out _);
	}

	public Cube ReserveIncomingSlice(int type, out bool mergedWithTopCube)
	{
		mergedWithTopCube = false;
		if (_info == null) return null;
		CubeInfo info = _info.PushSlice(type);
		mergedWithTopCube = info != null;
		if (info == null) info = _info.AddCube(type);
		if (info == null) return null;
		bool newCube = info.CubeSc == null;
		if (newCube) CreateCube(info);
		if (info.CubeSc == null) return null;
		info.CubeSc.ReserveIncomingSlice(newCube);
		_animator?.SetFinishPresentationTime(_info.IsFinished() ? Time.time + BoxFinishRules.FinishDelay : (float?)null);
		_animator?.PlayPopInAudio();
		return info.CubeSc;
	}
	public void CompleteIncomingSlice(Cube cube)
	{
		if (cube == null || cube.Info == null) return;
		cube.CompleteIncomingSlice();
		RemoveColorLock(cube.Info.Type).Forget();
	}
	public bool PushSlice(int type, float interval)
	{
		if (_info == null) return false;
		CubeInfo cubeInfo = _info.PushSlice(type);
		if (cubeInfo == null) return false;
		_animator?.PlayPopInAudio();
		if (cubeInfo.CubeSc == null) CreateCube(cubeInfo);
		if (cubeInfo.CubeSc != null) cubeInfo.CubeSc.PlayAddAnim(interval, 0.12f);
		RemoveColorLock(type).Forget();
		HandleFinishIfReady();
		return true;
	}

	public bool PopupSlice()
	{
		if (_info == null) return false;
		CubeInfo cubeInfo = _info.PopupSlice();
		if (cubeInfo == null) return false;
		PlayLinkedSliceEmitted(cubeInfo);
		return true;
	}

	internal void PlayLinkedSliceEmitted(CubeInfo cubeInfo)
	{
		_animator?.PlayJumpAudio();
		if (cubeInfo.CubeSc != null) cubeInfo.CubeSc.PlayDescAnim();
	}

	public void PlayPopInAudio() => _animator?.PlayPopInAudio();
	public void PlayJumpAudio() => _animator?.PlayJumpAudio();

	public void RemoveEmptyCube()
	{
		for (int i = Cubes.Count - 1; i >= 0; i--)
		{
			Cube cube = Cubes[i];
			if (cube == null || cube.Info == null || cube.Info.Size <= 0)
			{
				Cubes.RemoveAt(i);
				if (cube != null) cube.CheckEmpty();
			}
		}
		_info?.ClearCubes();
	}

	public void HandleFinishIfReady()
	{
		if (_isFinishHandled || IsSurpriseTransformationPlaying || _info == null || OnPushSliceCount > 0 || !_info.IsFinished()) return;
		_isFinishHandled = true;
		float interval = GameController.Current == null ? float.PositiveInfinity :
			GameController.Current.GetLastFinishBoxInterval();
		if (GameController.Current != null) GameController.Current.HandleBoxFinished(this);
		GameData data = GameData.Current;
		int combo = data == null ? 0 : (interval <= GameData.BoxEmojiComboTime ? data.BoxEmojiCombo : data.BoxCombo);
		int finished = data == null ? 1 : data.FinishBoxCount;
		_animator?.PlayFullAnim(interval, combo, finished, _routineVersion).Forget();
	}

	private void OnDisable()
	{
		_routineVersion++;
		_animator?.OnDisable();
		CancelSurpriseTransformation();
	}

	internal Vector3 GetFinishCubePos()
	{
		float zOffset = _info != null && _info.BoxType == BoxType.Long ? -0.8f : 0f;
		return transform.TransformPoint(new Vector3(0f, 0.8f, zOffset));
	}

	public Vector3 GetTopSlicePos()
	{
		if (_info == null) return transform.position;
		int index = _info.GetSliceCount();
		if (_info.BoxType == BoxType.Single)
		{
			CubeInfo top = _info.GetTopCube();
			index = top == null ? 0 : top.Size % GameData.SliceCountPerGroup;
		}
		return GetSlicePos(index);
	}

	public Vector3 GetCubePos(CubeInfo info, bool isTop = false, int visibleSize = -1)
	{
		if (_info == null || info == null) return transform.position;
		float sizeOffset = (GameData.SliceCountPerGroup - (visibleSize < 0 ? info.Size : visibleSize)) * CubeSizeCenterOffset;
		if (_info.BoxType == BoxType.Single)
			return transform.TransformPoint(new Vector3(0f, 1.12f, sizeOffset));

		int precedingSlices = 0;
		int index = _info.CubeInfos.IndexOf(info);
		if (index < 0) index = _info.CubeInfos.Count;
		for (int i = 0; i < index; i++) precedingSlices += _info.CubeInfos[i].Size;
		return GetBoxTargetPos(precedingSlices,
			new Vector3(0f, 0.8f, NormalCubeBaseZ + sizeOffset));
	}

	private Vector3 GetSlicePos(int index)
	{
		Vector3 pos = GetBoxTargetPos(index, new Vector3(0f, 0.81f, -1.65f));
		if (_info != null && _info.BoxType == BoxType.Single)
			pos += transform.TransformVector(new Vector3(0f, 0f, 0.2f));
		return pos;
	}

	private Vector3 GetBoxTargetPos(int index, Vector3 offset)
	{
		if (_info != null && _info.BoxType == BoxType.Long) offset.z += LongBoxOffsetZ;
		else if (_info != null && _info.BoxType == BoxType.Single)
		{
			offset.y += SingleBoxOffsetY;
			offset.z = -0.4f;
		}
		offset.z += index * SlicePositionInterval;
		return transform.TransformPoint(offset);
	}

	private UniTask CreateCube()
	{
		Cubes.RemoveAll(cube => cube == null);
		if (_info == null) return default;
		foreach (CubeInfo cubeInfo in _info.CubeInfos)
		{
			if (cubeInfo.CubeSc != null)
			{
				if (!Cubes.Contains(cubeInfo.CubeSc)) Cubes.Add(cubeInfo.CubeSc);
				continue;
			}
			CreateCube(cubeInfo);
		}
		return default;
	}

	private UniTask CreateCube(CubeInfo info, float delay = 0f, string playAnim = "")
	{
		if (info == null) return default;
		GameObject cubePrefab = _cubePrefab;
		GameObject instance = Instantiate(cubePrefab, transform);
		if (instance.transform.parent == null) instance.transform.SetParent(transform, false);
		instance.SetActive(true);
		Cube cube = instance.GetComponent<Cube>();
		if (cube == null) cube = instance.AddComponent<Cube>();
		cube.Init(info, this, _gameLoader, playAnim);
		Cubes.Add(cube);
		if (delay > 0f) cube.PlayAddAnim(delay);
		return default;
	}
}
