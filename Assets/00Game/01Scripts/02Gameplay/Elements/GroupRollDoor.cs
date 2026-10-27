using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GroupRollDoor : MonoBehaviour
{
	private const float DefaultWidth = 1.44f;
	private const float HorizontalPadding = 0.2f;
	private const float SliceCapRatio = 0.22f;
	private const float MinSize = 0.2f;
	private const float MinSourceSize = 0.001f;
	private const float MinDestroyDelay = 0.1f;
	private const float FixedWorldY = 1.08f;
	private const float ExitDirectionOffset = -0.1f;
	private const float DoorSideInset = 0.16f;
	private const float KeyInsertSurfaceOffset = 0.04f;
	private static readonly string[] LeftDoorNames = { "KeyLockDoorL", "KeylockDoorL" };
	private static readonly string[] RightDoorNames = { "KeyLockDoorR", "KeylockDoorR" };
	private static readonly string[] BodyNames = { "KeyLockBox" };
	private static readonly string[] KeyLockNames = { "KeyLock", "Keylock" };
	private readonly List<Mesh> _runtimeMeshes = new List<Mesh>();
	private GameObject _visualRoot;
	private Animation _unlockAnimation;
	private Box _referenceBox;
	private Transform _keyLockRoot;
	private Transform _keyHole;
	private GroupInfo _groupInfo;
	private bool _isUnlocked;
	public bool IsUnlockComplete => _isUnlocked && (_visualRoot == null || !_visualRoot.activeSelf);

	private struct DoorFrame
	{
		public Vector3 Center;
		public float Width;
	}

	private enum StretchRole { None, Body, LeftDoor, RightDoor }

	public void Init(GroupInfo groupInfo, IReadOnlyList<Box> groupBoxes, GameObject templatePrefab)
	{
		_groupInfo = groupInfo;
		_isUnlocked = groupInfo == null || !groupInfo.IsLocked;
		if (groupInfo == null || groupBoxes == null || groupBoxes.Count == 0) return;
		var boxes = new List<Box>(groupBoxes.Count);
		var positions = new List<Vector3>(groupBoxes.Count);
		for (int i = 0; i < groupBoxes.Count; i++)
		{
			if (groupBoxes[i] == null) continue;
			boxes.Add(groupBoxes[i]);
			positions.Add(groupBoxes[i].transform.position);
		}
		if (boxes.Count == 0) return;
		_referenceBox = boxes[0];
		Vector3 axis = ResolveAxis(boxes, positions);
		DoorFrame frame = CalculateFrame(boxes, positions, axis);
		Vector3 position = frame.Center + GetExitDirection(_referenceBox) * ExitDirectionOffset;
		position.y = FixedWorldY;
		transform.SetPositionAndRotation(position, GetRotationForAxis(axis));
		BuildVisual(groupInfo, templatePrefab, frame);
		if (_isUnlocked) HideKeyLock();
	}

	public void Unlock()
	{
		if (_isUnlocked) return;
		_isUnlocked = true;
		_groupInfo?.Unlock();
		if (_referenceBox != null && _referenceBox.GameLoader != null && GameAudioPlayer.Current != null)
			GameAudioPlayer.Current.PlayColorLockOpen(_referenceBox.GameLoader.ColorLockOpen);
		if (PlayUnlockAnimation(_unlockAnimation, out float length))
			Destroy(gameObject, Mathf.Max(MinDestroyDelay, length));
		else Destroy(gameObject);
	}

	public bool GetKeyInsertPose(out Vector3 position, out Quaternion rotation, out Vector3 insertDirection)
	{
		position = Vector3.zero;
		rotation = Quaternion.identity;
		insertDirection = Vector3.down;
		Transform hole = GetKeyHoleTransform();
		if (hole == null) return false;
		Vector3 forward = transform.forward;
		forward.y = 0f;
		if (forward.sqrMagnitude < MinSourceSize) forward = Vector3.forward;
		position = hole.position + Vector3.up * KeyInsertSurfaceOffset;
		rotation = Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
		return true;
	}

	public void HideKeyLock()
	{
		Transform keyLock = GetKeyLockRoot();
		if (keyLock != null) keyLock.gameObject.SetActive(false);
	}

	private void BuildVisual(GroupInfo groupInfo, GameObject templatePrefab, DoorFrame frame)
	{
		if (_visualRoot != null) Destroy(_visualRoot);
		ReleaseRuntimeMeshes();
		_keyLockRoot = null;
		_keyHole = null;
		_unlockAnimation = null;
		if (templatePrefab == null) return;
		_visualRoot = Instantiate(templatePrefab, transform);
		_visualRoot.name = templatePrefab.name + "_Visual";
		Transform root = _visualRoot.transform;
		root.localPosition = Vector3.zero;
		root.localRotation = Quaternion.identity;
		root.localScale = Vector3.one;
		GroupDoorVisualReferences references = _visualRoot.GetComponent<GroupDoorVisualReferences>();
		_unlockAnimation = references != null && references.UnlockAnimation != null ?
			references.UnlockAnimation : _visualRoot.GetComponentInChildren<Animation>(true);
		_keyLockRoot = references != null ? references.KeyLockRoot : null;
		_keyHole = references != null ? references.KeyHole : null;
		CacheKeyLockTransforms();
		ApplyGroupMaterial(groupInfo);
		if (!CalculateLocalBounds(root, out Bounds sourceBounds))
		{
			Debug.LogWarning("GroupRollDoor build failed: template has no renderable bounds.", this);
			return;
		}
		root.localPosition = -sourceBounds.center;
		StretchMeshes(root, sourceBounds, frame.Width);
	}

	private void CacheKeyLockTransforms()
	{
		if (_keyLockRoot == null && _visualRoot != null)
			_keyLockRoot = ResolveDescendant(_visualRoot.transform, KeyLockNames);
		if (_keyHole == null) _keyHole = ResolveKeyHole(_keyLockRoot);
	}

	private Transform GetKeyLockRoot()
	{
		CacheKeyLockTransforms();
		return _keyLockRoot;
	}

	private Transform GetKeyHoleTransform()
	{
		CacheKeyLockTransforms();
		return _keyHole != null ? _keyHole : _keyLockRoot;
	}

	private static Transform ResolveKeyHole(Transform keyLock)
	{
		SpriteRenderer sprite = keyLock == null ? null : keyLock.GetComponentInChildren<SpriteRenderer>(true);
		return sprite == null ? null : sprite.transform;
	}

	private void ApplyGroupMaterial(GroupInfo groupInfo)
	{
		if (_visualRoot == null || groupInfo == null || !GroupColorLockConstants.IsValidType(groupInfo.LockedByColor)) return;
		Transform keyLock = GetKeyLockRoot();
		MeshRenderer renderer = keyLock == null ? null : keyLock.GetComponentInChildren<MeshRenderer>(true);
		if (renderer == null) return;
		Material material = GameAssetsConfig.GetGroupLockMatByType(groupInfo.LockedByColor);
		if (material != null)
		{
			Material[] materials = renderer.sharedMaterials;
			for (int i = 0; i < materials.Length; i++) materials[i] = material;
			renderer.sharedMaterials = materials;
		}
		else
		{
			var block = new MaterialPropertyBlock();
			renderer.GetPropertyBlock(block);
			block.SetColor("_BaseColor", GroupColorLockConstants.GetColorByType(groupInfo.LockedByColor));
			renderer.SetPropertyBlock(block);
		}
	}

	private void StretchMeshes(Transform root, Bounds sourceBounds, float targetWidth)
	{
		Transform leftDoor = ResolveDescendant(root, LeftDoorNames);
		Transform rightDoor = ResolveDescendant(root, RightDoorNames);
		float targetMinX = sourceBounds.center.x - targetWidth * 0.5f;
		float targetMaxX = sourceBounds.center.x + targetWidth * 0.5f;
		float inset = Mathf.Min(DoorSideInset, targetWidth * 0.25f);
		float leftX = targetMinX + inset;
		float rightX = targetMaxX - inset;
		MovePivotRootX(root, leftDoor, leftX);
		MovePivotRootX(root, rightDoor, rightX);
		bool hasLeft = CalculateLocalBounds(root, leftDoor, out Bounds leftBounds);
		bool hasRight = CalculateLocalBounds(root, rightDoor, out Bounds rightBounds);
		foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
		{
			MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
			if (filter.sharedMesh == null || renderer == null) continue;
			StretchRole role = GetStretchRole(filter, renderer, leftDoor, rightDoor);
			if (role == StretchRole.None) continue;
			Bounds bounds = sourceBounds;
			float min = targetMinX, max = targetMaxX;
			if (role == StretchRole.LeftDoor && hasLeft)
			{
				bounds = leftBounds;
				min = leftX;
				max = sourceBounds.center.x;
			}
			else if (role == StretchRole.RightDoor && hasRight)
			{
				bounds = rightBounds;
				min = sourceBounds.center.x;
				max = rightX;
			}
			Mesh mesh = CreateSlicedMesh(filter.sharedMesh, root, filter.transform, bounds, min, max);
			filter.sharedMesh = mesh;
			_runtimeMeshes.Add(mesh);
		}
	}

	private static StretchRole GetStretchRole(MeshFilter filter, MeshRenderer renderer,
		Transform leftDoor, Transform rightDoor)
	{
		if (IsSameOrChildOf(renderer.transform, leftDoor)) return StretchRole.LeftDoor;
		if (IsSameOrChildOf(renderer.transform, rightDoor)) return StretchRole.RightDoor;
		return IsNamed(filter.name, BodyNames) || IsNamed(filter.sharedMesh.name, BodyNames) ?
			StretchRole.Body : StretchRole.None;
	}

	private static bool IsSameOrChildOf(Transform child, Transform parent) =>
		child != null && parent != null && (child == parent || child.IsChildOf(parent));

	private static bool IsNamed(string value, IReadOnlyList<string> names)
	{
		if (string.IsNullOrEmpty(value)) return false;
		for (int i = 0; i < names.Count; i++)
			if (string.Equals(value, names[i], StringComparison.OrdinalIgnoreCase)) return true;
		return false;
	}

	private static Transform ResolveDescendant(Transform root, IReadOnlyList<string> names)
	{
		if (root == null) return null;
		foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
			if (IsNamed(child.name, names)) return child;
		return null;
	}

	private static void MovePivotRootX(Transform root, Transform pivot, float targetRootX)
	{
		if (pivot == null) return;
		float delta = targetRootX - GetLocalToRootMatrix(root, pivot).MultiplyPoint3x4(Vector3.zero).x;
		if (Mathf.Abs(delta) <= MinSourceSize) return;
		Matrix4x4 parentToRoot = pivot.parent == null || pivot.parent == root ?
			Matrix4x4.identity : GetLocalToRootMatrix(root, pivot.parent);
		pivot.localPosition += parentToRoot.inverse.MultiplyVector(new Vector3(delta, 0f, 0f));
	}

	private static Mesh CreateSlicedMesh(Mesh source, Transform root, Transform meshTransform,
		Bounds sourceBounds, float targetMinX, float targetMaxX)
	{
		Matrix4x4 toRoot = GetLocalToRootMatrix(root, meshTransform);
		Matrix4x4 fromRoot = toRoot.inverse;
		Vector3[] vertices = source.vertices;
		float sourceWidth = sourceBounds.size.x;
		float targetWidth = targetMaxX - targetMinX;
		float capWidth = sourceWidth * SliceCapRatio;
		float leftBoundary = sourceBounds.min.x + capWidth;
		float rightBoundary = sourceBounds.max.x - capWidth;
		bool sliced = sourceWidth > MinSourceSize && targetWidth > capWidth * 2f;
		float centerScale = (targetWidth - capWidth * 2f) / Mathf.Max(MinSourceSize, sourceWidth - capWidth * 2f);
		for (int i = 0; i < vertices.Length; i++)
		{
			Vector3 point = toRoot.MultiplyPoint3x4(vertices[i]);
			point.x = sliced ? SliceX(point.x, sourceBounds.min.x, sourceBounds.max.x,
				targetMinX, targetMaxX, leftBoundary, rightBoundary, capWidth, centerScale) :
				(targetMinX + targetMaxX) * 0.5f + (point.x - sourceBounds.center.x) * GetScale(targetWidth, sourceWidth);
			vertices[i] = fromRoot.MultiplyPoint3x4(point);
		}
		var mesh = new Mesh
		{
			name = source.name + "_GroupRollDoor",
			indexFormat = source.indexFormat == IndexFormat.UInt32 || source.vertexCount > 65535 ?
				IndexFormat.UInt32 : IndexFormat.UInt16,
			vertices = vertices
		};
		CopyMeshData(source, mesh);
		mesh.RecalculateBounds();
		return mesh;
	}

	private static float SliceX(float x, float sourceMinX, float sourceMaxX, float targetMinX,
		float targetMaxX, float leftBoundary, float rightBoundary, float capWidth, float centerScale)
	{
		if (x <= leftBoundary) return targetMinX + x - sourceMinX;
		if (x >= rightBoundary) return targetMaxX - (sourceMaxX - x);
		return targetMinX + capWidth + (x - leftBoundary) * centerScale;
	}

	private static float GetScale(float targetSize, float sourceSize) =>
		Mathf.Abs(sourceSize) > MinSourceSize ? targetSize / sourceSize : 1f;

	private static void CopyMeshData(Mesh source, Mesh target)
	{
		if (source.normals.Length == source.vertexCount) target.normals = source.normals;
		if (source.tangents.Length == source.vertexCount) target.tangents = source.tangents;
		if (source.colors32.Length == target.vertexCount) target.colors32 = source.colors32;
		var uv = new List<Vector4>();
		for (int channel = 0; channel < 8; channel++)
		{
			source.GetUVs(channel, uv);
			if (uv.Count == source.vertexCount) target.SetUVs(channel, uv);
		}
		target.subMeshCount = source.subMeshCount;
		for (int i = 0; i < source.subMeshCount; i++) target.SetTriangles(source.GetTriangles(i), i);
		if (target.normals.Length == 0) target.RecalculateNormals();
		if (target.tangents.Length == 0) target.RecalculateTangents();
	}

	private static Matrix4x4 GetLocalToRootMatrix(Transform root, Transform child)
	{
		Matrix4x4 matrix = Matrix4x4.identity;
		while (child != null && child != root)
		{
			matrix = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale) * matrix;
			child = child.parent;
		}
		return matrix;
	}

	private static bool CalculateLocalBounds(Transform root, out Bounds bounds)
	{
		bool hasBounds = CalculateLocalBounds(root, root, out bounds);
		foreach (SpriteRenderer sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
			if (sprite.sprite != null)
				EncapsulateBounds(sprite.sprite.bounds, GetLocalToRootMatrix(root, sprite.transform), ref bounds, ref hasBounds);
		return hasBounds;
	}

	private static bool CalculateLocalBounds(Transform root, Transform scope, out Bounds bounds)
	{
		bounds = default;
		bool hasBounds = false;
		if (scope == null) return false;
		foreach (MeshFilter filter in scope.GetComponentsInChildren<MeshFilter>(true))
			if (filter.sharedMesh != null)
				EncapsulateBounds(filter.sharedMesh.bounds, GetLocalToRootMatrix(root, filter.transform), ref bounds, ref hasBounds);
		return hasBounds;
	}

	private static void EncapsulateBounds(Bounds sourceBounds, Matrix4x4 matrix, ref Bounds bounds, ref bool hasBounds)
	{
		Vector3 min = sourceBounds.min, max = sourceBounds.max;
		for (int i = 0; i < 8; i++)
		{
			Vector3 point = matrix.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? min.x : max.x,
				(i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
			if (hasBounds) bounds.Encapsulate(point);
			else { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
		}
	}

	private static bool PlayUnlockAnimation(Animation animation, out float animationLength)
	{
		animationLength = 0f;
		if (animation == null) return false;
		animation.Stop();
		if (animation.clip != null)
		{
			animationLength = animation.clip.length;
			if (animation.Play()) return true;
		}
		foreach (AnimationState state in animation)
		{
			if (state.clip == null) continue;
			animation.clip = state.clip;
			animationLength = state.clip.length;
			return animation.Play(state.name);
		}
		return false;
	}

	private static Vector3 ResolveAxis(IReadOnlyList<Box> boxes, IReadOnlyList<Vector3> positions)
	{
		Vector3 axis = Vector3.zero;
		float longest = 0f;
		for (int i = 0; i < positions.Count; i++)
			for (int j = i + 1; j < positions.Count; j++)
			{
				Vector3 delta = positions[j] - positions[i];
				delta.y = 0f;
				if (delta.sqrMagnitude > longest) { axis = delta; longest = delta.sqrMagnitude; }
			}
		Vector3 right = boxes.Count > 0 ? boxes[0].transform.right : Vector3.right;
		right.y = 0f;
		if (axis.sqrMagnitude < MinSourceSize) axis = right;
		if (axis.sqrMagnitude < MinSourceSize) axis = Vector3.right;
		axis.Normalize();
		if (right.sqrMagnitude > MinSourceSize && Vector3.Dot(axis, right.normalized) < 0f) axis = -axis;
		return axis;
	}

	private static Quaternion GetRotationForAxis(Vector3 axis) => Quaternion.LookRotation(GetDepthAxis(axis), Vector3.up);

	private static Vector3 GetExitDirection(Box box)
	{
		Vector3 direction = box.transform.forward;
		direction.y = 0f;
		return direction.sqrMagnitude <= MinSourceSize ? Vector3.back : -direction.normalized;
	}

	private static Vector3 GetDepthAxis(Vector3 axis)
	{
		Vector3 depth = Vector3.Cross(axis, Vector3.up);
		return depth.sqrMagnitude < MinSourceSize ? Vector3.forward : depth.normalized;
	}

	private static DoorFrame CalculateFrame(IReadOnlyList<Box> boxes, IReadOnlyList<Vector3> positions, Vector3 axis)
	{
		Vector3 origin = Vector3.zero;
		for (int i = 0; i < positions.Count; i++) origin += positions[i];
		origin /= positions.Count;
		Vector3 depth = GetDepthAxis(axis);
		Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
		bool hasBounds = false;
		for (int i = 0; i < boxes.Count; i++)
		{
			Transform card = boxes[i] != null ? boxes[i].BoxCard : null;
			if (card == null) throw new InvalidOperationException("GroupRollDoor frame requires BoxCard. box=" + boxes[i].name);
			foreach (Renderer renderer in card.GetComponentsInChildren<Renderer>())
			{
				EncapsulateWorldBounds(renderer.bounds, origin, axis, depth, ref min, ref max);
				hasBounds = true;
			}
		}
		if (!hasBounds) throw new InvalidOperationException("GroupRollDoor frame requires at least one renderer under BoxCard.");
		min.x -= HorizontalPadding;
		max.x += HorizontalPadding;
		Vector3 center = (min + max) * 0.5f;
		return new DoorFrame
		{
			Center = origin + axis * center.x + Vector3.up * center.y + depth * center.z,
			Width = Mathf.Max(MinSize, Mathf.Max(DefaultWidth, max.x - min.x))
		};
	}

	private static void EncapsulateWorldBounds(Bounds bounds, Vector3 origin, Vector3 axis, Vector3 depth,
		ref Vector3 min, ref Vector3 max)
	{
		Vector3 lo = bounds.min, hi = bounds.max;
		for (int i = 0; i < 8; i++)
		{
			Vector3 point = Project(new Vector3((i & 1) == 0 ? lo.x : hi.x, (i & 2) == 0 ? lo.y : hi.y,
				(i & 4) == 0 ? lo.z : hi.z), origin, axis, depth);
			min = Vector3.Min(min, point);
			max = Vector3.Max(max, point);
		}
	}

	private static Vector3 Project(Vector3 point, Vector3 origin, Vector3 axis, Vector3 depth)
	{
		Vector3 delta = point - origin;
		return new Vector3(Vector3.Dot(delta, axis), delta.y, Vector3.Dot(delta, depth));
	}

	private void ReleaseRuntimeMeshes()
	{
		foreach (Mesh mesh in _runtimeMeshes) if (mesh != null) Destroy(mesh);
		_runtimeMeshes.Clear();
	}

	private void OnDestroy() => ReleaseRuntimeMeshes();
}
