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
		private LavaDetailDialog _dialog;

		private void Awake()
		{
			_btn = GetComponent<Button>();
		}

		private void Start()
		{
			_dialog = GetComponentInParent<LavaDetailDialog>();
			if (Managers.Lava != null)
			{
				_record = Managers.Lava.GetRecord();
			}

			if (HintType == LavaHintType.Player)
			{
				Transform plane = _dialog != null ? _dialog.GetCurrPlane()?.GetPlaneNode() : null;
				if (plane != null && transform.parent != null)
					transform.localPosition = transform.parent.InverseTransformPoint(plane.position);
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
			if (_dialog != null && _dialog.IsPlayAnimation) return;
			switch (HintType)
			{
				case LavaHintType.Reward:
					ShowLavaHint(true, "Lava-TutorialDesc3", null).Forget();
					break;
				case LavaHintType.Plane:
					ShowLavaHint(false, "Lava-TutorialDesc2", null).Forget();
					break;
				case LavaHintType.Player:
					ShowLavaHint(false, "Lava-TutorialDesc1", new object[] { Managers.Lava != null ? Managers.Lava.GetRemainingPlayers() : 0 }).Forget();
					break;
			}
		}

		private async UniTask ShowLavaHint(bool flipY, string key, object[] arguments)
		{
			GameObject prefab = LavaSettings.Instance != null ? LavaSettings.Instance.HintPrefab : null;
			if (prefab == null)
			{
				prefab = Resources.Load<GameObject>("Lava/Prefab/LavaHint");
			}

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
