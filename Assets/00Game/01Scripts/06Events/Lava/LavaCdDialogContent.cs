using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.Lava
{
	public class LavaCdDialogContent : DialogContent
	{
		[SerializeField] private Button _closeButton;
		[SerializeField] private Timer _timer;

		public static UniTask<LavaCdDialogContent> Show(Transform parent) => EventPrefabUI.Show<LavaCdDialogContent>(parent, "LavaCdDialog");



		public static async UniTask ShowWithDialog(Transform parent)
		{
			await Show(parent);
		}

		protected override void Awake()
		{
			base.Awake();
			if (_closeButton == null)
			{
				_closeButton = HierarchyReferenceCache.ChildComponent<Button>(transform, "CloseButton");
			}
			if (_timer == null)
			{
				_timer = HierarchyReferenceCache.ChildComponent<Timer>(transform, "Timer");
			}
		}

		protected override void Start()
		{
			base.Start();

			if (_closeButton != null)
			{
				_closeButton.onClick.AddListener(() => Dismiss());
			}

			if (_timer != null)
			{
				_timer.OnStopped?.AddListener(OnTimerStopped);
				if (Managers.Lava != null)
				{
					CountdownHelper.CountdownFixTime(_timer, Managers.Lava.GetRemainRetryTime());
				}
			}
		}

		private void OnTimerStopped()
		{
			Dismiss();
		}
	}
}