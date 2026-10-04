using UnityEngine;

namespace Sugar.UI
{
	public class TableViewCell : MonoBehaviour
	{
		[HideInInspector] public string TypeIdentifier;
		[HideInInspector] public RectTransform RectTransform;

		protected virtual void Awake()
		{
			RectTransform = GetComponent<RectTransform>();
		}
	}
}
