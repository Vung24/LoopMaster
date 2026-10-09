using UnityEngine;

public static class GameplayCameraLayout
{
	public const float NativeDepthProjection = .9063078f; 
	public const float NativePadding = .4f;
	public const float NativeBottomReserve = 175f; 

	public static float NativeSize(Bounds bounds, float aspect, float visibleHeight, int level)
	{
		float height = bounds.size.z * NativeDepthProjection / (2f * Mathf.Max(.1f, visibleHeight));
		float width = (bounds.size.x + .5f) / (2f * Mathf.Max(.01f, aspect));
		return Mathf.Max(level <= 4 ? 8f : 9.6f, Mathf.Max(height, width) + NativePadding);
	}

	public static Rect Viewport(Rect pixels, Rect safeArea, float topPixels, float bottomPixels)
	{
		if (pixels.width <= 0f || pixels.height <= 0f) return new Rect(0, 0, 1, 1);
		if (safeArea.width <= 0f || safeArea.height <= 0f) safeArea = pixels;
		float left = Mathf.Max(pixels.xMin, safeArea.xMin);
		float right = Mathf.Min(pixels.xMax, safeArea.xMax);
		float top = Mathf.Min(pixels.yMax, safeArea.yMax) - Mathf.Max(0f, topPixels);
		float bottom = Mathf.Max(pixels.yMin, safeArea.yMin) + Mathf.Max(0f, bottomPixels);

		float inset = Mathf.Clamp(Mathf.Max(pixels.yMax - top, bottom - pixels.yMin), 0f, pixels.height * .45f);
		float horizontalInset = Mathf.Clamp(Mathf.Max(left - pixels.xMin, pixels.xMax - right), 0f, pixels.width * .45f);
		return new Rect(horizontalInset / pixels.width, inset / pixels.height,
			1f - 2f * horizontalInset / pixels.width, 1f - 2f * inset / pixels.height);
	}

	public static bool TryGetBounds(Transform root, out Bounds bounds)
	{
		bounds = default;
		bool found = false;
		if (root == null) return false;
		foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
		{
			if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
			MeshFilter filter = renderer.GetComponent<MeshFilter>();
			if (filter == null || filter.sharedMesh == null) continue;
			if (!found) { bounds = renderer.bounds; found = true; }
			else bounds.Encapsulate(renderer.bounds);
		}
		return found;
	}

	public static Bounds FallbackBounds(GameData data, Transform root)
	{
		float size = data?.Puzzle == null ? 8f : Mathf.Max(1f, data.Puzzle.Size);
		Bounds bounds = new Bounds(root == null ? Vector3.zero : root.position, new Vector3(size, 2f, size));
		if (data == null) return bounds;
		foreach (Vector3 point in data.PathPoints)
			bounds.Encapsulate(root == null ? point : root.TransformPoint(point));
		return bounds;
	}

	public static void Fit(Camera main, Camera top, Bounds bounds, Rect viewport, int level)
	{
		if (main == null || !main.orthographic) return;
		Vector3 extents = bounds.extents;
		Vector3 right = main.transform.right, up = main.transform.up, forward = main.transform.forward;
		float horizontal = Project(extents, right), vertical = Project(extents, up);
		float size = NativeSize(bounds, main.aspect, viewport.height, level);
		float projectedSize = Mathf.Max((vertical + NativePadding) / Mathf.Max(.1f, viewport.height),
			(horizontal + .25f) / (Mathf.Max(.01f, main.aspect) * Mathf.Max(.1f, viewport.width))) + NativePadding;
		size = Mathf.Max(size, projectedSize);
		float distance = Mathf.Max(25f, Project(extents, forward) + main.nearClipPlane + 2f);
		main.orthographicSize = size;
		main.transform.position = bounds.center - forward * distance;
		main.farClipPlane = Mathf.Max(main.farClipPlane, distance + Project(extents, forward) + 2f);
		if (top == null || top == main || !top.orthographic) return;
		top.orthographicSize = size;
		top.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
		top.rect = main.rect;
		top.aspect = main.aspect;
		top.nearClipPlane = main.nearClipPlane;
		top.farClipPlane = main.farClipPlane;
	}

	private static float Project(Vector3 extents, Vector3 axis) =>
		Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;
}
