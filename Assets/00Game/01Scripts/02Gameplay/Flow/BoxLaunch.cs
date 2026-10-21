using UnityEngine;

public sealed class BoxLaunch
{
	private readonly System.Func<bool> _isGameplayActive;
	private readonly System.Func<bool> _isSelectingShuffle;
	private readonly System.Action<Box> _tryUseShuffle;
	private readonly System.Action _hideShuffleSelection;
	private readonly System.Func<GameData> _getData;
	private readonly System.Func<LinkedCubeCable> _getLinkedCable;
	private readonly System.Func<bool> _isGameplaySettled;
	private readonly System.Func<SliceTransferFlow> _getTransfers;
	private readonly System.Func<BoxInfo, Box> _getBox;
	private readonly System.Func<float> _getJumpDuration;
	private readonly System.Func<float> _getJumpHeight;
	private readonly System.Action _notifyBoxClicked;
	private readonly System.Action<string> _showToast;

	public BoxLaunch(
		System.Func<bool> isGameplayActive,
		System.Func<bool> isSelectingShuffle,
		System.Action<Box> tryUseShuffle,
		System.Action hideShuffleSelection,
		System.Func<GameData> getData,
		System.Func<LinkedCubeCable> getLinkedCable,
		System.Func<bool> isGameplaySettled,
		System.Func<SliceTransferFlow> getTransfers,
		System.Func<BoxInfo, Box> getBox,
		System.Func<float> getJumpDuration,
		System.Func<float> getJumpHeight,
		System.Action notifyBoxClicked,
		System.Action<string> showToast = null)
	{
		_isGameplayActive = isGameplayActive;
		_isSelectingShuffle = isSelectingShuffle;
		_tryUseShuffle = tryUseShuffle;
		_hideShuffleSelection = hideShuffleSelection;
		_getData = getData;
		_getLinkedCable = getLinkedCable;
		_isGameplaySettled = isGameplaySettled;
		_getTransfers = getTransfers;
		_getBox = getBox;
		_getJumpDuration = getJumpDuration;
		_getJumpHeight = getJumpHeight;
		_notifyBoxClicked = notifyBoxClicked;
		_showToast = showToast ?? DefaultShowToast;
	}

	private static void DefaultShowToast(string message)
	{
		GameplayToast.Show(message);
	}

	public void OnClickBox(Box box)
	{
		if (_isGameplayActive == null || !_isGameplayActive()) return;
		if (box == null || box.Info == null) return;

		if (_isSelectingShuffle != null && _isSelectingShuffle())
		{
			_tryUseShuffle?.Invoke(box);
			if (!_isSelectingShuffle()) _hideShuffleSelection?.Invoke();
			return;
		}

		GameData data = _getData?.Invoke();
		if (data == null) return;

		if (data.ActiveLinkedLaunch != null)
		{
			RejectBoxClick(box, LinkedCubeLaunchFailure.Busy, "Please wait for cubes to enter!");
			return;
		}

		if (box.Info.State != BoxState.Free)
		{
			RejectBoxClick(box, LinkedCubeLaunchFailure.Busy, "Please wait for cubes to enter!");
			return;
		}

		CubeInfo top = box.Info.PeekTopCube();
		if (top != null && top.LinkedPairId != 0)
		{
			HandleLinkedBoxClick(box, data);
			return;
		}

		if (!box.CanPopupSlice())
		{
			box.PlayClickedAnim(false);
			string message = !data.IsTransferHaveSpace() ? "Road is full!" : "Box is blocked!";
			_showToast?.Invoke(message);
			return;
		}

		SliceTransferFlow transfers = _getTransfers?.Invoke();
		if (transfers != null)
		{
			float duration = _getJumpDuration?.Invoke() ?? GameplayFeedbackRules.SliceJumpDuration;
			float height = _getJumpHeight?.Invoke() ?? GameplayFeedbackRules.SliceJumpHeight;
			transfers.Launch(box, duration, height);
		}

		_notifyBoxClicked?.Invoke();
	}

	private void HandleLinkedBoxClick(Box box, GameData data)
	{
		if (_isGameplaySettled != null && !_isGameplaySettled())
		{
			RejectBoxClick(box, LinkedCubeLaunchFailure.Busy, "Please wait for cubes to enter!");
			return;
		}

		SliceTransferFlow transfers = _getTransfers?.Invoke();
		if (transfers == null) return;

		float duration = _getJumpDuration?.Invoke() ?? GameplayFeedbackRules.SliceJumpDuration;
		float height = _getJumpHeight?.Invoke() ?? GameplayFeedbackRules.SliceJumpHeight;

		if (!transfers.TryLaunchLinked(box, duration, height, out LinkedCubeLaunchFailure failure))
		{
			box.PlayClickedAnim(false);
			LinkedCubeCable cable = _getLinkedCable?.Invoke();
			if (cable != null) cable.ShowBlocked(failure);

			LinkedCubePair pair = data.LinkedPair;
			if (pair != null && _getBox != null)
			{
				Box partner = _getBox(box.Info == pair.FirstBox ? pair.SecondBox : pair.FirstBox);
				if (partner != null) partner.PlayClickedAnim(false);
			}
		}

		_notifyBoxClicked?.Invoke();
	}

	private void RejectBoxClick(Box box, LinkedCubeLaunchFailure failure, string toastMessage)
	{
		box.PlayClickedAnim(false);
		LinkedCubeCable cable = _getLinkedCable?.Invoke();
		if (cable != null) cable.ShowBlocked(failure);
		_showToast?.Invoke(toastMessage);
	}
}

