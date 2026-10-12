using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class LoseLavaNode : MonoBehaviour
{
	[SerializeField] private TMP_Text _progressText;
	[SerializeField] private TMP_Text _descText;

	private void Awake()
	{
		if (_progressText == null)
		{
			_progressText = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "ProgressText");
		}
		if (_descText == null)
		{
			_descText = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Desc");
		}
	}

	private void Start()
	{
		StartAsync().Forget();
	}

	public async UniTaskVoid StartAsync()
	{
		if (Managers.Lava != null)
		{
			if (_progressText != null)
			{
				_progressText.text = $"{Managers.Lava.GetRecord()}/{Managers.Lava.GetLevelGoal()}";
			}

			if (_descText != null)
			{
				// Native GetTitleI18n resolves EventLava before formatting Language/Fail-Desc2.
				string title;
				switch (Managers.Lava.GetTitleKey())
				{
					case "Lava-BouncyTitle": title = "BOUNCY RUSH"; break;
					case "Lava-PillarsTitle": title = "PILLAR JUMPING"; break;
					default: title = "CUBE JUMPING"; break;
				}
				_descText.text = string.Format("You will fail the <color=#C31439>{0}!</color> ", title);
			}
		}

		await UniTask.CompletedTask;
	}
}
