using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Lava
{
	public class LavaHintButton : MonoBehaviour
	{
		public enum LavaHintType
		{
			Reward = 0,
			Plane = 1,
			Player = 2
		}

		public LavaHintType HintType;
		public int PlaneIndex;
		private Button _btn;
		private int _record;

		private void Awake()
		{
			_btn = GetComponent<Button>();
		}

		private void Start()
		{
			if (HintType == LavaHintType.Player)
			{
				// Position relative to player
			}
			else if (HintType == LavaHintType.Plane)
			{
				if (PlaneIndex < _record)
				{
					gameObject.SetActive(false);
					return;
				}
			}

			if (_btn != null)
			{
				_btn.onClick.AddListener(OnClickButton);
			}
		}

		private void OnClickButton()
		{
			switch (HintType)
			{
				case LavaHintType.Reward:
					ShowLavaHint(true, "LavaRewardHint", null).Forget();
					break;
				case LavaHintType.Plane:
					ShowLavaHint(false, "LavaPlaneHint", null).Forget();
					break;
				case LavaHintType.Player:
					ShowLavaHint(false, "LavaPlayerHint", null).Forget();
					break;
			}
		}

		private async UniTask ShowLavaHint(bool flipY, string key, object[] arguments)
		{
			GameObject prefab = Resources.Load<GameObject>("Lava/Prefab/LavaHint");
			GameObject instance = null;
			if (prefab != null)
			{
				instance = Instantiate(prefab, transform.parent);
			}
			if (instance != null)
			{
				LavaHint hint = instance.GetComponent<LavaHint>();
				if (hint != null)
				{
					hint.SetLocalPosition(transform.localPosition, flipY);
					hint.SetStringKey(key, arguments);
				}
			}
			await UniTask.CompletedTask;
		}
	}
}