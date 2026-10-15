using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;

public sealed class SliceTransferFlow
{
	private sealed class LaunchOperation
	{
		public int Version;
		public bool FirstVibration = true;
	}

	private readonly MonoBehaviour _host;
	private readonly CancellationToken _hostToken;
	private readonly IReadOnlyList<Box> _boxes;
	private readonly Func<bool> _isGameRunning;
	private readonly Func<Box, bool> _isGroupUnlocking;
	private readonly Action _updateCombo;
	private readonly Action _refreshPathState;
	private readonly Action _onLaunchFinished;
	private readonly Action _checkGameOver;
	private readonly Action<bool> _setSpeedUp;
	private readonly List<LaunchOperation> _launches = new List<LaunchOperation>();
	private readonly List<Box> _arrivalCandidates = new List<Box>();
	private readonly Dictionary<int, List<Box>> _boxesByPath = new Dictionary<int, List<Box>>();
	private readonly List<List<Box>> _pathBuckets = new List<List<Box>>();
	private bool _arrivalBatchActive;
	private readonly HashSet<Box> _surprises = new HashSet<Box>();
	private GameData _data;
	private GameLoader _loader;
	private Transform _gameRoot;
	private int _version;
	private float _popInVibrationEndTime;
	private float _popOutVibrationEndTime;
	private bool _isPlayingPopInVibration;
	private bool _isPlayingPopOutVibration;
	private CancellationTokenSource _vibrationLifetime;
	private int _vibrationVersion;

	public SliceTransferFlow(MonoBehaviour host, IReadOnlyList<Box> boxes, Func<bool> isGameRunning,
		Func<Box, bool> isGroupUnlocking, Action updateCombo,
		Action refreshPathState, Action onLaunchFinished, Action checkGameOver, Action<bool> setSpeedUp)
	{
		_host = host;
		_hostToken = host.destroyCancellationToken;
		_boxes = boxes;
		_isGameRunning = isGameRunning;
		_isGroupUnlocking = isGroupUnlocking;
		_updateCombo = updateCombo;
		_refreshPathState = refreshPathState;
		_onLaunchFinished = onLaunchFinished;
		_checkGameOver = checkGameOver;
		_setSpeedUp = setSpeedUp;
	}

	public void Reset(GameData data, GameLoader loader, Transform gameRoot)
	{
		Cancel();
		_data = data;
		_loader = loader;
		_gameRoot = gameRoot;
	}

	public void Cancel()
	{
		StopVibrationWindows();
		EndArrivalBatch();
		_version++;
		foreach (Box box in _surprises.ToArray()) if (box != null) box.CancelSurpriseTransformation();
		_surprises.Clear();
		_launches.Clear();
		_arrivalCandidates.Clear();
	}

	public void StopVibrationWindows()
	{
		_vibrationVersion++;
		if (_vibrationLifetime != null)
		{
			_vibrationLifetime.Cancel();
			_vibrationLifetime.Dispose();
			_vibrationLifetime = null;
		}
		_isPlayingPopInVibration = _isPlayingPopOutVibration = false;
		_popInVibrationEndTime = _popOutVibrationEndTime = 0f;
		GameVibrationHelper.Cancel();
	}

	private bool IsCurrent(int version) => version == _version && _host != null && _host.gameObject.activeInHierarchy && _isGameRunning();

	public void BeginArrivalBatch()
	{
		EndArrivalBatch();
		int bucketCount = 0;
		for (int i = 0; i < _boxes.Count; i++)
		{
			Box box = _boxes[i];
			if (box == null || box.Info == null) continue;
			int path = box.Info.PathIndex;
			if (!_boxesByPath.TryGetValue(path, out List<Box> bucket))
			{
				if (bucketCount == _pathBuckets.Count) _pathBuckets.Add(new List<Box>());
				bucket = _pathBuckets[bucketCount++];
				_boxesByPath.Add(path, bucket);
			}
			bucket.Add(box);
		}
		_arrivalBatchActive = true;
	}

	public void EndArrivalBatch()
	{
		_arrivalBatchActive = false;
		_boxesByPath.Clear();
		for (int i = 0; i < _pathBuckets.Count; i++) _pathBuckets[i].Clear();
	}

	public bool CheckInBox(Slice slice, float jumpDuration, float jumpHeight)
	{
		if (_data == null || slice == null || slice.Info == null) return false;
		_arrivalCandidates.Clear();
		try
		{
			IReadOnlyList<Box> boxes = _boxes;
			if (_arrivalBatchActive)
			{
				if (!_boxesByPath.TryGetValue(slice.Info.PathIndex, out List<Box> bucket)) return false;
				boxes = bucket;
			}
			for (int i = 0; i < boxes.Count; i++)
			{
				Box box = boxes[i];
				if (IsSliceCanJump(slice.Info, box)) _arrivalCandidates.Add(box);
			}

			bool skipSource = _arrivalCandidates.Count > 1 && slice.Info.SourceBox != null;
			for (int i = 0; i < _arrivalCandidates.Count; i++)
			{
				Box box = _arrivalCandidates[i];
				if ((skipSource && box.Info == slice.Info.SourceBox) || _isGroupUnlocking(box)) continue;
				JumpSliceToBox(slice, box, jumpDuration, jumpHeight);
				return true;
			}

			return false;
		}
		finally { _arrivalCandidates.Clear(); }
	}

	private bool IsSliceCanJump(SliceInfo slice, Box box)
	{
		return slice != null && box != null && box.Info != null &&
			box.Info.PathIndex == slice.PathIndex && slice.IsMoved &&
			box.CanJumpType(slice.Type) &&
			(_data == null || !_data.IsIgnoreBox(slice.Type, box.Info));
	}

	private void JumpSliceToBox(Slice slice, Box box, float jumpDuration, float jumpHeight)
	{
		if (slice == null || slice.Info == null || box == null || box.Info == null ||
			_isGroupUnlocking(box) || !box.CanJumpType(slice.Info.Type)) return;

		_updateCombo();
		box.Info.State = BoxState.OnPush;
		box.OnPushSliceCount++;
		_data.UndoData.UpdateUndoBox(slice.Info, box.Info);
		Cube destination = box.ReserveIncomingSlice(slice.Info.Type, out bool mergedWithTopCube);
		if (destination == null)
		{
			box.OnPushSliceCount = Mathf.Max(0, box.OnPushSliceCount - 1);
			if (box.OnPushSliceCount == 0 && box.Info.State == BoxState.OnPush)
				box.Info.State = BoxState.Free;
			return;
		}
		ExtendVibrationWindow(false);
		_data.RemoveTransferSlice(slice.Info);
		int effectSliceCount = box.Info.GetSliceCount();
		Color effectColor = GameAssetsConfig.GetCubeColor(slice.Info.Type);
		int version = _version;
		slice.JumpToBox(box, jumpDuration, () =>
		{
			if (version != _version || _data == null || box == null || box.Info == null) return;
			if (mergedWithTopCube) box.PlayCubeAddEffect(effectColor, effectSliceCount);
			FinishJumpSliceToBox(box, destination, version);
		}, jumpHeight);
		return;
	}

	private void FinishJumpSliceToBox(Box box, Cube destination, int version)
	{
		if (version != _version || _data == null || box == null || box.Info == null) return;
		box.CompleteIncomingSlice(destination);
		box.PlayAddCubeAnim().Forget();
		box.OnPushSliceCount = Mathf.Max(0, box.OnPushSliceCount - 1);
		if (box.OnPushSliceCount == 0)
		{
			if (box.Info.State == BoxState.OnPush) box.Info.State = BoxState.Free;
			if (box.CheckSurpriseFinished()) HandleSurpriseBoxFinished(box);
			box.TryHandleFinish();
		}
		_checkGameOver();
	}

	private void HandleSurpriseBoxFinished(Box box)
	{
		if (box == null || box.Info == null || _data == null) return;
		if (!_surprises.Add(box)) return;
		WatchSurpriseBox(box, box.Info, _data, _version, box.SurpriseTransformation).Forget();
		_data.UndoData.Clear();
		for (int i = 0; i < _boxes.Count; i++)
			if (_boxes[i] != null) _boxes[i].OnBoxFinished(box.Info);
	}

	private async UniTask WatchSurpriseBox(Box box, BoxInfo info, GameData data, int version, UniTask completion)
	{
		try
		{
			await completion;
			if (version != _version || _data != data || box == null || !box.isActiveAndEnabled || box.Info != info) return;
			box.TryHandleFinish();
			_checkGameOver();
		}
		catch (OperationCanceledException) { }
		finally { if (version == _version) _surprises.Remove(box); }
	}

	public void Launch(Box box, float jumpDuration, float jumpHeight)
	{
		if (box == null || box.Info == null || box.Info.State != BoxState.Free || !box.CanPopupSlice()) return;
		if (!_isGameRunning() || box == null || box.Info == null || _data == null || !_data.CanCubeJumpToTransfer())
		{
			if (box != null) box.PlayClickedAnim(false);
			return;
		}

		List<CubeInfo> topCubes = box.Info.GetTopCubes(false);
		if (topCubes.Count == 0)
		{
			box.PlayClickedAnim(false);
			return;
		}

		box.Info.State = BoxState.OnPopup;
		_data.OnJumpingCube += topCubes.Count;
		box.PlayClickedAnim(true);
		var operation = new LaunchOperation { Version = _version };
		_launches.Add(operation);
		JumpSliceToTransferAsync(box, topCubes, jumpDuration, jumpHeight, operation).Forget();
		return;
	}

	private async UniTask JumpSliceToTransferAsync(Box box, List<CubeInfo> topCubes, float jumpDuration, float jumpHeight, LaunchOperation operation)
	{
		try
		{
			await UniTask.WaitForSeconds(GameplayFeedbackRules.SliceLaunchStartDelay, delayTiming: PlayerLoopTiming.PreLateUpdate, cancellationToken: _hostToken);
			if (!IsCurrent(operation.Version) || box == null || box.Info == null) return;
			var created = new List<SliceInfo>();
			float nextLaunchTime = Time.time;
			for (int cubeIndex = 0; cubeIndex < topCubes.Count; cubeIndex++)
			{
				CubeInfo cube = topCubes[cubeIndex];
				if (cube == null) continue;
				if (!IsCurrent(operation.Version)) return;
				if (!_data.IsTransferHaveSpace()) break;
				int sliceCount = cube.Size;
				for (int sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
				{
					if (!IsCurrent(operation.Version)) return;
					if (_data == null) break;
					var info = new SliceInfo(cube.Type, box.Info);
					if (!_data.AddTransferSlice(info)) continue;
					if (!box.PopupSlice())
					{
						_data.RemoveTransferSlice(info);
						break;
					}

					Slice visual = CreateSlice(info, box);
					ExtendVibrationWindow(true, operation.FirstVibration);
					operation.FirstVibration = false;
					if (visual != null) visual.JumpToTransfer(box.Info.PathIndex, jumpDuration, jumpHeight).Forget();
					created.Add(info);
					_refreshPathState();
					// Keep the release cadence across frames; a per-slice wait rounds
					// every interval up to a frame and spreads the airborne train out.
					nextLaunchTime += GameplayFeedbackRules.SliceLaunchInterval;
					while (Time.time < nextLaunchTime)
					{
						if (!IsCurrent(operation.Version)) return;
						await UniTask.NextFrame(PlayerLoopTiming.PreLateUpdate, _hostToken);
					}
				}
			}

			await UniTask.WaitForSeconds(0.3f, delayTiming: PlayerLoopTiming.PreLateUpdate, cancellationToken: _hostToken);
			if (!IsCurrent(operation.Version) || box == null || box.Info == null) return;
			box.RemoveEmptyCube();
			box.CheckHiddenSecret();

			while (HasAirborneSlices(created))
			{
				if (!IsCurrent(operation.Version)) return;
				await UniTask.NextFrame(PlayerLoopTiming.PreLateUpdate, _hostToken);
			}

			if (!IsCurrent(operation.Version)) return;
			if (_data != null)
			{
				_data.OnJumpingCube = Mathf.Max(0, _data.OnJumpingCube - topCubes.Count);
				CheckNearOver();
				if (box != null && box.Info != null && box.Info.BoxType == BoxType.Single)
					_data.UndoData.Clear();
				else if (box != null && box.Info != null)
				{
					_data.UndoData.Init(box.Info);
					_data.UndoData.AddUndo(created);
				}
			}

			if (!IsCurrent(operation.Version) || box == null || box.Info == null) return;
			box.Info.State = BoxState.Free;
			_launches.Remove(operation);
			_onLaunchFinished();
		}
		catch (OperationCanceledException) { }
		finally { _launches.Remove(operation); }
	}

	private bool HasAirborneSlices(List<SliceInfo> slices)
	{
		if (_data == null) return false;
		for (int i = 0; i < slices.Count; i++)
		{
			SliceInfo info = slices[i];
			if (info != null && info.SliceSc != null && info.PathIndex < 0 && _data.TransferSlice.Contains(info))
				return true;
		}
		return false;
	}

	private void ExtendVibrationWindow(bool popOut, bool first = false)
	{
		// Native ExtendSlicePop*VibrationWindow: extend by 0.3s, one loop per direction.
		if (_vibrationLifetime == null)
			_vibrationLifetime = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_hostToken);
		if (popOut)
		{
			if (first) GameVibrationHelper.PlaySlicePopOutFirst();
			_popOutVibrationEndTime = Mathf.Max(_popOutVibrationEndTime, Time.time + 0.3f);
			if (!_isPlayingPopOutVibration)
			{
				_isPlayingPopOutVibration = true;
				PlayVibrationWindow(true, _vibrationVersion, _vibrationLifetime.Token).Forget();
			}
		}
		else
		{
			_popInVibrationEndTime = Mathf.Max(_popInVibrationEndTime, Time.time + 0.3f);
			if (!_isPlayingPopInVibration)
			{
				_isPlayingPopInVibration = true;
				PlayVibrationWindow(false, _vibrationVersion, _vibrationLifetime.Token).Forget();
			}
		}
	}

	private async UniTask PlayVibrationWindow(bool popOut, int version, System.Threading.CancellationToken token)
	{
		try
		{
			// Native uses UniTask at Update (enum 8), not coroutine WaitForSeconds.
			if (popOut) await UniTask.WaitForSeconds(0.025f, delayTiming: PlayerLoopTiming.Update, cancellationToken: token);
			while (version == _vibrationVersion && _host != null && _host.isActiveAndEnabled &&
				Time.time < (popOut ? _popOutVibrationEndTime : _popInVibrationEndTime))
			{
				if (popOut) GameVibrationHelper.PlaySlicePopOut();
				else GameVibrationHelper.PlaySlicePopIn();
				await UniTask.WaitForSeconds(0.025f, delayTiming: PlayerLoopTiming.Update, cancellationToken: token);
			}
		}
		catch (OperationCanceledException) { }
		finally
		{
			if (version == _vibrationVersion)
			{
				if (popOut) _isPlayingPopOutVibration = false;
				else _isPlayingPopInVibration = false;
			}
		}
	}

	private Slice CreateSlice(SliceInfo info, Box box)
	{
		Slice slice = _loader.GetSlice(_gameRoot);
		if (slice == null) return null;
		Vector3 position = box == null ? Slice.GetRoadPosition(info.PathIndex) : box.GetTopSlicePos();
		Quaternion rotation = box == null ? Quaternion.identity : box.transform.rotation;
		slice.Init(info, position, rotation);
		return slice;
	}

	private void CheckNearOver()
	{
		if (_data == null || !_data.IsGameCanSpeedUp()) return;
		_data.IsNearOver = true;
		// Native CheckNearOver (RVA 0x15CF6A4) also enables the controller's speed flag.
		_setSpeedUp(true);
	}
}
