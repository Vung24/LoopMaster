using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	[RequireComponent(typeof(Button))]
	public class InfoButton : MonoBehaviour
	{
		private Button _button;
		private Image _image;

		private void Awake()
		{
			_button = GetComponent<Button>();
			_image = GetComponent<Image>();
			SetImageAsync().Forget();
		}

		private async UniTaskVoid SetImageAsync()
		{
			if (_image != null && _image.sprite == null)
			{
				Sprite infoSprite = Resources.Load<Sprite>("UI/Common/btn_info");
				if (infoSprite != null)
				{
					_image.sprite = infoSprite;
				}
			}
			await UniTask.CompletedTask;
		}
	}
}