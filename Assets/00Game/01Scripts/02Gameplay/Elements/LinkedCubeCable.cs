using System;
using UnityEngine;

public sealed class LinkedCubeCable : MonoBehaviour
{
	[SerializeField] private Mesh _bridgeMesh;
	[SerializeField] private Mesh _jointMesh;
	[SerializeField] private Material _bridgeMaterial;
	[SerializeField] private Vector3 _memberSide = new Vector3(.5f, 0f, .36f);
	[SerializeField, Min(.01f)] private float _cableDiameter = .14f;
	private const int BendSegmentCount = 5;
	private Renderer _firstHalfRenderer, _secondHalfRenderer;
	private Renderer _firstStubRenderer, _secondStubRenderer;
	private Renderer _firstMountRenderer, _secondMountRenderer;
	private Renderer[] _firstBendRenderers, _secondBendRenderers;
	private MaterialPropertyBlock _properties;
	private GameData _data;
	private LinkedCubePair _pair;
	public LinkedCubePair Pair => _pair;
	private Transform _bridge, _firstSocket, _secondSocket;
	private Vector3 _firstAnchor, _secondAnchor;
	private Vector3 _firstElbow, _secondElbow;
	private bool _hasFirst, _hasSecond;
	private float _blockedUntil;
	private float _consumedAt = -1f;
	private bool _cachedReady;
	private float _nextReadyCheckTime;
	private Color _firstTintColor, _secondTintColor;
	private bool _hasTintColor;
	private Vector3 _geometryFirstAnchor, _geometrySecondAnchor;
	private Vector3 _geometryFirstElbow, _geometrySecondElbow;
	private float _geometryThickness;
	private bool _hasGeometry;

	/// <summary>Creates a two-color cable with side exits and a straight middle span.</summary>
	public void Initialize(GameData data)
	{
		if (data == null) throw new ArgumentNullException(nameof(data));
		Initialize(data, data.LinkedPair);
	}

	/// <summary>Binds this cable to one independent pair on the current board.</summary>
	public void Initialize(GameData data, LinkedCubePair pair)
	{
		if (data == null) throw new ArgumentNullException(nameof(data));
		if (pair != null && !data.ContainsLinkedPair(pair))
			throw new InvalidOperationException("The cable pair must belong to the initialized board.");
		_data = data;
		_pair = pair;
		_hasGeometry = false;
		if (_pair == null) { enabled = false; return; }
		if (_bridgeMesh == null || _jointMesh == null || _bridgeMaterial == null)
			throw new InvalidOperationException("Assign the cylinder mesh, joint mesh and material on LinkedCubeCable.prefab.");
		_properties = new MaterialPropertyBlock();
		_bridge = CreateRoot("Bridge");
		_firstHalfRenderer = CreatePart("FirstHalf", _bridgeMesh);
		_secondHalfRenderer = CreatePart("SecondHalf", _bridgeMesh);
		_firstStubRenderer = CreatePart("FirstSideExit", _bridgeMesh);
		_secondStubRenderer = CreatePart("SecondSideExit", _bridgeMesh);
		_firstMountRenderer = CreatePart("FirstMount", _jointMesh);
		_secondMountRenderer = CreatePart("SecondMount", _jointMesh);
		_firstBendRenderers = CreateBend("FirstBend");
		_secondBendRenderers = CreateBend("SecondBend");
		_firstSocket = CreateRoot("Anchor_A");
		_secondSocket = CreateRoot("Anchor_B");
	}

	private Renderer[] CreateBend(string name)
	{
		var renderers = new Renderer[BendSegmentCount];
		for (int i = 0; i < renderers.Length; i++)
			renderers[i] = CreatePart(name + i, _bridgeMesh);
		return renderers;
	}

	private Renderer CreatePart(string name, Mesh mesh)
	{
		return CreateMesh(name, _bridge, mesh, _bridgeMaterial,
			Vector3.zero, Quaternion.identity, Vector3.one);
	}

	public void ShowBlocked(LinkedCubeLaunchFailure reason)
	{
		_blockedUntil = Time.time + .55f;
	}

	private Transform CreateRoot(string name)
	{
		var root = new GameObject(name);
		root.transform.SetParent(transform, false);
		root.SetActive(false);
		return root.transform;
	}

	private static Renderer CreateMesh(string name, Transform parent, Mesh mesh, Material material,
		Vector3 position, Quaternion rotation, Vector3 scale)
	{
		var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
		part.transform.SetParent(parent, false);
		part.transform.localPosition = position;
		part.transform.localRotation = rotation;
		part.transform.localScale = scale;
		part.GetComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer renderer = part.GetComponent<MeshRenderer>();
		renderer.sharedMaterial = material;
		return renderer;
	}

	private void LateUpdate()
	{
		if (_pair == null || _data == null || GameData.Current != _data || _bridge == null) return;
		bool consumed = _pair.State == LinkedCubePairState.Consumed;
		if (!consumed) _consumedAt = -1f;
		else if (_consumedAt < 0f) _consumedAt = Time.time;
		float fade = consumed ? Mathf.Clamp01(1f - (Time.time - _consumedAt) / .18f) : 1f;
		Vector3 firstCenter = MemberCenter(_pair.FirstCube, _firstAnchor);
		Vector3 secondCenter = MemberCenter(_pair.SecondCube, _secondAnchor);
		UpdateAnchor(_pair.FirstCube, secondCenter, ref _firstAnchor, ref _firstElbow, ref _hasFirst);
		UpdateAnchor(_pair.SecondCube, firstCenter, ref _secondAnchor, ref _secondElbow, ref _hasSecond);
		bool show = fade > 0f && _hasFirst && _hasSecond;
		SetVisible(_bridge, show);
		SetVisible(_firstSocket, show);
		SetVisible(_secondSocket, show);
		if (!show) return;
		if (Time.time >= _nextReadyCheckTime)
		{
			_cachedReady = LinkedCubeLaunch.CanBegin(_data, _pair.FirstBox, out _);
			_nextReadyCheckTime = Time.time + 0.08f;
		}
		bool ready = _cachedReady;
		float pulse = 1f;
		if (Time.time < _blockedUntil) pulse += .06f * Mathf.Sin(Time.time * 30f);
		else if (ready) pulse += .015f * Mathf.Sin(Time.time * 4f);
		float thickness = _cableDiameter * fade * pulse;
		RefreshGeometry(thickness);
		UpdateTint();
	}

	private void RefreshGeometry(float thickness)
	{
		// Exact comparisons retain tiny movements and the existing pulse/fade timeline.
		if (_hasGeometry && _geometryThickness == thickness &&
			_geometryFirstAnchor.Equals(_firstAnchor) && _geometrySecondAnchor.Equals(_secondAnchor) &&
			_geometryFirstElbow.Equals(_firstElbow) && _geometrySecondElbow.Equals(_secondElbow)) return;
		UpdateBend(_firstBendRenderers, _firstAnchor, _firstElbow, _secondElbow, thickness,
			out Vector3 firstExit, out Vector3 firstMiddle);
		UpdateBend(_secondBendRenderers, _secondAnchor, _secondElbow, _firstElbow, thickness,
			out Vector3 secondExit, out Vector3 secondMiddle);
		Vector3 midpoint = (firstMiddle + secondMiddle) * .5f;
		SetSegment(_firstStubRenderer.transform, _firstAnchor, firstExit, thickness);
		SetSegment(_firstHalfRenderer.transform, firstMiddle, midpoint, thickness);
		SetSegment(_secondHalfRenderer.transform, midpoint, secondMiddle, thickness);
		SetSegment(_secondStubRenderer.transform, secondExit, _secondAnchor, thickness);
		SetMount(_firstMountRenderer.transform, _firstAnchor, thickness * 1.2f);
		SetMount(_secondMountRenderer.transform, _secondAnchor, thickness * 1.2f);
		_firstSocket.localPosition = _firstAnchor;
		_secondSocket.localPosition = _secondAnchor;
		_geometryFirstAnchor = _firstAnchor;
		_geometrySecondAnchor = _secondAnchor;
		_geometryFirstElbow = _firstElbow;
		_geometrySecondElbow = _secondElbow;
		_geometryThickness = thickness;
		_hasGeometry = true;
	}

	private static void UpdateBend(Renderer[] renderers, Vector3 anchor, Vector3 elbow,
		Vector3 partnerElbow, float diameter, out Vector3 start, out Vector3 end)
	{
		Vector3 incoming = anchor - elbow;
		Vector3 outgoing = partnerElbow - elbow;
		float trim = Mathf.Min(.12f, incoming.magnitude * .45f, outgoing.magnitude * .2f);
		start = elbow + incoming.normalized * trim;
		end = elbow + outgoing.normalized * trim;
		Vector3 previous = start;
		for (int i = 0; i < renderers.Length; i++)
		{
			float progress = (i + 1f) / renderers.Length;
			Vector3 first = Vector3.Lerp(start, elbow, progress);
			Vector3 second = Vector3.Lerp(elbow, end, progress);
			Vector3 current = Vector3.Lerp(first, second, progress);
			SetSegment(renderers[i].transform, previous, current, diameter);
			previous = current;
		}
	}

	private static void SetSegment(Transform segment, Vector3 start, Vector3 end, float diameter)
	{
		Vector3 span = end - start;
		Quaternion rotation = Quaternion.identity;
		if (span.sqrMagnitude > .0001f)
			rotation = Quaternion.FromToRotation(Vector3.up, span.normalized);
		segment.SetLocalPositionAndRotation((start + end) * .5f, rotation);
		segment.localScale = new Vector3(diameter, span.magnitude * .5f, diameter);
	}

	private static void SetMount(Transform joint, Vector3 position, float diameter)
	{
		joint.localPosition = position;
		joint.localScale = Vector3.one * diameter;
	}

	private static void SetVisible(Transform part, bool visible)
	{
		if (part.gameObject.activeSelf != visible) part.gameObject.SetActive(visible);
	}

	private Vector3 MemberCenter(CubeInfo member, Vector3 fallback)
	{
		if (member != null && member.Size > 0 && member.CubeSc != null) return member.CubeSc.transform.position;
		return transform.TransformPoint(fallback);
	}

	private void UpdateAnchor(CubeInfo member, Vector3 partnerCenter,
		ref Vector3 local, ref Vector3 elbow, ref bool captured)
	{
		if (member == null || member.Size <= 0 || member.CubeSc == null) return;
		Transform cube = member.CubeSc.transform;
		Vector3 direction = cube.InverseTransformPoint(partnerCenter);
		// Prefer the side face so slot differences do not drag the exit towards a corner.
		Vector3 outward = Vector3.right * Mathf.Sign(direction.x);
		float edge = _memberSide.x;
		if (Mathf.Abs(direction.x) < .0001f)
		{
			outward = Vector3.forward * Mathf.Sign(direction.z);
			edge = _memberSide.z;
		}
		float radius = _cableDiameter * .5f * 1.06f;
		Vector3 side = outward * Mathf.Max(0f, edge - radius);
		side.y = _memberSide.y;
		local = transform.InverseTransformPoint(cube.TransformPoint(side));
		Vector3 bend = outward * (edge + radius + .045f);
		bend.y = _memberSide.y;
		elbow = transform.InverseTransformPoint(cube.TransformPoint(bend));
		captured = true;
	}

	private void UpdateTint()
	{
		Color first = GetMemberTint(_pair.FirstCube);
		Color second = GetMemberTint(_pair.SecondCube);
		if (_hasTintColor && _firstTintColor == first && _secondTintColor == second) return;
		_firstTintColor = first;
		_secondTintColor = second;
		_hasTintColor = true;
		ApplyTint(_firstHalfRenderer, first);
		ApplyTint(_secondHalfRenderer, second);
		ApplyTint(_firstStubRenderer, first);
		ApplyTint(_firstMountRenderer, first);
		ApplyTint(_secondStubRenderer, second);
		ApplyTint(_secondMountRenderer, second);
		for (int i = 0; i < BendSegmentCount; i++)
		{
			ApplyTint(_firstBendRenderers[i], first);
			ApplyTint(_secondBendRenderers[i], second);
		}
	}

	private static Color GetMemberTint(CubeInfo member)
	{
		if (member == null) return Color.white;
		if (!member.IsSecret) return GameAssetsConfig.GetCubeColor(member.Type);

		Material secretMaterial = GameAssetsConfig.GetSecretMat();
		if (secretMaterial == null) return Color.white;
		if (secretMaterial.HasProperty("_BaseColor")) return secretMaterial.GetColor("_BaseColor");
		if (secretMaterial.HasProperty("_Color")) return secretMaterial.GetColor("_Color");
		return Color.white;
	}

	private void ApplyTint(Renderer renderer, Color color)
	{
		_properties.SetColor("_BaseColor", color);
		_properties.SetColor("_Color", color);
		_properties.SetFloat("_Glossiness", .25f);
		_properties.SetFloat("_SpecularHighlights", 0f);
		renderer.SetPropertyBlock(_properties);
	}
}
