using TMPro;
using UnityEngine;

namespace Snui
{
	[RequireComponent(typeof(TMP_Text))]
	public class Snui_Price : MonoBehaviour
	{
		private string _id;
		private TMP_Text _tmpText;
		private Transform _loading;

		private void Awake()
		{
			_tmpText = GetComponent<TMP_Text>();
			_loading = transform.Find("Loading");
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