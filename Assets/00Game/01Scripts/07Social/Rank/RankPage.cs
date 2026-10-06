using UnityEngine;

public class RankPage : MonoBehaviour
{
	[SerializeField] private GameObject _noNetwork;
	[SerializeField] private GameObject _loading;
	private void Awake()
	{
		if (_noNetwork != null) _noNetwork.SetActive(true);
		if (_loading != null) _loading.SetActive(false);
	}
}
