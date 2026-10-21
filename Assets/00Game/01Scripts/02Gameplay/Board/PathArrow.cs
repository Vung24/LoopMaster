using UnityEngine;

public class PathArrow : MonoBehaviour
{
	private const float MinDirectionMagnitude = 0.00001f;
	private const float PathHeightOffset = -0.45f;
	private int _pathIndex;
	private readonly RoadMotion _roadMotion = new RoadMotion(false);

	public void Init(int index)
	{
		_roadMotion.Reset(transform.position, transform.rotation);
		_pathIndex = index;
		MoveNextPath(0.01f);
	}

	public void MoveNextPath(float interval)
	{
		if (GameData.Current == null || GameData.Current.PathPoints.Count == 0) return;

		int next = (_pathIndex + 1) % GameData.Current.PathPoints.Count;
		_pathIndex = next;
		if (!GameData.Current.IsPathCircle &&
			GameData.Current.IsPathSectionIndex(_pathIndex))
		{
			_roadMotion.Reset(transform.position, transform.rotation);
			transform.position = GetPosByPathIndex(_pathIndex);
			return;
		}

		if (!_roadMotion.IsMoving)
			_roadMotion.Reset(transform.position, transform.rotation);
		Vector3 startPosition = transform.position;
		Quaternion rotation = transform.rotation;
		if (_roadMotion.IsMoving)
		{
			startPosition = _roadMotion.TailPosition;
			rotation = _roadMotion.TailRotation;
		}
		Vector3 targetPosition = GetPosByPathIndex(next);
		Vector3 direction = targetPosition - startPosition;
		if (direction.magnitude > MinDirectionMagnitude)
			rotation = Quaternion.LookRotation(direction, Vector3.up);
		_roadMotion.Enqueue(targetPosition, rotation, Mathf.Max(0.000001f, interval));
	}

	internal void AdvanceRoadVisual(float delta)
	{
		if (!isActiveAndEnabled || !_roadMotion.Advance(delta)) return;
		transform.SetPositionAndRotation(_roadMotion.Position, _roadMotion.Rotation);
	}

	private void OnDisable()
	{
		_roadMotion.Reset(transform.position, transform.rotation);
	}

	private Vector3 GetPosByPathIndex(int index)
	{
		if (GameData.Current == null) return transform.position;
		return GameData.Current.GetPosByPathIndex(index) +
			new Vector3(0f, PathHeightOffset, 0f);
	}

}
