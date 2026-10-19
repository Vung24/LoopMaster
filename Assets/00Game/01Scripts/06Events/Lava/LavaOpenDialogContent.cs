using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Lava
{
	public class LavaOpenDialogContent : DialogContent
	{
		[SerializeField] private Button _closeButton;
		[SerializeField] private Button _startBtn;
		[SerializeField] private TMP_Text _contentText;

		public static UniTask<LavaOpenDialogContent> Show(Transform parent) => EventPrefabUI.Show<LavaOpenDialogContent>(parent, "LavaOpenDialog");



		public static async UniTask ShowWithDialog(Transform parent)
		{
			var dialog = await Show(parent);
			await UniTask.WaitUntil(() => dialog == null || dialog.Result != RESULT_UNKNOWN);
		}

		protected override void Awake()
		{
			base.Awake();
			if (_closeButton == null)
			{
				_closeButton = HierarchyReferenceCache.ChildComponent<Button>(transform, "CloseButton");
			}
			if (_startBtn == null)
			{
				_startBtn = HierarchyReferenceCache.ChildComponent<Button>(transform, "StartBtn");
			}
			if (_contentText == null)
			{
				_contentText = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Content");
			}
		}

		protected override void Start()
		{
			base.Start();

			if (_contentText != null && Managers.Lava != null)
			{
				int levelGoal = Managers.Lava.GetLevelGoal();
				_contentText.text = $"Win {levelGoal} consecutive levels to reach the final platform and share the prize.";
			}

			if (_startBtn != null)
			{
				BreathAnim breath = _startBtn.gameObject.GetOrAddComponent<BreathAnim>();
				breath?.Play(0f, 3f);
				_startBtn.onClick.AddListener(OnClickStartBtn);
			}

			if (_closeButton != null)
			{
				_closeButton.onClick.AddListener(() => Dismiss());
			}

			if (Managers.Lava != null && !Managers.Lava.HasShownTutorial())
			{
				ShowTutorial().Forget();
			}
		}

		private async UniTask ShowTutorial()
		{
			if (Managers.Lava != null)
			{
				Managers.Lava.OnTutorialShown();
			}
			await UniTask.CompletedTask;
		}

		private void OnClickStartBtn()
		{
			if (Result != RESULT_UNKNOWN || Managers.Lava == null || !Managers.Lava.IsWaitingStart()) return;
			if (Managers.Lava != null)
			{
				Managers.Lava.StartEvent();
			}
			Dismiss(1);
		}
	}
}
