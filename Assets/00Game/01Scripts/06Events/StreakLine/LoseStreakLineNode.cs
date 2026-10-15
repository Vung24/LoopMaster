using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class LoseStreakLineNode : MonoBehaviour
{
	[SerializeField]
	private TMP_Text _desc;

	private void Awake()
	{
		if (_desc == null)
		{
			_desc = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Desc");
			if (_desc == null)
			{
				_desc = GetComponentInChildren<TMP_Text>(true);
			}
		}
	}

	private void Start()
	{
		UpdateDesc().Forget();
	}

	public async UniTaskVoid UpdateDesc()
	{
		if (_desc != null)
		{
			// English values from Language/Fail-Desc2 and StreakLine-Title2.
			_desc.text = "You will fail the <color=#C31439>SKY RISE!</color> ";
		}
		await UniTask.CompletedTask;
	}
}
