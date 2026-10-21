using UnityEngine;

public class PathArrow : MonoBehaviour
{
	private const float MinDirectionMagnitude = 0.00001f;
	private const float PathHeightOffset = -0.45f;
	private int _pathIndex;
	private bool _isMoving;
	private float _moveElapsed;
	private float _moveDuration;
	private Vector3 _moveStartPosition;
	private Vector3 _moveTargetPosition;
	private Quaternion _moveStartRotation;
	private Quaternion _moveTargetRotation;

	public void Init(int index)
	{
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
			_isMoving = false;
			_moveElapsed = 0f;
			transform.position = GetPosByPathIndex(_pathIndex);
			return;
		}

		Vector3 startPos = _isMoving ? _moveTargetPosition : transform.position;
		Quaternion startRot = _isMoving ? _moveTargetRotation : transform.rotation;
		_moveStartPosition = startPos;
		_moveTargetPosition = GetPosByPathIndex(next);
		_moveStartRotation = startRot;
		Vector3 direction = _moveTargetPosition - _moveStartPosition;
		_moveTargetRotation = direction.magnitude <= MinDirectionMagnitude
			? _moveStartRotation
			: Quaternion.LookRotation(direction, Vector3.up);

		if (_isMoving && _moveDuration > 0f)
		{
			_moveElapsed = Mathf.Max(0f, _moveElapsed - _moveDuration);
		}
		else
		{
			_moveElapsed = 0f;
		}

		_moveDuration = Mathf.Max(0.01f, interval);
		_isMoving = true;
	}

	private void Update()
	{
		if (!_isMoving) return;
		float delta = Time.deltaTime;
		if (delta <= 0f) return;
		_moveElapsed += delta;
		float progress = Mathf.Clamp01(_moveElapsed / _moveDuration);
		transform.SetPositionAndRotation(
			Vector3.Lerp(_moveStartPosition, _moveTargetPosition, progress),
			Quaternion.Slerp(_moveStartRotation, _moveTargetRotation, progress));
		if (progress >= 1f && _moveElapsed >= _moveDuration + 0.05f)
		{
			CompleteMove();
		}
	}

	private void CompleteMove()
	{
		transform.SetPositionAndRotation(_moveTargetPosition, _moveTargetRotation);
		_isMoving = false;
		_moveElapsed = 0f;
	}

	private void OnDisable()
	{
		_isMoving = false;
		_moveElapsed = 0f;
	}

	private Vector3 GetPosByPathIndex(int index)
	{
		if (GameData.Current == null) return transform.position;
		return GameData.Current.GetPosByPathIndex(index) +
			new Vector3(0f, PathHeightOffset, 0f);
	}

}
