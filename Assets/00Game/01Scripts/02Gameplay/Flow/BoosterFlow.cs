using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using static UnityEngine.Object;

public sealed class BoosterFlow
{
	private readonly MonoBehaviour _host;
	private readonly IReadOnlyList<Box> _boxes;
	private readonly Func<BoxInfo, Box> _getBox;
	private readonly Func<bool> _isGameRunning;
	private readonly Func<bool> _isGameplaySettled;
	private readonly Action _refreshPathState;
	private readonly Action _onFinished;
	private readonly Action<AudioClip> _playAudio;
	private GameData _data;
	private CoreGameplayEngine _engine;
	private GameLoader _loader;
	private Transform _gameRoot;
	private InventoryManager _inventory;
	private Coroutine _completion;
	private int _operationVersion;
	private Sequence _undoPresentation;
	private readonly List<GameObject> _undoVisuals = new List<GameObject>();
	private bool _cachedCanShuffle;
	private float _nextShuffleCheckTime;
	private InventoryManager Inventory => _inventory != null ? _inventory : (_inventory = InventoryManager.Ensure());

	public bool IsBusy { get; private set; }
	public bool IsSelectingShuffle { get; private set; }
	public bool CanRequestUndo => CanUseBooster(false) && Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_UNDO) &&
		(!Inventory.CanUseBooster(InventoryKeys.TOOL_UNDO) || (_isGameplaySettled() && _data.CanUndo()));
	public bool CanRequestShuffle => CanUseBooster(false) && Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_SHUFFLE) &&
		(!Inventory.CanUseBooster(InventoryKeys.TOOL_SHUFFLE) || (_isGameplaySettled() && HasAnyShuffleableBox()));
	public bool CanRequestPath => CanUseBooster(false) && _engine != null && Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_PATH);
	public bool CanUseUndo => CanUseBooster(true) && _data.CanUndo() &&
		Inventory.CanUseBooster(InventoryKeys.TOOL_UNDO);
	public bool CanUseShuffle => CanUseBooster(true) && HasAnyShuffleableBox() &&
		Inventory.CanUseBooster(InventoryKeys.TOOL_SHUFFLE);

	private bool HasAnyShuffleableBox()
	{
		for (int i = 0; i < _boxes.Count; i++)
		{
			if (IsShuffleTarget(_boxes[i]))
				return true;
		}
		return false;
	}
	public bool CanUsePath => CanRequestPath &&
		Inventory.CanUseBooster(InventoryKeys.TOOL_PATH);

	public UndoUnavailableReason GetUndoUnavailableReason()
	{
		if (!Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_UNDO))
		{
			return UndoUnavailableReason.Locked;
		}

		if (_data == null || !_data.CanUndo())
		{
			return UndoUnavailableReason.NoMoves;
		}

		return UndoUnavailableReason.None;
	}

	public void GetRequestState(out bool undo, out bool shuffle, out bool path)
	{
		undo = shuffle = path = false;
		if (!CanUseBooster(false)) return;
		path = _engine != null && Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_PATH);
		// HUD queries share one settled-state scan; action properties remain live.
		bool settled = _isGameplaySettled();
		undo = Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_UNDO) &&
			(!Inventory.CanUseBooster(InventoryKeys.TOOL_UNDO) || (settled && _data.CanUndo()));
		bool canShuffle = false;
		if (settled)
		{
			float now = Time.unscaledTime;
			if (now >= _nextShuffleCheckTime)
			{
				_cachedCanShuffle = HasAnyShuffleableBox();
				_nextShuffleCheckTime = now + 0.15f;
			}
			canShuffle = _cachedCanShuffle;
		}
		shuffle = Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_SHUFFLE) &&
			(!Inventory.CanUseBooster(InventoryKeys.TOOL_SHUFFLE) || canShuffle);
	}

	public BoosterFlow(MonoBehaviour host, IReadOnlyList<Box> boxes, Func<BoxInfo, Box> getBox,
		Func<bool> isGameRunning, Func<bool> isGameplaySettled, Action refreshPathState,
		Action onFinished, Action<AudioClip> playAudio)
	{
		_host = host;
		_boxes = boxes;
		_getBox = getBox;
		_isGameRunning = isGameRunning;
		_isGameplaySettled = isGameplaySettled;
		_refreshPathState = refreshPathState;
		_onFinished = onFinished;
		_playAudio = playAudio;
	}

	public void Reset(GameData data, CoreGameplayEngine engine, GameLoader loader, Transform gameRoot)
	{
		Cancel();
		_data = data;
		_engine = engine;
		_loader = loader;
		_gameRoot = gameRoot;
	}

	public void Cancel()
	{
		CancelShuffleSelection();
		_operationVersion++;
		if (_completion != null && _host != null) _host.StopCoroutine(_completion);
		_completion = null;
		ClearUndoPresentation();
		IsBusy = false;
		_nextShuffleCheckTime = 0f;
	}

	public bool TryUsePath() => Inventory.TryUseBooster(InventoryKeys.TOOL_PATH, ApplyToolPath);
	public bool TryUseUndo() => Inventory.TryUseBooster(InventoryKeys.TOOL_UNDO, ApplyToolUndo);
	public bool TryUseShuffle()
	{
		if (!CanUseShuffle) return false;
		IsSelectingShuffle = true;
		return true;
	}

	public void CancelShuffleSelection()
	{
		IsSelectingShuffle = false;
	}

	public bool TryUseShuffle(Box box)
	{
		if (!CanSelectShuffleBox(box)) return false;
		// Selection itself never spends inventory. Commit only the chosen box.
		CancelShuffleSelection();
		return Inventory.TryUseBooster(InventoryKeys.TOOL_SHUFFLE, () => ApplyToolShuffle(box));
	}

	public bool CanSelectShuffleBox(Box box)
	{
		return IsSelectingShuffle && CanUseBooster(true, true) &&
			Inventory.CanUseBooster(InventoryKeys.TOOL_SHUFFLE) &&
			_boxes.Contains(box) && IsShuffleTarget(box);
	}

	private bool IsShuffleTarget(Box box)
	{
		return box != null && box.isActiveAndEnabled && box.Info != null &&
			box.Info.State == BoxState.Free && _data != null &&
			_data.Boxes.Contains(box.Info) && box.Info.CanShuffle();
	}

	private bool ApplyToolPath()
	{
		if (!CanUseBooster(false) || _engine == null) return false;
		_engine.UsePathBooster();
		_playAudio(_loader == null ? null : _loader.PathClip);
		_refreshPathState();
		return true;
	}

	private bool ApplyToolUndo()
	{
		if (!CanUseBooster(true) || _engine == null || !_data.CanUndo()) return false;
		if (_data.UndoData.IsLinked) return ApplyLinkedUndo();

		UndoData undo = _data.UndoData;
		Box source = _getBox(undo.PopupBox);
		var destinations = new List<Box>();
		var seenDestinations = new HashSet<Box>();
		for (int i = 0; i < undo.BoxData.Count; i++)
		{
			UndoBox item = undo.BoxData[i];
			if (item == null || item.Box == null) continue;
			Box box = _getBox(item.Box);
			if (box != null && seenDestinations.Add(box))
				destinations.Add(box);
		}
		var roadVisuals = new List<Slice>();
		for (int i = 0; i < undo.Slices.Count; i++)
		{
			SliceInfo slice = undo.Slices[i];
			if (slice != null && _data.TransferSlice.Contains(slice) && slice.SliceSc != null)
				roadVisuals.Add(slice.SliceSc);
		}
		int initialSourceSliceCount = source != null ? source.GetCurrentSliceVisualCount() : 0;
		int restoredType = undo.Type;
		// Capture visual origins before the engine clears undo history and restores the data.
		var returnOrigins = new List<(Vector3 position, Quaternion rotation, Vector3 scale)>();
		foreach (Slice slice in roadVisuals)
			returnOrigins.Add((slice.transform.position, slice.transform.rotation, slice.transform.localScale));
		foreach (UndoBox item in undo.BoxData)
		{
			Box destination = item == null || item.Box == null ? null : _getBox(item.Box);
			if (destination == null || item.Size <= 0) continue;
			for (int i = 0; i < item.Size; i++)
				returnOrigins.Add((destination.GetUndoDepartureSlicePos(i),
					destination.transform.rotation, Vector3.one));
		}

		IsBusy = true;
		if (!_engine.UndoLastMove())
		{
			IsBusy = false;
			return false;
		}

		foreach (Slice slice in roadVisuals)
			if (slice != null) slice.DismissForUndo();
		if (source != null && _loader != null && _loader.Slice != null && returnOrigins.Count > 0)
		{
			_undoPresentation = DOTween.Sequence().SetLink(_host.gameObject);
			for (int i = 0; i < returnOrigins.Count; i++)
			{
				var origin = returnOrigins[i];
				GameObject visual = Instantiate(_loader.Slice, origin.position, origin.rotation, _gameRoot);
				_undoVisuals.Add(visual);
				visual.transform.localScale = origin.scale;
				Slice slice = visual.GetComponent<Slice>();
				if (slice != null) slice.PrepareVisual(restoredType);
				visual.SetActive(true);
				float delay = returnOrigins.Count <= 1 ? 0f : 0.08f * i / (returnOrigins.Count - 1);
				Vector3 targetPos = source.GetUndoLandingSlicePos(initialSourceSliceCount + i);
				_undoPresentation.Insert(delay, visual.transform.DOJump(targetPos, 0.8f, 1, 0.32f).SetEase(Ease.OutQuad));
				_undoPresentation.Insert(delay, visual.transform.DORotateQuaternion(source.transform.rotation, 0.32f));
				_undoPresentation.Insert(delay, visual.transform.DOScale(Vector3.one, 0.32f));
			}
			_undoPresentation.OnComplete(ClearUndoPresentation);
		}
		foreach (Box destination in destinations) destination.RefreshAfterUndo(false, restoredType);
		if (source != null) source.RefreshAfterUndo(true, restoredType, _undoPresentation != null ? 0.4f : 0f);

		var affected = new List<BoxInfo>();
		for (int i = 0; i < destinations.Count; i++)
		{
			if (destinations[i].Info != null)
				affected.Add(destinations[i].Info);
		}
		if (source != null && source.Info != null) affected.Add(source.Info);
		foreach (BoxInfo info in affected) info.State = BoxState.OnUnlocking;
		_playAudio(_loader == null ? null : _loader.UndoClip);
		_refreshPathState();
		_completion = _host.StartCoroutine(FinishBoosterUse(0.6f, affected));
		return true;
	}

	private bool ApplyLinkedUndo()
	{
		if (!_engine.TryPrepareLinkedUndo(out LinkedCubeUndoPlan plan)) return false;
		var origins = new List<(Vector3 position, Quaternion rotation, Vector3 scale, int color, Box source, Vector3 targetPosition)>();
		var destOffsets = new Dictionary<Box, int>();
		var initialSourceCounts = new Dictionary<Box, int>();
		var sourceOffsets = new Dictionary<Box, int>();
		var roadVisuals = new List<Slice>();
		foreach (LinkedCubeUndoEntry entry in plan.Entries)
		{
			Box source = _getBox(entry.Source);
			Box destination = entry.Destination == null ? null : _getBox(entry.Destination);
			if (source == null || entry.Destination != null && destination == null) return false;
			if (!initialSourceCounts.ContainsKey(source))
			{
				initialSourceCounts[source] = source.GetCurrentSliceVisualCount();
				sourceOffsets[source] = 0;
			}
			int landingIndex = initialSourceCounts[source] + sourceOffsets[source]++;
			Vector3 landingPos = source.GetUndoLandingSlicePos(landingIndex);
			if (entry.Destination == null)
			{
				Slice slice = entry.Slice.SliceSc;
				if (slice == null) continue;
				origins.Add((slice.transform.position, slice.transform.rotation, slice.transform.localScale, entry.Color, source, landingPos));
				roadVisuals.Add(slice);
			}
			else
			{
				if (!destOffsets.TryGetValue(destination, out int destOffset))
					destOffset = 0;
				destOffsets[destination] = destOffset + 1;
				Vector3 departurePos = destination.GetUndoDepartureSlicePos(destOffset);
				origins.Add((departurePos, destination.transform.rotation, Vector3.one, entry.Color, source, landingPos));
			}
		}
		if (!_engine.TryUndoLinked(plan)) return false;
		IsBusy = true;
		foreach (Slice slice in roadVisuals) if (slice != null) slice.DismissForUndo();
		if (_loader != null && _loader.Slice != null && origins.Count > 0)
		{
			_undoPresentation = DOTween.Sequence().SetLink(_host.gameObject);
			for (int i = 0; i < origins.Count; i++)
			{
				var origin = origins[i];
				GameObject visual = Instantiate(_loader.Slice, origin.position, origin.rotation, _gameRoot);
				_undoVisuals.Add(visual);
				visual.transform.localScale = origin.scale;
				Slice slice = visual.GetComponent<Slice>();
				if (slice != null) slice.PrepareVisual(origin.color);
				visual.SetActive(true);
				float delay = origins.Count <= 1 ? 0f : .08f * i / (origins.Count - 1);
				_undoPresentation.Insert(delay, visual.transform.DOJump(origin.targetPosition, .8f, 1, .32f).SetEase(Ease.OutQuad));
				_undoPresentation.Insert(delay, visual.transform.DORotateQuaternion(origin.source.transform.rotation, .32f));
				_undoPresentation.Insert(delay, visual.transform.DOScale(Vector3.one, .32f));
			}
			_undoPresentation.OnComplete(ClearUndoPresentation);
		}
		var affected = new List<BoxInfo>();
		float restoreDelay = _undoPresentation != null ? 0.4f : 0f;
		foreach (BoxInfo info in plan.Boxes)
		{
			Box box = _getBox(info);
			bool isSource = plan.Pair != null && plan.Pair.Contains(info);
			int restoredType = isSource ? (info == plan.Pair.FirstBox ? plan.Pair.FirstCube.Type : plan.Pair.SecondCube.Type) : 0;
			if (box != null) box.RefreshAfterUndo(isSource, restoredType, isSource ? restoreDelay : 0f);
			info.State = BoxState.OnUnlocking;
			affected.Add(info);
		}
		_playAudio(_loader == null ? null : _loader.UndoClip);
		_refreshPathState();
		_completion = _host.StartCoroutine(FinishBoosterUse(.6f, affected));
		return true;
	}

	private void ClearUndoPresentation()
	{
		_undoPresentation?.Kill();
		_undoPresentation = null;
		foreach (GameObject visual in _undoVisuals)
			if (visual != null) UnityEngine.Object.Destroy(visual);
		_undoVisuals.Clear();
	}

	private bool ApplyToolShuffle(Box box)
	{
		if (!CanUseBooster(true) || _engine == null) return false;

		IsBusy = true;
		int shuffledCount = _engine.UseShuffleBooster(box.Info);
		if (shuffledCount <= 0)
		{
			IsBusy = false;
			return false;
		}

		box.Info.State = BoxState.OnUnlocking;
		box.PlayShuffleLayout(0.6f);
		_playAudio(_loader == null ? null : _loader.ShuffleClip);
		_completion = _host.StartCoroutine(FinishBoosterUse(0.65f,
			new List<BoxInfo> { box.Info }));
		return true;
	}

	private bool CanUseBooster(bool requireSettled, bool allowShuffleSelection = false)
	{
		return Time.timeScale > 0f && _isGameRunning() && !IsBusy &&
			(!IsSelectingShuffle || allowShuffleSelection) && _data != null &&
			_data.ActiveLinkedLaunch == null &&
			(_data.LinkedPair == null || _isGameplaySettled()) &&
			(!requireSettled || _isGameplaySettled());
	}

	private IEnumerator FinishBoosterUse(float delay, List<BoxInfo> boxes)
	{
		int operationVersion = _operationVersion;
		yield return new WaitForSeconds(delay);
		if (operationVersion != _operationVersion) yield break;
		foreach (BoxInfo box in boxes)
			if (box != null && box.State == BoxState.OnUnlocking) box.State = BoxState.Free;
		_completion = null;
		IsBusy = false;
		_nextShuffleCheckTime = 0f;
		_onFinished();
	}
}
