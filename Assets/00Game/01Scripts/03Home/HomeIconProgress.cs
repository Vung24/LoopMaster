using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Snui
{
	public class HomeIconProgress : MonoBehaviour
	{
		[SerializeField] private ProgressBar _progressBar;

		private void Awake()
		{
			if (_progressBar == null)
			{
				_progressBar = GetComponentInChildren<ProgressBar>(true);
			}
		}

		public static async UniTask<HomeIconProgress> Create(Transform parent)
		{
			GameObject prefab = Resources.Load<GameObject>("Events/Common/HomeIconProgress");
			if (prefab == null)
			{
				prefab = Resources.Load<GameObject>("UI/Common/HomeIconProgress");
			}
			if (prefab == null)
			{
				prefab = PrefabLibrary.Current?.HomeIconProgressPrefab;
			}
			if (prefab != null)
			{
				GameObject go = Instantiate(prefab, parent);
				return go.GetComponent<HomeIconProgress>();
			}
			await UniTask.CompletedTask;
			return null;
		}

		public void SetProgress(int current, int total)
		{
			if (_progressBar != null)
			{
				_progressBar.SetPercent(current, total);
			}
		}

		public Tween IncreaseProgress(int count, float duration = 1f)
		{
			if (_progressBar != null)
			{
				return _progressBar.IncreaseTo(count, duration);
			}
			return null;
		}
	}
}
