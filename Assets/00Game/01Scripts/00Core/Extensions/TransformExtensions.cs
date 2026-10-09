using UnityEngine;

public static class TransformExtensions
{
	public static void SetLocalX(this Transform transform, float x)
	{
		if (transform == null) return;
		Vector3 pos = transform.localPosition;
		pos.x = x;
		transform.localPosition = pos;
	}

	public static void SetLocalY(this Transform transform, float y)
	{
		if (transform == null) return;
		Vector3 pos = transform.localPosition;
		pos.y = y;
		transform.localPosition = pos;
	}

	public static void SetLocalZ(this Transform transform, float z)
	{
		if (transform == null) return;
		Vector3 pos = transform.localPosition;
		pos.z = z;
		transform.localPosition = pos;
	}
}

