using Cysharp.Threading.Tasks;
using Sugar.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Profile
{
	public class ProfileAvatarCell : MonoBehaviour
	{
		[SerializeField] private UrlImage _urlImage;
		[SerializeField] private Image _frame;
		[SerializeField] private Transform _selectTick;
		[SerializeField] private Sprite _frameSelectSprite;
		[SerializeField] private Sprite _frameUnselectSprite;

		public Button Button => GetComponent<Button>();

		public UniTask SetUrlSpriteAsync(string url)
		{
			if (_urlImage != null)
			{
				return _urlImage.SetUrlSpriteAsync(url);
			}
			return UniTask.CompletedTask;
		}

		public void SetSelected(bool value)
		{
			if (_frame != null)
			{
				_frame.sprite = value ? _frameSelectSprite : _frameUnselectSprite;
			}
			if (_selectTick != null)
			{
				_selectTick.gameObject.SetActive(value);
			}
		}
	}
}