using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class BouncyLavaDetailDialog : Snui.Lava.LavaDetailDialog
{
	[SerializeField] private int[] _fallPlayerSides = { 0, 0, 1, 1, 1, 1, 0, 0 };
	private readonly List<int> _fallIndexes = new List<int>();
	private bool _fallIndexesCreated;
	public override async UniTask PlayPlayersJumpAnimation()
	{
		int nextIndex = GetNextPlaneIndex();
		if (nextIndex < _planes.Count && _planes[nextIndex] != null)
		{
			Transform topNode = _planes[nextIndex].GetPlaneNode() ?? _planes[nextIndex].transform;
			Vector3 targetBase = _playerNode != null ? _playerNode.InverseTransformPoint(topNode.position) : topNode.localPosition;

			List<int> fallIndexes = GetFallPlayerIndexes();

			for (int i = 0; i < _players.Count; i++)
			{
				BouncyLavaPlayer bouncyPlayer = GetBouncyPlayer(i);
				if (bouncyPlayer != null && bouncyPlayer.gameObject.activeSelf)
				{
					Vector3 avatarOffset = i > 0 && Managers.Lava != null ? Managers.Lava.GetAvatarPosition(i - 1) : Vector3.zero;
					Vector3 targetPos = targetBase + avatarOffset;

					if (fallIndexes.Contains(i) || (_failed && i == 0))
					{
						bool isRightSide = avatarOffset.x >= 0f;
						float splashDelay = GetWaterSplashDelay(nextIndex);
						bouncyPlayer.DOJumpToFail(isRightSide, targetPos, 0.45f, 80f, 0.25f, splashDelay);
					}
					else
					{
						bouncyPlayer.DOJumpTo(targetPos, 0.45f, 80f, 0.25f);
					}
				}
			}

			int presentationMs = Mathf.CeilToInt((0.45f + GetWaterSplashDelay(nextIndex) + 0.25f) * 1000f) + 50;
			await UniTask.Delay(presentationMs, cancellationToken: this.GetCancellationTokenOnDestroy());
			foreach (int index in fallIndexes)
				if (_players[index] != null) _players[index].gameObject.SetActive(false);
			if (_failed && _userPlayer != null) _userPlayer.gameObject.SetActive(false);
			_planes[nextIndex].Hit();
		}
	}

	public List<int> GetFallPlayerIndexes()
	{
		if (_fallIndexesCreated) return _fallIndexes;
		_fallIndexesCreated = true;
		int count = _failed ? 2 : Mathf.Min(2, _losePlayers);
		for (int i = 1; i < _players.Count; i++)
		{
			if (_players[i] != null && _players[i].gameObject.activeSelf)
			{
				_fallIndexes.Add(i);
			}
		}
		int next = GetNextPlaneIndex();
		bool endpoint = next == _planes.Count - 1;
		bool leftmost = GetFallPlayerSide(next);
		_fallIndexes.Sort((a, b) =>
		{
			float ax = endpoint ? _players[a].transform.localPosition.x : Managers.Lava.GetAvatarPosition(a - 1).x;
			float bx = endpoint ? _players[b].transform.localPosition.x : Managers.Lava.GetAvatarPosition(b - 1).x;
			return leftmost ? ax.CompareTo(bx) : bx.CompareTo(ax);
		});
		if (_fallIndexes.Count > count) _fallIndexes.RemoveRange(count, _fallIndexes.Count - count);
		return _fallIndexes;
	}

	public bool GetFallPlayerSide(int index)
	{
		return index >= 0 && index < _fallPlayerSides.Length && _fallPlayerSides[index] != 0;
	}

	public float GetWaterSplashDelay(int index)
	{
		return index < 5 ? 0.65f : 0.75f;
	}

	public override async UniTask PlayFallAnimation()
	{
		if (_jumpPending || _failed)
		{
			int currPlane = GetCurrPlaneIndex();
			if (currPlane < _planes.Count && _planes[currPlane] != null)
			{
				_planes[currPlane].Fall();
			}
			await UniTask.Delay(600, cancellationToken: this.GetCancellationTokenOnDestroy());
			if (currPlane > 0 && currPlane < _planes.Count - 1 && _planes[currPlane] != null)
				_planes[currPlane].gameObject.SetActive(false);
		}
	}

	public BouncyLavaPlayer GetBouncyPlayer(int index)
	{
		if (index >= 0 && index < _players.Count)
		{
			return _players[index] as BouncyLavaPlayer;
		}
		return null;
	}
}
