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
		await UniTask.Yield();
		if (Managers.StreakLine != null && _desc != null)
		{
			int curr = Managers.StreakLine.GetCurrLevel();
			_desc.SetLocalizedString("Language", "Fail-Desc2", "StreakLine-Title2");
			gameObject.SetActive(curr > 0);
		}
	}
}