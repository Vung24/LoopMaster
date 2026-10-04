using Cysharp.Threading.Tasks;
using Sugar.UI;
using UnityEngine;

namespace Snui.Team
{
	public class TeamLogo : MonoBehaviour
	{
		[SerializeField] private UrlImage _urlImage;
		private string _logo;

		private void Awake()
		{
			if (_urlImage == null)
			{
				_urlImage = GetComponentInChildren<UrlImage>(true);
			}
		}

		public void SetLogo(string logo)
		{
			_logo = logo;
			if (_urlImage != null && !string.IsNullOrEmpty(logo))
			{
				_urlImage.SetUrlSpriteAsync(logo).Forget();
			}
		}

		public string GetLogo()
		{
			return _logo;
		}
	}
}