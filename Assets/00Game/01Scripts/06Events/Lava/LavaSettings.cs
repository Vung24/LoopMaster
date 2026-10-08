using UnityEngine;

namespace Snui.Lava
{
	[CreateAssetMenu(fileName = "LavaSettings", menuName = "Settings/LavaSettings")]
	public class LavaSettings : SingletonScriptableObject<LavaSettings>
	{
		[SerializeField] private GameObject _iconPrefab;
		[SerializeField] private GameObject _openDialogPrefab;
		[SerializeField] private GameObject _startDialogPrefab;
		[SerializeField] private GameObject _detailDialogPrefab;
		[SerializeField] private GameObject _winDialogPrefab;
		[SerializeField] private GameObject _tipDialogPrefab;
		[SerializeField] private GameObject _cdDialogPrefab;
		[SerializeField] private GameObject _hintPrefab;

		public GameObject IconPrefab => _iconPrefab;
		public GameObject OpenDialogPrefab => _openDialogPrefab;
		public GameObject StartDialogPrefab => _startDialogPrefab;
		public GameObject DetailDialogPrefab => _detailDialogPrefab;
		public GameObject WinDialogPrefab => _winDialogPrefab;
		public GameObject TipDialogPrefab => _tipDialogPrefab;
		public GameObject CdDialogPrefab => _cdDialogPrefab;
		public GameObject HintPrefab => _hintPrefab;
	}
}