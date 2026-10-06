using System;
using System.Collections.Generic;
using DG.Tweening;
using Spine.Unity;
using UnityEngine;

public class GameUtils
{
	public static Spine.Animation GetSkeletonAnimation(Spine.SkeletonData data, string name)
	{
		if (data == null) return null;
		for (int i = 0; i < data.Animations.Count; i++)
		{
			Spine.Animation animation = data.Animations.Items[i];
			if (animation.Name == name) return animation;
		}
		return null;
	}

	public static float GetDurationOfSpine(SkeletonGraphic skeleton, string animName)
	{
		return GetSkeletonAnimation(skeleton.SkeletonDataAsset.GetAnimationStateData().SkeletonData, animName).Duration;
	}

	public static float GetDurationOfSpine(SkeletonAnimation skeletonAnimation, string aniName)
	{
		return GetSkeletonAnimation(skeletonAnimation.AnimationState.Data.SkeletonData, aniName).Duration;
	}

	public static List<T> RandomSortList<T>(List<T> list)
	{
		if (list == null) throw new ArgumentNullException(nameof(list));
		var random = new System.Random();
		var result = new List<T>();
		foreach (T item in list) result.Insert(random.Next(result.Count + 1), item);
		return result;
	}

	// Both jump helpers operate in local XY and preserve the node's Z position.
	public static void LocalJumpByParabola(Transform node, Vector3 targetPos,
		float duration, float jumpHeight, float jumpDuration)
	{
		Vector3 origin = node.localPosition;
		Sequence vertical = DOTween.Sequence();
		node.DOBlendableLocalMoveBy(new Vector3(targetPos.x - origin.x, 0f, 0f), duration)
			.SetEase(Ease.Linear);
		vertical.Append(node.DOBlendableLocalMoveBy(new Vector3(0f, jumpHeight, 0f), jumpDuration)
			.SetEase(Ease.OutQuart));
		vertical.Append(node.DOBlendableLocalMoveBy(
			new Vector3(0f, targetPos.y - origin.y - jumpHeight, 0f), duration - jumpDuration)
			.SetEase(Ease.InQuart));
	}

	public static Tween DOParabolaJumpTo(Transform node, Vector3 targetPos,
		float duration, float jumpHeight, float jumpDuration)
	{
		if (jumpDuration > duration) throw new Exception("jumpDuration can not bigger then duration");
		Vector3 origin = node.localPosition;
		return DOTween.Sequence()
			.Append(node.DOBlendableLocalMoveBy(new Vector3(0f, jumpHeight, 0f), jumpDuration)
				.SetEase(Ease.OutSine))
			.Append(node.DOBlendableLocalMoveBy(
				new Vector3(0f, targetPos.y - origin.y - jumpHeight, 0f), duration - jumpDuration)
				.SetEase(Ease.InQuad))
			.Insert(0f, node.DOBlendableLocalMoveBy(new Vector3(targetPos.x - origin.x, 0f, 0f), duration)
				.SetEase(Ease.Linear))
			.SetLink(node.gameObject);
	}

	public static void Shake(Transform trans, float duration)
	{
		Sequence sequence = DOTween.Sequence();
		for (int i = 0; i < 5; i++)
			sequence.Append(trans.DOShakePosition(duration / 5f,
				new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.5f, 0.5f), 0f),
				10, 90f, false, true));
	}

	public static void PlayParticles(Transform node)
	{
		foreach (ParticleSystem particle in node.GetComponentsInChildren<ParticleSystem>()) particle.Play();
	}

	public static List<Vector3> ResamplePointsOptimized(List<Vector3> originalPoints, int n)
	{
		if (originalPoints == null || originalPoints.Count < 2 || n < 2)
		{
			Debug.LogError("simple point param err!");
			return new List<Vector3>();
		}
		var lengths = new float[originalPoints.Count];
		for (int i = 1; i < originalPoints.Count; i++)
			lengths[i] = lengths[i - 1] + Vector3.Distance(originalPoints[i - 1], originalPoints[i]);
		float total = lengths[lengths.Length - 1];
		var result = new List<Vector3>(n);
		if (Mathf.Approximately(total, 0f))
		{
			for (int i = 0; i < n; i++) result.Add(originalPoints[0]);
			return result;
		}
		for (int i = 0; i < n; i++)
		{
			float targetDistance = total * (i / (float)(n - 1));
			int index = Array.BinarySearch(lengths, targetDistance);
			if (index < 0) index = ~index - 1;
			index = Mathf.Clamp(index, 0, originalPoints.Count - 2);
			float segment = lengths[index + 1] - lengths[index];
			float t = Mathf.Approximately(segment, 0f) ? 0f : (targetDistance - lengths[index]) / segment;
			result.Add(Vector3.Lerp(originalPoints[index], originalPoints[index + 1], t));
		}
		return result;
	}

	public static List<Vector3> GetEquidistantPoints(List<ControlPoint> curve, int n, int totalPoints = 0)
	{
		if (curve == null) throw new ArgumentNullException(nameof(curve));
		var result = new List<Vector3>();
		if (curve.Count < 2) return result;
		if (totalPoints > 0) n = Mathf.Max(2, totalPoints / (curve.Count - 1));
		for (int i = 0; i < curve.Count - 1; i++)
		{
			ControlPoint current = curve[i];
			ControlPoint next = curve[i + 1];
			int count = i == curve.Count - 2 ? n : n - 1;
			List<Vector3> segment = BezierSampler.SampleCubicBezierEquidistant(
				current.Position, current.OutTangent, next.InTangent, next.Position, count);
			for (int j = i == 0 ? 0 : 1; j < segment.Count; j++) result.Add(segment[j]);
		}
		return result;
	}

	public static Vector3 GetBezierPoint(List<ControlPoint> curve, float t)
	{
		int index = GetBezierSegment(curve, t, out float localT);
		Vector3 p0 = curve[index].Position;
		Vector3 p1 = curve[index].OutTangent;
		Vector3 p2 = curve[index + 1].InTangent;
		Vector3 p3 = curve[index + 1].Position;
		float u = 1f - localT;
		return u * u * u * p0 + 3f * u * u * localT * p1
			+ 3f * u * localT * localT * p2 + localT * localT * localT * p3;
	}

	public static Vector3 GetBezierTangent(List<ControlPoint> curve, float t)
	{
		int index = GetBezierSegment(curve, t, out float localT);
		return CubicBezierTangent(localT, curve[index].Position, curve[index].OutTangent,
			curve[index + 1].InTangent, curve[index + 1].Position);
	}

	private static int GetBezierSegment(List<ControlPoint> curve, float t, out float localT)
	{
		if (curve == null) throw new ArgumentNullException(nameof(curve));
		if (curve.Count < 2) throw new ArgumentException("A Bezier curve requires at least two control points.", nameof(curve));
		float scaledT = t * (curve.Count - 1);
		int index = Mathf.Clamp(Mathf.FloorToInt(scaledT), 0, curve.Count - 2);
		localT = scaledT - index;
		return index;
	}

	private static Vector3 CubicBezierTangent(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
	{
		float u = 1f - t;
		Vector3 tangent = 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
		return tangent.normalized;
	}

	public static float GetPathDistance(List<Vector3> points)
	{
		if (points == null) throw new ArgumentNullException(nameof(points));
		float distance = 0f;
		for (int i = 1; i < points.Count; i++) distance += Vector3.Distance(points[i - 1], points[i]);
		return distance;
	}

	public static int GetNearestIndex(Vector3 pos, List<Vector3> pathPoints)
	{
		if (pathPoints == null) throw new ArgumentNullException(nameof(pathPoints));
		int nearest = 0;
		float nearestDistance = 1e10f;
		for (int i = 0; i < pathPoints.Count; i++)
		{
			float dx = pos.x - pathPoints[i].x;
			float dz = pos.z - pathPoints[i].z;
			float distance = Mathf.Sqrt(dx * dx + dz * dz);
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = i;
			}
		}
		return nearest;
	}
}
