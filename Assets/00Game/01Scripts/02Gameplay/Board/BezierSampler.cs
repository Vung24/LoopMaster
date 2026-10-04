using System.Collections.Generic;
using UnityEngine;

public static class BezierSampler
{
	public static List<Vector3> SampleCubicBezierEquidistant(
		Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int n)
	{
		return AdaptiveSample(p0, p1, p2, p3, n);
	}

	private static List<Vector3> AdaptiveSample(
		Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int n)
	{
		var result = new List<Vector3>();
		if (n < 2) return new List<Vector3> { p0, p3 };
		const int segments = 1000;
		var lengths = new List<float>(segments + 1) { 0f };
		Vector3 previous = p0;
		for (int i = 1; i <= segments; i++)
		{
			Vector3 point = CalculateCubicBezierPoint(p0, p1, p2, p3, i / (float)segments);
			lengths.Add(lengths[i - 1] + Vector3.Distance(previous, point));
			previous = point;
		}

		float total = lengths[lengths.Count - 1];
		for (int i = 0; i < n; i++)
		{
			float distance = total * i / (n - 1);
			float t = GetTByDistance(distance, lengths, segments);
			result.Add(CalculateCubicBezierPoint(p0, p1, p2, p3, t));
		}
		result[0] = p0;
		result[result.Count - 1] = p3;
		return result;
	}

	private static float GetTByDistance(float targetDistance,
		List<float> cumulativeLengths, int segments)
	{
		int upper = cumulativeLengths.BinarySearch(targetDistance);
		if (upper < 0) upper = ~upper;
		if (upper <= 0) return 0f;
		if (upper >= cumulativeLengths.Count) return 1f;
		float range = cumulativeLengths[upper] - cumulativeLengths[upper - 1];
		float fraction = range <= 0.00001f ? 0f :
			(targetDistance - cumulativeLengths[upper - 1]) / range;
		return Mathf.Clamp01((upper - 1 + fraction) / Mathf.Max(1, segments));
	}

	private static Vector3 CalculateCubicBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float u = 1f - t;
		return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
	}
}
