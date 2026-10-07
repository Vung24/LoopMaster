using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class GameLoading : LoadingPanel
{
	private TMP_Text _animText;
	private float _timer;
	private int _animStage = 1;

	public static async UniTask ShowLoading()
	{
		if (Current != null)
		{
			return;
		}

		GameObject instance = null;
		try
		{
			var handle = Addressables.InstantiateAsync("GameLoading");
			instance = await handle.Task;
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning($"[GameLoading] Addressables failed to instantiate 'GameLoading': {ex.Message}. Trying fallback.");
		}

		if (instance == null)
		{
			var prefab = Resources.Load<GameObject>("Common/Prefab/GameLoading");
#if UNITY_EDITOR
			if (prefab == null)
			{
				prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/00Game/AAResources/Common/Prefab/GameLoading.prefab");
			}
#endif
			if (prefab != null)
			{
				instance = Instantiate(prefab);
			}
		}

		if (instance != null)
		{
			var loading = instance.GetComponent<GameLoading>();
			if (loading != null)
			{
				await loading.Show();
			}
		}
	}

	protected override float GetMinLoadingTime()
	{
		return 0.3f;
	}

	protected override void Awake()
	{
		base.Awake();
		if (_animText == null)
		{
			_animText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "AnimText");
		}
	}

	private void Start()
	{
		if (_canvasGroup != null)
		{
			_canvasGroup.alpha = 0f;
		}
	}

	private void Update()
	{
		_timer += Time.deltaTime;
		if (_timer < 0.5f) return;
		_timer = 0f;

		string dots = "";
		for (int i = 0; i < _animStage; i++)
		{
			dots += ".";
		}

		if (_animStage >= 3)
		{
			_animStage = 0;
		}
		else
		{
			_animStage++;
		}

		if (_animText != null)
		{
			_animText.SetText(dots);
		}
	}

	protected override async UniTask PlayShowAnim()
	{
		if (_canvasGroup != null)
		{
			_canvasGroup.alpha = 0f;
			await _canvasGroup.DOFade(1f, 0.2f).SetLink(gameObject).AsyncWaitForCompletion();
		}
	}

	protected override async UniTask PlayDismissAnim()
	{
		if (_canvasGroup != null)
		{
			await _canvasGroup.DOFade(0f, 0.2f).SetLink(gameObject).AsyncWaitForCompletion();
		}
		if (Current == this)
		{
			Current = null;
		}
		if (!Addressables.ReleaseInstance(gameObject))
		{
			Destroy(gameObject);
		}
	}
}
