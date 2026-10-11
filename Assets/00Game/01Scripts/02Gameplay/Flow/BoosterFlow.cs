using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
	private InventoryManager Inventory => _inventory != null ? _inventory : (_inventory = InventoryManager.Ensure());

	public bool IsBusy { get; private set; }
	public bool CanRequestUndo => CanUseBooster(true) && _data.CanUndo() && Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_UNDO);
	public bool CanRequestShuffle => CanUseBooster(true) &&
		_boxes.Any(box => box != null && box.Info != null && box.Info.CanShuffle()) && Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_SHUFFLE);
	public bool CanRequestPath => CanUseBooster(false) && _engine != null && Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_PATH);
	public bool CanUseUndo => CanRequestUndo &&
		Inventory.CanUseBooster(InventoryKeys.TOOL_UNDO);
	public bool CanUseShuffle => CanRequestShuffle &&
		Inventory.CanUseBooster(InventoryKeys.TOOL_SHUFFLE);
	public bool CanUsePath => CanRequestPath &&
		Inventory.CanUseBooster(InventoryKeys.TOOL_PATH);

	public void GetRequestState(out bool undo, out bool shuffle, out bool path)
	{
		undo = shuffle = path = false;
		if (!CanUseBooster(false)) return;
		path = _engine != null && Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_PATH);
		// HUD queries share one settled-state scan; action properties remain live.
		if (!_isGameplaySettled()) return;
		undo = _data.CanUndo() && Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_UNDO);
		for (int i = 0; i < _boxes.Count; i++)
		{
			Box box = _boxes[i];
			if (box == null || box.Info == null || !box.Info.CanShuffle()) continue;
			shuffle = Inventory.IsBoosterUnlocked(InventoryKeys.TOOL_SHUFFLE);
			break;
		}
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
		_operationVersion++;
		if (_completion != null && _host != null) _host.StopCoroutine(_completion);
		_completion = null;
		IsBusy = false;
	}

	public bool TryUsePath() => Inventory.TryUseBooster(InventoryKeys.TOOL_PATH, ApplyToolPath);
	public bool TryUseUndo() => Inventory.TryUseBooster(InventoryKeys.TOOL_UNDO, ApplyToolUndo);
	public bool TryUseShuffle() => Inventory.TryUseBooster(InventoryKeys.TOOL_SHUFFLE, ApplyToolShuffle);

	private bool ApplyToolPath()
	{
		if (!CanUseBooster(false) || _engine == null) return false;
		_engine.UsePathBooster();
		_playAudio(_loader == null ? null : _loader.PathClip);
		if (_loader != null && _loader.ToolPathBoxEffect != null)
		{
			GameObject effect = UnityEngine.Object.Instantiate(_loader.ToolPathBoxEffect, _gameRoot);
			effect.SetActive(true);
			UnityEngine.Object.Destroy(effect, 1.5f);
		}
		_refreshPathState();
		return true;
	}

	private bool ApplyToolUndo()
	{
		if (!CanUseBooster(true) || _engine == null || !_data.CanUndo()) return false;

		UndoData undo = _data.UndoData;
		Box source = _getBox(undo.PopupBox);
		var destinations = undo.BoxData
			.Where(item => item != null && item.Box != null)
			.Select(item => _getBox(item.Box))
			.Where(box => box != null).Distinct().ToList();
		var roadVisuals = undo.Slices
			.Where(slice => slice != null && _data.TransferSlice.Contains(slice) && slice.SliceSc != null)
			.Select(slice => slice.SliceSc).ToList();
		int restoredType = undo.Type;

		IsBusy = true;
		if (!_engine.UndoLastMove())
		{
			IsBusy = false;
			return false;
		}

		foreach (Slice slice in roadVisuals)
			if (slice != null) slice.DismissForUndo();
		foreach (Box destination in destinations) destination.RefreshAfterUndo(false, restoredType);
		if (source != null) source.RefreshAfterUndo(true, restoredType);

		var affected = destinations.Select(box => box.Info).Where(info => info != null).ToList();
		if (source != null && source.Info != null) affected.Add(source.Info);
		foreach (BoxInfo info in affected) info.State = BoxState.OnUnlocking;
		_playAudio(_loader == null ? null : _loader.UndoClip);
		_refreshPathState();
		_completion = _host.StartCoroutine(FinishBoosterUse(0.6f, affected));
		return true;
	}

	private bool ApplyToolShuffle()
	{
		if (!CanUseBooster(true) || _engine == null) return false;
		List<Box> shuffleBoxes = _boxes.Where(box =>
			box != null && box.Info != null && box.Info.CanShuffle()).ToList();
		if (shuffleBoxes.Count == 0) return false;

		IsBusy = true;
		int shuffledCount = _engine.UseShuffleBooster();
		if (shuffledCount <= 0)
		{
			IsBusy = false;
			return false;
		}

		foreach (Box box in shuffleBoxes) box.PlayShuffleLayout(0.6f);
		_playAudio(_loader == null ? null : _loader.ShuffleClip);
		_completion = _host.StartCoroutine(FinishBoosterUse(0.65f,
			shuffleBoxes.Select(box => box.Info).Where(info => info != null).ToList()));
		return true;
	}

	private bool CanUseBooster(bool requireSettled)
	{
		return Time.timeScale > 0f && _isGameRunning() && !IsBusy && _data != null &&
			(!requireSettled || _isGameplaySettled());
	}

	private System.Collections.IEnumerator FinishBoosterUse(float delay, List<BoxInfo> boxes)
	{
		int operationVersion = _operationVersion;
		yield return new WaitForSeconds(delay);
		if (operationVersion != _operationVersion) yield break;
		foreach (BoxInfo box in boxes)
			if (box != null && box.State == BoxState.OnUnlocking) box.State = BoxState.Free;
		_completion = null;
		IsBusy = false;
		_onFinished();
	}
}
