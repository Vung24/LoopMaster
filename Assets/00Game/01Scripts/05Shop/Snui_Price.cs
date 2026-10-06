using TMPro;
using UnityEngine;

namespace Snui
{
	[RequireComponent(typeof(TMP_Text))]
	public class Snui_Price : MonoBehaviour
	{
		private string _id;
		[SerializeField] private TMP_Text _tmpText;
		[SerializeField] private Transform _loading;

		private void Awake()
		{
			if (_tmpText == null) _tmpText = GetComponent<TMP_Text>();
			if (_loading == null) _loading = HierarchyReferenceCache.Child(transform, "Loading");
			if (_loading != null)
			{
				_loading.gameObject.SetActive(false);
			}
		}

		public void SetId(string id)
		{
			_id = id;
			if (_loading != null) _loading.gameObject.SetActive(false);
			if (_tmpText != null && !string.IsNullOrEmpty(id))
			{
				_tmpText.text = "$0.99";
			}
		}

		public void SetIdWithDiscount(string id, float discount)
		{
			SetId(id);
		}

		public void SetPrice(string id)
		{
			SetId(id);
		}
	}
}