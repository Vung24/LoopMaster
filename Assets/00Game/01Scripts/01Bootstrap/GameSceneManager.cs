using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public enum SceneType
{
	Home = 0,
	Game = 1,
	Splash = 2
}

public enum GameEnterRoute
{
	Splash = 0,
	Home = 1,
	Quit = 2,
	Fail = 3,
	WinNext = 4,
	Deeplink = 5
}

public static class GameSceneManager
{
	public static SceneType CurSceneType { get; set; } = SceneType.Home;
	public static bool OnChangeScene { get; set; }
	private static AsyncOperationHandle<SceneInstance> _homeSceneHandle;

	public static AsyncOperationHandle<SceneInstance> PreloadHomeScene()
	{
		string scenePath = GetScenePath(SceneType.Home);
		try
		{
			_homeSceneHandle = Addressables.LoadSceneAsync(scenePath, LoadSceneMode.Single, false);
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[GameSceneManager] Addressables preload HomeScene failed: {ex.Message}");
		}
		return _homeSceneHandle;
	}

	public static async UniTask EnterGame(GameEnterRoute route, bool withAnimation = true)
	{
		PlayerPrefs.SetInt("LastRouteEnterGame", (int)route);
		PlayerPrefs.Save();
		await TransitionToScene(SceneType.Game, withAnimation);
	}

	public static async UniTask EnterHome(bool withAnimation = true)
	{
		await TransitionToScene(SceneType.Home, withAnimation);
	}

	public static async UniTask ExitGame()
	{
		await TransitionToScene(SceneType.Home, withAnimation: true);
	}

	public static async UniTask RestartGame(GameEnterRoute route)
	{
		await EnterGame(route, withAnimation: true);
	}

	public static async UniTask ReloadScene(GameEnterRoute route)
	{
		if (CurSceneType == SceneType.Home)
		{
			await EnterHome(withAnimation: true);
		}
		else
		{
			await TransitionToScene(CurSceneType, withAnimation: true);
		}
	}

	private static async UniTask TransitionToScene(SceneType sceneType, bool withAnimation = true)
	{
		if (OnChangeScene) return;
		OnChangeScene = true;
		string scenePath = GetScenePath(sceneType);
		try
		{
			DOTween.KillAll(false);

			if (withAnimation)
			{
				await ShowSceneLoading();
			}

			await LoadSceneAndWait(sceneType, scenePath);
			CurSceneType = sceneType;

			await LoadingPanel.TryDismiss();
		}
		finally
		{
			OnChangeScene = false;
		}
	}

	private static async UniTask LoadSceneAndWait(SceneType sceneType, string scenePath)
	{
		if (sceneType == SceneType.Home && _homeSceneHandle.IsValid())
		{
			try
			{
				await _homeSceneHandle.Task;
				if (_homeSceneHandle.Status != AsyncOperationStatus.Succeeded)
				{
					throw _homeSceneHandle.OperationException ?? new InvalidOperationException("HomeScene preload failed.");
				}
				var activateOp = _homeSceneHandle.Result.ActivateAsync();
				await activateOp;
				_homeSceneHandle = default;
				return;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameSceneManager] Activate preloaded HomeScene failed: {ex.Message}. Falling back.");
				_homeSceneHandle = default;
			}
		}

		bool addressablesLoaded = false;
		try
		{
			var handle = Addressables.LoadSceneAsync(scenePath, LoadSceneMode.Single);
			await handle.Task;
			if (handle.Status == AsyncOperationStatus.Succeeded)
			{
				addressablesLoaded = true;
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[GameSceneManager] Addressables load '{scenePath}' failed: {ex.Message}. Trying Build Settings fallback.");
			addressablesLoaded = false;
		}

		if (!addressablesLoaded)
		{
			string sceneName = GetSceneName(sceneType);
			if (!Application.CanStreamedLevelBeLoaded(sceneName))
			{
				throw new InvalidOperationException($"Scene '{sceneName}' could not be loaded through Addressables and is not enabled in Build Settings. Check its Address and rebuild Addressables content.");
			}
			var asyncOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
			if (asyncOp != null)
			{
				await UniTask.WaitUntil(() => asyncOp.isDone);
			}
		}
	}

	public static async UniTask ShowSceneLoading()
	{
		await GameLoading.ShowLoading();
	}

	private static string GetScenePath(SceneType sceneType)
	{
		switch (sceneType)
		{
			case SceneType.Home: return "HomeScene";
			case SceneType.Game: return "GameScene";
			case SceneType.Splash: return "SplashScene";
			default: return "HomeScene";
		}
	}

	private static string GetSceneName(SceneType sceneType)
	{
		switch (sceneType)
		{
			case SceneType.Home: return "HomeScene";
			case SceneType.Game: return "GameScene";
			case SceneType.Splash: return "SplashScene";
			default: return "HomeScene";
		}
	}
}
