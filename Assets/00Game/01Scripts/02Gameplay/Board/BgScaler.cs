using UnityEngine;

public class BgScaler : MonoBehaviour
{
	private void Start()
	{
		Camera camera = Camera.main;
		SpriteRenderer sprite = GetComponent<SpriteRenderer>();
		if (camera == null || sprite == null || sprite.sprite == null || !camera.orthographic) return;

		Vector2 spriteSize = sprite.sprite.bounds.size;
		if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;
		float worldHeight = camera.orthographicSize * 2f;
		float worldWidth = worldHeight * camera.aspect;
		float scale = Mathf.Max(worldWidth / spriteSize.x, worldHeight / spriteSize.y);
		transform.localScale = new Vector3(scale, scale, transform.localScale.z);
	}
}
