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
			_descText = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "DescText");
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
				string titleKey = Managers.Lava.GetTitleKey();
				_descText.SetLocalizedString("Language", "Fail-Desc2", new object[] { titleKey });
			}
		}

		await UniTask.CompletedTask;
	}
}
