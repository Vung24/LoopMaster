using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Sugar.UI
{
	[AddComponentMenu("UI/UrlImage")]
	public class UrlImage : Image
	{
		[SerializeField] private string _url;

		public string Url => _url;

		public async UniTask SetUrlSpriteAsync(string url, bool nativeSize = false)
		{
			_url = url;
			if (string.IsNullOrEmpty(url))
			{
				sprite = null;
				return;
			}

			Sprite loadedSprite = Resources.Load<Sprite>(url);
			if (loadedSprite != null)
			{
				sprite = loadedSprite;
				if (nativeSize)
				{
					SetNativeSize();
				}
				return;
			}

			await UniTask.CompletedTask;
		}
	}
}
