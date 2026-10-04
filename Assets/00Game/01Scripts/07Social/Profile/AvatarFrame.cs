using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui.Profile
{
	public class AvatarFrame : MonoBehaviour
	{
		public const string DEFAULT = "default";
		public const string PASS_VIP = "pass";

		private string _currName;
		private AvatarFrameContent _avatarFrameContent;

		private void Awake()
		{
			if (_avatarFrameContent == null)
			{
				_avatarFrameContent = GetComponentInChildren<AvatarFrameContent>(true);
			}
		}

		public void SetAvatarFrame(string name, bool isLock = false)
		{
			UpdateFrame(name, isLock).Forget();
		}

		public async UniTask UpdateFrame(string name, bool isLock = false)
		{
			if (string.IsNullOrEmpty(name))
			{
				name = DEFAULT;
			}

			if (_currName == name && _avatarFrameContent != null)
			{
				if (isLock)
				{
					_avatarFrameContent.SetLock();
				}
				return;
			}

			_currName = name;

			if (_avatarFrameContent != null)
			{
				Destroy(_avatarFrameContent.gameObject);
				_avatarFrameContent = null;
			}

			string path = $"Profile/AvatarFrame/{name}";
			GameObject prefab = Resources.Load<GameObject>(path);
			if (prefab != null)
			{
				GameObject instance = Instantiate(prefab, transform);
				instance.transform.localPosition = Vector3.zero;
				instance.transform.localScale = Vector3.one;
				_avatarFrameContent = instance.GetComponent<AvatarFrameContent>();
				if (_avatarFrameContent != null && isLock)
				{
					_avatarFrameContent.SetLock();
				}
			}

			await UniTask.CompletedTask;
		}
	}
}