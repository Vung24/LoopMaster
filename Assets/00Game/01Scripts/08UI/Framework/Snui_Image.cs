using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	[RequireComponent(typeof(Image))]
	public sealed class Snui_Image : MonoBehaviour
	{
		private Image image;
		public Image Image { get { if (image == null) image = GetComponent<Image>(); return image; } }

		public async UniTaskVoid SetSpriteAsync(string path, Action<Sprite> callback = null)
		{
			if (string.IsNullOrWhiteSpace(path)) { callback?.Invoke(null); return; }
			ResourceRequest request = Resources.LoadAsync<Sprite>(path);
			await request;
			SetSprite(request.asset as Sprite, callback);
		}

		public void SetSprite(Sprite sprite, Action<Sprite> callback = null)
		{
			if (Image != null)
			{
				Image.sprite = sprite;
				Image.enabled = sprite != null;
				if (sprite != null) Image.SetAllDirty();
			}
			callback?.Invoke(sprite);
		}
	}
}
