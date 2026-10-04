using System.Collections.Generic;
using UnityEngine;

public static class AdjacentLockRules
{
	private const float BoxFootprintWidth = 1.55f;
	private const float AdjacentPositionTolerance = 0.55f;
	// Native TryGetFlatAxes (RVA 0x1259D78) rejects almost vertical axes
	// before normalization: squared horizontal length must exceed 0.0001.
	private const float MinimumFlatAxisSquaredMagnitude = 0.0001f;
	private static readonly float SameDirectionDotThreshold = Mathf.Cos(5f * Mathf.Deg2Rad);

	public static List<int> GetSourceIndexes(IList<BoxData> boxes, int lockedBoxIndex)
	{
		var result = new List<int>();
		if (boxes == null || lockedBoxIndex < 0 || lockedBoxIndex >= boxes.Count) return result;

		BoxData lockedBox = boxes[lockedBoxIndex];
		if (lockedBox == null || lockedBox.IsAdjacentLocked <= 0) return result;
		for (int index = 0; index < boxes.Count; index++)
		{
			if (index == lockedBoxIndex) continue;
			if (CanUnlock(boxes[index], lockedBox)) result.Add(index);
		}
		return result;
	}

	public static bool CanUnlock(BoxData sourceBox, BoxData lockedBox)
	{
		return sourceBox != null && lockedBox != null &&
			CanUnlock(sourceBox.Position, sourceBox.Rotation, lockedBox.Position, lockedBox.Rotation);
	}

	public static bool CanUnlock(Vector3 sourcePos, Vector3 sourceRot,
		Vector3 lockPos, Vector3 lockRot)
	{
		Vector3 lockForward;
		Vector3 lockRight;
		Vector3 sourceForward;
		Vector3 sourceRight;
		if (!TryGetFlatAxes(lockRot, out lockForward, out lockRight) ||
			!TryGetFlatAxes(sourceRot, out sourceForward, out sourceRight)) return false;
		if (Vector3.Dot(lockForward, sourceForward) < SameDirectionDotThreshold) return false;

		Vector3 delta = FlattenXZ(sourcePos - lockPos);
		float sideDistance = Mathf.Abs(Vector3.Dot(delta, lockRight));
		float forwardDistance = Mathf.Abs(Vector3.Dot(delta, lockForward));
		return Mathf.Abs(sideDistance - BoxFootprintWidth) <= AdjacentPositionTolerance &&
			forwardDistance <= AdjacentPositionTolerance;
	}

	private static bool TryGetFlatAxes(Vector3 rotation, out Vector3 forward, out Vector3 right)
	{
		Quaternion quaternion = Quaternion.Euler(rotation);
		forward = FlattenXZ(quaternion * Vector3.forward);
		right = FlattenXZ(quaternion * Vector3.right);
		if (forward.sqrMagnitude <= MinimumFlatAxisSquaredMagnitude ||
			right.sqrMagnitude <= MinimumFlatAxisSquaredMagnitude) return false;
		forward.Normalize();
		right.Normalize();
		return true;
	}

	private static Vector3 FlattenXZ(Vector3 value)
	{
		return new Vector3(value.x, 0f, value.z);
	}
}
