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

	private sealed class LinkedFlight
	{
		public SliceInfo Info;
		public int TargetPath;
		public float Duration;
		public float Elapsed;
		public bool Landed;
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
	private CancellationTokenSource _linkedLifetime;

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
		_linkedLifetime?.Cancel();
		_linkedLifetime?.Dispose();
		_linkedLifetime = null;
		try { _data?.ActiveLinkedLaunch?.Cancel(); }
		catch (InvalidOperationException error) { Debug.LogError("Linked launch cleanup requires board reload: " + error); }
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
		_data.UndoData.CaptureLinkedExpected(box.Info);
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
		void OnSliceJumpCompleted()
		{
			if (version != _version || _data == null || box == null || box.Info == null) return;
			if (mergedWithTopCube) box.PlayCubeAddEffect(effectColor, effectSliceCount);
			FinishJumpSliceToBox(box, destination, version);
		}

		slice.JumpToBox(box, jumpDuration, OnSliceJumpCompleted, jumpHeight);
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
			box.HandleFinishIfReady();
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
			box.HandleFinishIfReady();
			_checkGameOver();
		}
		catch (OperationCanceledException) { }
		finally { if (version == _version) _surprises.Remove(box); }
	}

	public void Launch(Box box, float jumpDuration, float jumpHeight)
	{
		if (_data != null && _data.ActiveLinkedLaunch != null) return;
		if (box != null && box.Info != null && box.Info.PeekTopCube() != null && box.Info.PeekTopCube().LinkedPairId != 0) return;
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

	public bool LaunchLinked(Box source, float jumpDuration, float jumpHeight, out LinkedCubeLaunchFailure failure)
	{
		failure = LinkedCubeLaunchFailure.Busy;
		if (!_isGameRunning() || _host == null || !_host.isActiveAndEnabled || _data == null || _launches.Count != 0)
			return false;
		LinkedCubePair pair = source == null || source.Info == null ? null : source.Info.LinkedPair;
		if (pair == null) { failure = LinkedCubeLaunchFailure.InvalidState; return false; }
		Box first = GetBox(pair.FirstBox), second = GetBox(pair.SecondBox);
		if (first == null || second == null || pair.FirstCube == null || pair.SecondCube == null ||
			pair.FirstCube.CubeSc == null || pair.SecondCube.CubeSc == null)
		{
			failure = LinkedCubeLaunchFailure.InvalidState;
			return false;
		}
		if (!first.CanPopupSlice() || !second.CanPopupSlice() ||
			_isGroupUnlocking(first) || _isGroupUnlocking(second)) return false;
		if (!LinkedCubeLaunch.Begin(_data, source.Info, out LinkedCubeLaunch launch, out failure)) return false;
		first.PlayClickedAnim(true);
		second.PlayClickedAnim(true);
		_linkedLifetime = CancellationTokenSource.CreateLinkedTokenSource(_hostToken);
		RunLinkedLaunchAsync(launch, first, second, jumpDuration, jumpHeight, _version, _linkedLifetime.Token).Forget();
		return true;
	}

	private Box GetBox(BoxInfo info)
	{
		for (int i = 0; i < _boxes.Count; i++)
			if (_boxes[i] != null && _boxes[i].Info == info) return _boxes[i];
		return null;
	}

	private async UniTask RunLinkedLaunchAsync(LinkedCubeLaunch launch, Box first, Box second,
		float jumpDuration, float jumpHeight, int version, CancellationToken token)
	{
		Exception failure = null;
		var flights = new List<LinkedFlight>();
		try
		{
			await UniTask.WaitForSeconds(GameplayFeedbackRules.SliceLaunchStartDelay,
				delayTiming: PlayerLoopTiming.PreLateUpdate, cancellationToken: token);
			float untilEmission = 0f;
			while (launch.IsEmitting || !launch.CanComplete)
			{
				token.ThrowIfCancellationRequested();
				if (!IsCurrent(version)) throw new OperationCanceledException(token);
				if (first == null || second == null) throw new InvalidOperationException("Linked cube source was destroyed during launch.");
				if (launch.IsEmitting && untilEmission <= 0f && Time.timeScale > 0f)
				{
					EmitLinkedSlice(launch, first, false, jumpDuration, jumpHeight, flights);
					EmitLinkedSlice(launch, second, true, jumpDuration, jumpHeight, flights);
					_refreshPathState();
					untilEmission = GameplayFeedbackRules.SliceLaunchInterval;
				}
				await UniTask.NextFrame(PlayerLoopTiming.PreLateUpdate, token);
				if (!IsCurrent(version)) throw new OperationCanceledException(token);
				float delta = Time.deltaTime;
				untilEmission -= delta;
				UpdateLinkedFlights(launch, flights, delta);
			}
			launch.Complete();
			CheckNearOver();
			_onLaunchFinished();
		}
		catch (OperationCanceledException) { }
		catch (Exception error) { failure = error; }
		finally
		{
			try { if (launch.IsActive) launch.Cancel(); }
			catch (InvalidOperationException cleanupError)
			{
				if (failure == null) failure = cleanupError;
				else failure = new AggregateException(failure, cleanupError);
			}
			if (version == _version)
			{
				_linkedLifetime?.Dispose();
				_linkedLifetime = null;
			}
		}
		if (failure != null && version == _version) Debug.LogException(failure);
	}

	private void EmitLinkedSlice(LinkedCubeLaunch launch, Box box, bool second, float duration, float height,
		List<LinkedFlight> flights)
	{
		if (box == null) throw new InvalidOperationException("Linked cube source was destroyed.");
		CubeInfo member = second ? launch.Pair.SecondCube : launch.Pair.FirstCube;
		Vector3 origin = box.GetTopSlicePos();
		SliceInfo info = launch.Emit(second);
		if (info == null) return;
		Slice visual = _loader.GetSlice(_gameRoot);
		if (visual == null) throw new InvalidOperationException("Cannot allocate linked slice visual.");
		visual.Init(info, origin, box.transform.rotation);
		box.PlayLinkedSliceEmitted(member);
		box.RemoveEmptyCube();
		if (member.Size == 0)
		{
			box.CheckHiddenSecret();
			launch.Data.UndoData.CaptureLinkedExpected(box.Info);
		}
		ExtendVibrationWindow(true, launch.Created.Count <= 2);
		flights.Add(new LinkedFlight { Info = info, TargetPath = box.Info.PathIndex, Duration = Mathf.Max(.01f, duration) });
		visual.JumpToTransfer(box.Info.PathIndex, duration, height, commitLogicOnComplete: false).Forget();
	}

	private void UpdateLinkedFlights(LinkedCubeLaunch launch, List<LinkedFlight> flights, float delta)
	{
		float scale = GameController.Current == null ? 1f : GameController.Current.SliceJumpTimeScale;
		foreach (LinkedFlight flight in flights)
		{
			if (flight.Landed) continue;
			SliceInfo info = flight.Info;
			if (info.SliceSc == null || !launch.Data.TransferSlice.Contains(info))
				throw new InvalidOperationException("Linked pair " + launch.Pair.Id + " lost a slice before logical landing.");
			flight.Elapsed += delta * scale;
			if (flight.Elapsed < flight.Duration) continue;
			int count = launch.Data.PathPoints.Count;
			if (count < 2) throw new InvalidOperationException("Linked flight has no valid path.");
			info.PathIndex = ((flight.TargetPath % count) + count) % count;
			info.IsMoved = false;
			flight.Landed = true;
			launch.ConfirmLanding(info);
		}
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
					nextLaunchTime += GameplayFeedbackRules.SliceLaunchInterval;
					while (Time.time < nextLaunchTime)
					{
						if (!IsCurrent(operation.Version)) return;
						await UniTask.NextFrame(PlayerLoopTiming.PreLateUpdate, _hostToken);
					}
				}
			}

			if (!IsCurrent(operation.Version) || box == null || box.Info == null) return;
			box.RemoveEmptyCube();
			box.CheckHiddenSecret();

			// Reveal the exposed secret while slices are airborne; retain the source/Undo guard.
			await UniTask.WaitForSeconds(0.3f, delayTiming: PlayerLoopTiming.PreLateUpdate, cancellationToken: _hostToken);
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
		if (_vibrationLifetime == null)
			_vibrationLifetime = CancellationTokenSource.CreateLinkedTokenSource(_hostToken);
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

	private async UniTask PlayVibrationWindow(bool popOut, int version, CancellationToken token)
	{
		try
		{
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
		_setSpeedUp(true);
	}
}
