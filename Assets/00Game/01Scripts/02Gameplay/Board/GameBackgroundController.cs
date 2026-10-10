using UnityEngine;
public class GameBackgroundController : MonoBehaviour
{
	[SerializeField] private MeshRenderer _shadowRenderer;
	[SerializeField] private Camera _gameplayCamera;

	public void Bind(Camera gameplayCamera)
	{
		_gameplayCamera = gameplayCamera;
		RefreshVisibility();
	}

	private void RefreshVisibility()
	{
		if (_shadowRenderer == null) return;
		_shadowRenderer.enabled = isActiveAndEnabled && _gameplayCamera != null
			&& (_gameplayCamera.cullingMask & (1 << _shadowRenderer.gameObject.layer)) != 0;
	}

	private void OnEnable()
	{
		RefreshVisibility();
	}

	private void OnDisable()
	{
		if (_shadowRenderer != null) _shadowRenderer.enabled = false;
	}

	private void OnDestroy()
	{
		if (_shadowRenderer != null) _shadowRenderer.enabled = false;
	}
}
