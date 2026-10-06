using UnityEngine;
using UnityEngine.SceneManagement;

public class HeartManager : BaseHeartManager
{
	public static BaseHeartManager EnsureInitialized()
	{
		if (Managers.Heart != null) return Managers.Heart;
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			var scene = SceneManager.GetSceneAt(i);
			if (!scene.isLoaded) continue;
			foreach (var root in scene.GetRootGameObjects())
			{
				var manager = root.GetComponentInChildren<HeartManager>(true);
				if (manager == null) continue;
				Managers.Heart = manager;
				manager.Startup();
				return manager;
			}
		}
		return new GameObject("OfflineHeartManager").AddComponent<HeartManager>();
	}

	private void Awake()
	{
		if (Managers.Heart != null && Managers.Heart != this) { Destroy(this); return; }
		Managers.Heart = this;
		DontDestroyOnLoad(transform.root.gameObject);
		Startup();
	}

	private void OnDestroy()
	{
		if (Managers.Heart == this) Managers.Heart = null;
	}

	public override IHeartHud GetCurrentHeartHud() => null;
	public override bool IsMoreHeartDialogShowRvBtn() => false;
	public override int GetRefillHeartPrice() => 0;
}
