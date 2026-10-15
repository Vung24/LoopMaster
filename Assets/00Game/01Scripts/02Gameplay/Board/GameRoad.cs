using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GameRoad : MonoBehaviour
{
	private const int SectionVertCount = RoadGeometry.SectionVertexCount;
	[SerializeField] private Material _roadWallMat;
	[SerializeField] private Material _roadMat;
	[SerializeField] private Material _roadWallMatLight;
	[SerializeField] private Material _roadMatLight;
	private Mesh _pathRoadMesh;
	private MeshFilter _pathMeshFilter;
	private MeshRenderer _pathMeshRenderer;
	private GamePath _path;
	private Color _originWallColor = Color.white;
	private MaterialPropertyBlock _wallPropertyBlock;
	private Color _lastWallColor;
	private bool _hasWallColor;
	private readonly List<Vector3> _sampledPoints = new List<Vector3>();

	public IReadOnlyList<Vector3> PathPoints => _sampledPoints;

	private void Awake()
	{
		_pathMeshFilter = GetComponent<MeshFilter>();
		_pathMeshRenderer = GetComponent<MeshRenderer>();
		_pathRoadMesh = new Mesh { name = "GameRoad" };
		_pathRoadMesh.MarkDynamic();
		_pathMeshFilter.sharedMesh = _pathRoadMesh;
		InitUI();
	}

	public void InitCurve(GamePath path)
	{
		_path = path;
		UpdatePathMesh();
	}

	public void InitUI()
	{
		if (_pathMeshRenderer == null) _pathMeshRenderer = GetComponent<MeshRenderer>();
		Material road = _roadMat != null ? _roadMat : _roadMatLight;
		Material wall = _roadWallMat != null ? _roadWallMat : _roadWallMatLight;
		if (_pathMeshRenderer != null)
		{
			_pathMeshRenderer.sharedMaterials = new[] { road, wall };
			_hasWallColor = false;
			if (wall != null)
			{
				if (wall.HasProperty("_BaseColor")) _originWallColor = wall.GetColor("_BaseColor");
				else if (wall.HasProperty("_Color")) _originWallColor = wall.GetColor("_Color");
			}
			SetWallMatColor(_originWallColor);
		}
	}

	public void SetWarningByProgress(int progress)
	{
		float warning = Mathf.Clamp(progress, 0, 100) / 150f;
		SetWallMatColor(new Color(_originWallColor.r + warning * 0.5f,
			_originWallColor.g * (1f - warning), _originWallColor.b * (1f - warning), 1f));
	}

	private void SetWallMatColor(Color color)
	{
		if (_pathMeshRenderer == null) return;
		if (_hasWallColor && _lastWallColor == color) return;
		if (_wallPropertyBlock == null) _wallPropertyBlock = new MaterialPropertyBlock();
		_pathMeshRenderer.GetPropertyBlock(_wallPropertyBlock, 1);
		_wallPropertyBlock.SetVector("_BaseColor", color);
		_wallPropertyBlock.SetColor("_Color", color);
		_pathMeshRenderer.SetPropertyBlock(_wallPropertyBlock, 1);
		_lastWallColor = color;
		_hasWallColor = true;
	}

	private void UpdatePathMesh()
	{
		_sampledPoints.Clear();
		if (_path == null || _path.ControlPoints == null || _path.ControlPoints.Count < 2)
		{
			if (_pathRoadMesh != null) _pathRoadMesh.Clear();
			return;
		}

		int sampleCount = Mathf.Max(2, _path.ControlPoints.Count * 30);
		var tangents = new List<Vector3>(sampleCount + 1);
		for (int i = 0; i <= sampleCount; i++)
		{
			float progress = i / (float)sampleCount;
			_sampledPoints.Add(EvaluatePath(progress));
			tangents.Add(EvaluatePathTangent(progress));
		}
		CreateRoadMesh(_sampledPoints, tangents);
	}

	private Vector3 EvaluatePath(float progress)
	{
		int segmentCount = _path.ControlPoints.Count - 1;
		float scaled = Mathf.Clamp01(progress) * segmentCount;
		int segment = Mathf.Min(Mathf.FloorToInt(scaled), segmentCount - 1);
		float t = segment == segmentCount - 1 && progress >= 1f ? 1f : scaled - segment;
		ControlPoint current = _path.ControlPoints[segment];
		ControlPoint next = _path.ControlPoints[segment + 1];
		float inverse = 1f - t;
		return inverse * inverse * inverse * current.Position +
			3f * inverse * inverse * t * current.OutTangent +
			3f * inverse * t * t * next.InTangent + t * t * t * next.Position;
	}

	private Vector3 EvaluatePathTangent(float progress)
	{
		int segmentCount = _path.ControlPoints.Count - 1;
		float scaled = Mathf.Clamp01(progress) * segmentCount;
		int segment = Mathf.Min(Mathf.FloorToInt(scaled), segmentCount - 1);
		float t = segment == segmentCount - 1 && progress >= 1f ? 1f : scaled - segment;
		ControlPoint current = _path.ControlPoints[segment];
		ControlPoint next = _path.ControlPoints[segment + 1];
		float inverse = 1f - t;
		Vector3 tangent = 3f * inverse * inverse * (current.OutTangent - current.Position) +
			6f * inverse * t * (next.InTangent - current.OutTangent) +
			3f * t * t * (next.Position - next.InTangent);
		if (tangent.sqrMagnitude > 0.0001f) return tangent.normalized;
		return (next.Position - current.Position).normalized;
	}

	private void CreateRoadMesh(List<Vector3> pathPoints, List<Vector3> tangents)
	{
		if (_pathRoadMesh == null) Awake();
		int sections = pathPoints.Count;
		if (sections < 2) { _pathRoadMesh.Clear(); return; }
		var vertices = new Vector3[sections * SectionVertCount];
		var uvs = new Vector2[vertices.Length];
		for (int i = 0; i < sections; i++)
		{
			AddVertWithPoint(vertices, pathPoints[i], tangents[i], i);
			AddVertUv(uvs, i * SectionVertCount);
		}

		uvs[1] = new Vector2(1f, 0f);  

		int links = _path != null && _path.IsCircle > 0 ? sections : sections - 1;
		var roadTriangles = new int[links * 6];
		var wallTriangles = new int[links * 60];
		for (int i = 0; i < links; i++)
		{
			AddRoadTris(roadTriangles, i * 6, i);
			AddWallTris(wallTriangles, i * 60, i);
		}

		_pathRoadMesh.Clear();
		_pathRoadMesh.vertices = vertices;
		_pathRoadMesh.uv = uvs;
		_pathRoadMesh.subMeshCount = 2;
		_pathRoadMesh.SetTriangles(roadTriangles, 0);
		_pathRoadMesh.SetTriangles(wallTriangles, 1);
		_pathRoadMesh.RecalculateNormals();
		_pathRoadMesh.RecalculateBounds();
	}

	private void AddVertWithPoint(Vector3[] vertices, Vector3 point, Vector3 forward, int section)
	{
		Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
		if (right.sqrMagnitude < 0.001f) right = Vector3.left;
		int index = section * SectionVertCount;
		for (int i = 0; i < SectionVertCount; i++)
			vertices[index + i] = RoadGeometry.GetVertex(i, point, right);
	}

	private void AddRoadTris(int[] tris, int tIndex, int i)
	{
		int next = (i + 1) % _sampledPoints.Count;
		RoadGeometry.WriteRoadLink(tris, tIndex, i * SectionVertCount, next * SectionVertCount);
	}

	private void AddWallTris(int[] tris, int tIndex, int i)
	{
		int next = (i + 1) % _sampledPoints.Count;
		RoadGeometry.WriteWallLink(tris, tIndex, i * SectionVertCount, next * SectionVertCount);
	}

	private void AddVertUv(Vector2[] uvs, int vertIndex)
	{
		uvs[vertIndex + 1] = new Vector2(1f, 0.5f);
		for (int side = 0; side < 2; side++)
		{
			int first = vertIndex + (side == 0 ? 3 : 9);
			uvs[first] = new Vector2(0.35f, 0.5f);
			uvs[first + 1] = new Vector2(0.45f, 0.5f);
			uvs[first + 2] = new Vector2(0.55f, 0.5f);
			uvs[first + 3] = new Vector2(0.65f, 0.5f);
			uvs[first + 4] = new Vector2(1f, 0.5f);
		}
	}
	private void OnDestroy()
	{
		if (_pathRoadMesh != null) Destroy(_pathRoadMesh);
	}
}
