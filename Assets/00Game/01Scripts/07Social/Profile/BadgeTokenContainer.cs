using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui.Profile
{
	public class BadgeTokenContainer : MonoBehaviour
	{
		public const string None = "none";

		private string _token;
		private GameObject _loadedToken;

		public static bool IsNone(string token)
		{
			return string.IsNullOrEmpty(token) || string.Equals(token, None, StringComparison.OrdinalIgnoreCase);
		}

		public void SetToken(string token, bool isLarge = false, bool isLocked = false)
		{
			if (string.Equals(_token, token, StringComparison.Ordinal))
			{
				return;
			}

			_token = token;

			if (_loadedToken != null)
			{
				Destroy(_loadedToken);
				_loadedToken = null;
			}

			if (!IsNone(token))
			{
				RefreshTokenPrefab(token, isLarge, isLocked).Forget();
			}
		}

		private async UniTaskVoid RefreshTokenPrefab(string token, bool isLarge = false, bool isLocked = false)
		{
			string path = $"Profile/Badge/{token}";
			GameObject prefab = Resources.Load<GameObject>(path);
			if (prefab != null)
			{
				if (this == null || _token != token) return;

				_loadedToken = Instantiate(prefab, transform);
				_loadedToken.transform.localPosition = Vector3.zero;
				_loadedToken.transform.localScale = isLarge ? Vector3.one * 1.2f : Vector3.one;
			}

			await UniTask.CompletedTask;
		}
	}
}