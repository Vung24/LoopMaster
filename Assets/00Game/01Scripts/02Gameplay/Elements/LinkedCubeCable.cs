using System;
using UnityEngine;

public sealed class LinkedCubeCable : MonoBehaviour
{
	[SerializeField] private Mesh _bridgeMesh;
	[SerializeField] private Material _bridgeMaterial;
	[SerializeField] private Vector3 _memberSide = new Vector3(.5f, 0f, .36f);
	private Renderer _bridgeRenderer;
	private MaterialPropertyBlock _properties;
	private GameData _data;
	private LinkedCubePair _pair;
	private Transform _bridge, _firstSocket, _secondSocket;
	private Vector3 _firstAnchor, _secondAnchor;
	private bool _hasFirst, _hasSecond;
	private float _blockedUntil;
	private float _consumedAt = -1f;

	public void Initialize(GameData data)
	{
		_data = data;
		_pair = data.LinkedPair;
		if (_pair == null) { enabled = false; return; }
		if (_bridgeMesh == null || _bridgeMaterial == null)
			throw new InvalidOperationException("Assign the bridge mesh and material on LinkedCubeCable.prefab.");
		_properties = new MaterialPropertyBlock();
		_bridge = CreateRoot("Bridge");
		_bridgeRenderer = CreateMesh("SoftLink", _bridge, _bridgeMesh, _bridgeMaterial,
			new Vector3(0f, -.38f, -.7f), Quaternion.identity, Vector3.one);
		_firstSocket = CreateRoot("Anchor_A");
		_secondSocket = CreateRoot("Anchor_B");
	}

	public void ShowBlocked(LinkedCubeLaunchFailure reason)
	{
		_blockedUntil = Time.time + .55f;
	}

	public void ShowRecoveryNotice()
	{
		if (Snui.CommonToast.Instance != null)
			Snui.CommonToast.Instance.Show("Màn chơi đã được khôi phục về đầu");
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
		UpdateAnchor(_pair.FirstCube, secondCenter, ref _firstAnchor, ref _hasFirst);
		UpdateAnchor(_pair.SecondCube, firstCenter, ref _secondAnchor, ref _hasSecond);
		bool show = fade > 0f && _hasFirst && _hasSecond;
		SetVisible(_bridge, show);
		SetVisible(_firstSocket, show);
		SetVisible(_secondSocket, show);
		if (!show) return;
		Vector3 span = _secondAnchor - _firstAnchor;
		Quaternion rotation = span.sqrMagnitude > .0001f ? Quaternion.FromToRotation(Vector3.right, span.normalized) : Quaternion.identity;
		bool ready = LinkedCubeLaunch.CanBegin(_data, _pair.FirstBox, out _);
		float pulse = 1f;
		if (Time.time < _blockedUntil) pulse += .06f * Mathf.Sin(Time.time * 30f);
		else if (ready) pulse += .015f * Mathf.Sin(Time.time * 4f);
		float thickness = .75f * fade * pulse;
		_bridge.localPosition = (_firstAnchor + _secondAnchor) * .5f;
		_bridge.localRotation = rotation;
		_bridge.localScale = new Vector3(span.magnitude / 1.06f, thickness, thickness);
		_firstSocket.localPosition = _firstAnchor;
		_secondSocket.localPosition = _secondAnchor;
		_firstSocket.localRotation = _secondSocket.localRotation = rotation;
		_firstSocket.localScale = _secondSocket.localScale = Vector3.one * thickness;
		UpdateTint(ready);
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

	private void UpdateAnchor(CubeInfo member, Vector3 partnerCenter, ref Vector3 local, ref bool captured)
	{
		if (member == null || member.Size <= 0 || member.CubeSc == null) return;
		Transform cube = member.CubeSc.transform;
		Vector3 direction = cube.InverseTransformDirection(partnerCenter - cube.position);
		direction.y = 0f;
		if (direction.sqrMagnitude < .0001f) direction = Vector3.right;
		direction.Normalize();
		float xEdge = _memberSide.x / Mathf.Max(.0001f, Mathf.Abs(direction.x));
		float zEdge = _memberSide.z / Mathf.Max(.0001f, Mathf.Abs(direction.z));
		Vector3 side = direction * Mathf.Min(xEdge, zEdge);
		side.y = _memberSide.y;
		local = transform.InverseTransformPoint(cube.TransformPoint(side));
		captured = true;
	}

	private void UpdateTint(bool ready)
	{
		Color color = _bridgeRenderer.sharedMaterial.color;
		if (Time.time < _blockedUntil) color = Color.Lerp(color, new Color(1f, .28f, .2f), .45f);
		else if (!ready && _pair.State == LinkedCubePairState.Pending) color *= .82f;
		_properties.SetColor("_Color", color);
		_bridgeRenderer.SetPropertyBlock(_properties);
	}
}
