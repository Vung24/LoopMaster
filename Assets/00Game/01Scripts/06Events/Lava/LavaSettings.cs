using UnityEngine;

namespace Snui.Lava
{
	[CreateAssetMenu(fileName = "LavaSettings", menuName = "Settings/LavaSettings")]
	public class LavaSettings : SingletonScriptableObject<LavaSettings>
	{
		[SerializeField] private GameObject _iconPrefab;
		[SerializeField] private GameObject _hintPrefab;
		[SerializeField] private Sprite[] _avatarSprites;
		[SerializeField] private Sprite _avatarFrameSprite;

		public GameObject IconPrefab => _iconPrefab;
		public GameObject HintPrefab => _hintPrefab;
		public Sprite AvatarFrameSprite => _avatarFrameSprite;
		public Sprite GetAvatarSprite(int index) => _avatarSprites != null && _avatarSprites.Length > 0
			? _avatarSprites[Mathf.Abs(index) % _avatarSprites.Length] : null;
	}
}
