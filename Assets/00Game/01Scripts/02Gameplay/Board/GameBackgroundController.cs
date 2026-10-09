using UnityEngine;
public class GameBackgroundController : MonoBehaviour
{
	[SerializeField] private Material _shadowMaterial;
	[SerializeField] private float _groundHeight = -0.02f;
	private GameObject _shadowSurface;
	private Mesh _shadowMesh;

	public void Bind(Camera gameplayCamera)
	{
		if (gameplayCamera == null || (gameplayCamera.cullingMask & 1) == 0) return;
		if (_shadowSurface != null) return;
		if (_shadowMaterial == null) _shadowMaterial = Resources.Load<Material>("GameplayBackgroundShadow");
		if (_shadowMaterial == null) return;
		_shadowSurface = new GameObject("BackgroundShadowReceiver", typeof(MeshFilter), typeof(MeshRenderer));
		UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_shadowSurface, gameObject.scene);
		_shadowSurface.transform.position = new Vector3(0f, _groundHeight, 0f);
		_shadowMesh = new Mesh
		{
			name = "BackgroundShadowSurface",
			vertices = new[] { new Vector3(-50, 0, -50), new Vector3(-50, 0, 50), new Vector3(50, 0, 50), new Vector3(50, 0, -50) },
			normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
			triangles = new[] { 0, 1, 2, 0, 2, 3 }
		};
		_shadowMesh.RecalculateBounds();
		_shadowSurface.GetComponent<MeshFilter>().sharedMesh = _shadowMesh;
		MeshRenderer renderer = _shadowSurface.GetComponent<MeshRenderer>();
		renderer.sharedMaterial = _shadowMaterial;
		renderer.receiveShadows = true;
		// The ground depth pass is needed for Unity's directional shadow resolve.
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
	}

	private void OnEnable()
	{
		if (_shadowSurface != null) _shadowSurface.SetActive(true);
	}

	private void OnDisable()
	{
		if (_shadowSurface != null) _shadowSurface.SetActive(false);
	}

	private void OnDestroy()
	{
		if (_shadowSurface != null) Destroy(_shadowSurface);
		if (_shadowMesh != null) Destroy(_shadowMesh);
	}
}
