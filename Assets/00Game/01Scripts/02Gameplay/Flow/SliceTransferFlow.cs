using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class SliceTransferFlow
{
	private sealed class LaunchOperation
	{
		public Coroutine Routine;
		public int Version;
	}

	private readonly MonoBehaviour _host;
	private readonly IReadOnlyList<Box> _boxes;
	private readonly Func<bool> _isGameRunning;
	private readonly Func<int> _getLaunchTick;
	private readonly Func<Box, bool> _isGroupUnlocking;
	private readonly Action _updateCombo;
	private readonly Action _refreshPathState;
	private readonly Action _onLaunchFinished;
	private readonly Action _checkGameOver;
	private readonly List<LaunchOperation> _launches = new List<LaunchOperation>();
	private readonly HashSet<Box> _surprises = new HashSet<Box>();
	private GameData _data;
	private GameLoader _loader;
	private Transform _gameRoot;
	private int _version;

	public SliceTransferFlow(MonoBehaviour host, IReadOnlyList<Box> boxes, Func<bool> isGameRunning,
		Func<int> getLaunchTick, Func<Box, bool> isGroupUnlocking, Action updateCombo,
		Action refreshPathState, Action onLaunchFinished, Action checkGameOver)
	{
		_host = host;
		_boxes = boxes;
		_isGameRunning = isGameRunning;
		_getLaunchTick = getLaunchTick;
		_isGroupUnlocking = isGroupUnlocking;
		_updateCombo = updateCombo;
		_refreshPathState = refreshPathState;
		_onLaunchFinished = onLaunchFinished;
		_checkGameOver = checkGameOver;
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
		_version++;
		foreach (Box box in _surprises.ToArray()) if (box != null) box.CancelSurpriseTransformation();
		_surprises.Clear();
		foreach (LaunchOperation operation in _launches.ToArray())
			if (operation.Routine != null && _host != null) _host.StopCoroutine(operation.Routine);
		_launches.Clear();
	}

	private bool IsCurrent(int version) => version == _version && _isGameRunning();

	public bool CheckInBox(Slice slice, float jumpDuration, float jumpHeight)
	{
		if (_data == null || slice == null || slice.Info == null) return false;
		var candidates = new List<Box>();
		for (int i = 0; i < _boxes.Count; i++)
		{
			Box box = _boxes[i];
			if (IsSliceCanJump(slice.Info, box)) candidates.Add(box);
		}

		if (candidates.Count > 1 && slice.Info.SourceBox != null)
			candidates.RemoveAll(box => box != null && box.Info == slice.Info.SourceBox);

		for (int i = 0; i < candidates.Count; i++)
		{
			Box box = candidates[i];
			if (_isGroupUnlocking(box) ||
				(_data != null && _data.IsIgnoreBox(slice.Info.Type, box.Info))) continue;
			JumpSliceToBox(slice, box, jumpDuration, jumpHeight);
			return true;
		}
		return false;
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
		_data.RemoveTransferSlice(slice.Info);
		// Snapshot each reserved slot so overlapping arrivals burst at their own target.
		int effectSliceCount = box.Info.GetSliceCount();
		Color effectColor = GameAssetsConfig.GetCubeColor(slice.Info.Type);
		int version = _version;
		slice.JumpToBox(box, jumpDuration, () =>
		{
			if (version != _version || _data == null || box == null || box.Info == null) return;
			// Match impact rather than a fixed delay, including speed changes mid-flight.
			if (mergedWithTopCube) box.PlayCubeAddEffect(effectColor, effectSliceCount);
			FinishJumpSliceToBox(box, destination, version);
		}, jumpHeight);
		return;
	}

	private void FinishJumpSliceToBox(Box box, Cube destination, int version)
	{
		if (version != _version || _data == null || box == null || box.Info == null) return;
		box.CompleteIncomingSlice(destination);
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
		operation.Routine = _host.StartCoroutine(JumpSliceToTransferRoutine(
			box, topCubes, jumpDuration, jumpHeight, operation));
		return;
	}

	private IEnumerator JumpSliceToTransferRoutine(Box box, List<CubeInfo> topCubes, float jumpDuration, float jumpHeight, LaunchOperation operation)
	{
		try
		{
			var created = new List<SliceInfo>();
			for (int cubeIndex = 0; cubeIndex < topCubes.Count; cubeIndex++)
			{
				CubeInfo cube = topCubes[cubeIndex];
				if (cube == null) continue;
				if (!IsCurrent(operation.Version)) yield break;
				if (!_data.IsTransferHaveSpace()) break;
				int sliceCount = cube.Size;
				for (int sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
				{
					yield return WaitForNextConveyorTick();
					if (!IsCurrent(operation.Version)) yield break;
					if (_data == null) break;
					var info = new SliceInfo(cube.Type, box.Info);
					if (!_data.AddTransferSlice(info)) continue;
					if (!box.PopupSlice())
					{
						_data.RemoveTransferSlice(info);
						break;
					}

					Slice visual = CreateSlice(info, box);
					if (visual != null) visual.JumpToTransfer(box.Info.PathIndex, jumpDuration, jumpHeight);
					created.Add(info);
					_refreshPathState();
				}
			}

			// Native starts this delay after launching the batch, without awaiting its flights.
			// Reveal the exposed secret while slices are still in flight; keep Undo and
			// the source's input guard pending until every launched slice has landed.
			yield return new WaitForSeconds(0.3f);
			if (!IsCurrent(operation.Version) || box == null || box.Info == null) yield break;
			box.RemoveEmptyCube();
			box.CheckHiddenSecret();

			while (created.Any(info => info != null && info.SliceSc != null && info.PathIndex < 0 && _data != null && _data.TransferSlice.Contains(info)))
			{
				if (!IsCurrent(operation.Version)) yield break;
				yield return null;
			}

			if (!IsCurrent(operation.Version)) yield break;
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

			if (!IsCurrent(operation.Version) || box == null || box.Info == null) yield break;
			box.Info.State = BoxState.Free;
			_launches.Remove(operation);
			_onLaunchFinished();
		}
		finally { _launches.Remove(operation); }
	}

	private IEnumerator WaitForNextConveyorTick()
	{
		int version = _version;
		int launchTick = _getLaunchTick();
		while (IsCurrent(version) && _getLaunchTick() == launchTick) yield return null;
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
		_data.IsNearOver = true; // Keep the player's selected x2/x4 speed.
	}
}
