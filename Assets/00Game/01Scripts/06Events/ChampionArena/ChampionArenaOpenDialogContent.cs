using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.ChampionArena
{
	public class ChampionArenaOpenDialogContent : DialogContent
	{
		[SerializeField]
		private AudioClip _eventOpenClip;
		[SerializeField]
		private Button _continueBtn;

		public static UniTask<ChampionArenaOpenDialogContent> Show(Transform parent) => EventPrefabUI.Show<ChampionArenaOpenDialogContent>(parent, "ChampionArenaOpenDialog");

		protected override void Awake()
		{
			base.Awake();
			if (_continueBtn == null)
			{
				var cb = HierarchyReferenceCache.Descendant(transform, "ContinueBtn");
				if (cb != null) _continueBtn = cb.GetComponent<Button>();
			}
			if (_continueBtn != null)
			{
				_continueBtn.onClick.AddListener(OnClickCloseButton);
			}
		}

		protected override void Start()
		{
			base.Start();
			if (_eventOpenClip != null && AudioManager.Current != null)
			{
				AudioManager.Current.PlayOneShot(_eventOpenClip, gameObject);
			}
			Managers.ChampionArena?.OnOpenDialogShown();
		}

		protected override void OnClickCloseButton()
		{
			if (Managers.ChampionArena != null && Managers.ChampionArena.CanStartEvent())
			{
				Managers.ChampionArena.StartEvent();
			}
			base.OnClickCloseButton();
		}
	}
}