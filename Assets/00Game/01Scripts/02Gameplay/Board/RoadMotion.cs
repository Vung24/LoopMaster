using System;
using UnityEngine;

/// <summary>Consumes permitted road segments on the conveyor's shared visual timeline.</summary>
internal sealed class RoadMotion
{
	private const float MaxQueuedDuration = 0.2f;
	// Two clocks: at most 2 * ceil((0.5s backlog + 0.2s visual debt) / 0.00512s)
	// requests, plus active/initial segments. Catch-up only shortens existing durations.
	private const int Capacity = 288;
	private readonly Segment[] _segments = new Segment[Capacity];
	private int _head;
	private int _count;
	private float _elapsed;
	private float _lastRequestedDuration;
	private Vector3 _startPosition;
	private Quaternion _startRotation;

	private struct Segment
	{
		public Vector3 Position;
		public Quaternion Rotation;
		public float Duration;
	}

	public RoadMotion(bool easeRotation = false)
	{
	}

	public bool IsMoving => _count > 0;
	public Vector3 Position { get; private set; }
	public Quaternion Rotation { get; private set; }
	public Vector3 TailPosition => _segments[(_head + _count - 1) % Capacity].Position;
	public Quaternion TailRotation => _segments[(_head + _count - 1) % Capacity].Rotation;

	public void Reset(Vector3 position, Quaternion rotation)
	{
		_head = 0;
		_count = 0;
		_elapsed = 0f;
		_lastRequestedDuration = 0f;
		Position = _startPosition = position;
		Rotation = _startRotation = rotation;
	}

	public void Enqueue(Vector3 position, Quaternion rotation, float duration)
	{
		if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
			throw new ArgumentOutOfRangeException(nameof(duration), "Road duration must be finite and positive.");
		if (_count == Capacity)
			throw new InvalidOperationException("Road visual queue exceeded the conveyor timing bound. Check tick intervals and duplicate dispatch.");
		int tail = (_head + _count) % Capacity;
		_segments[tail] = new Segment { Position = position, Rotation = rotation, Duration = duration };
		_count++;
		if (_lastRequestedDuration > 0f && Mathf.Abs(duration - _lastRequestedDuration) > 0.000001f)
			SynchronizeArrival(duration);
		_lastRequestedDuration = duration;
		LimitQueuedDuration();
	}

	public bool Advance(float delta)
	{
		if (delta <= 0f || _count == 0) return false;
		while (_count > 0)
		{
			Segment current = _segments[_head];
			float remaining = current.Duration - _elapsed;
			if (delta < remaining)
			{
				_elapsed += delta;
				ApplyProgress(current);
				return true;
			}
			Position = _startPosition = current.Position;
			Rotation = _startRotation = current.Rotation;
			delta -= remaining;
			_elapsed = 0f;
			_head = (_head + 1) % Capacity;
			_count--;
		}
		return true;
	}

	private void ApplyProgress(Segment current)
	{
		float progress = Mathf.Clamp01(_elapsed / current.Duration);
		Position = Vector3.Lerp(_startPosition, current.Position, progress);
		Rotation = Quaternion.Slerp(_startRotation, current.Rotation, progress);
	}

	private void LimitQueuedDuration()
	{
		if (_count < 2) return;
		float remaining = -_elapsed;
		for (int i = 0; i < _count - 1; i++)
			remaining += _segments[(_head + i) % Capacity].Duration;
		if (remaining <= MaxQueuedDuration) return;

		float scale = MaxQueuedDuration / remaining;
		for (int i = 0; i < _count - 1; i++)
		{
			int index = (_head + i) % Capacity;
			_segments[index].Duration *= scale;
		}
		_elapsed *= scale;
	}

	private void SynchronizeArrival(float duration)
	{
		if (_count <= 1) return;
		float remaining = _segments[_head].Duration - _elapsed;
		for (int i = 1; i < _count; i++)
			remaining += _segments[(_head + i) % Capacity].Duration;
		if (remaining <= duration) return;

		// Scale only unstarted queued segments so in-flight pose and velocity do not jump
		float unstartedRemaining = 0f;
		for (int i = 1; i < _count; i++)
			unstartedRemaining += _segments[(_head + i) % Capacity].Duration;

		if (unstartedRemaining > 0.0001f)
		{
			float targetUnstarted = Mathf.Max(0.001f, duration - (_segments[_head].Duration - _elapsed));
			float scale = targetUnstarted / unstartedRemaining;
			for (int i = 1; i < _count; i++)
			{
				int index = (_head + i) % Capacity;
				_segments[index].Duration *= scale;
			}
		}
	}
}
