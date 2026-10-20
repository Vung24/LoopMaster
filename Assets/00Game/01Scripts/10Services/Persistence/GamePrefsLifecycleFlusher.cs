using UnityEngine;

public sealed class GamePrefsLifecycleFlusher : MonoBehaviour
{
    private static GamePrefsLifecycleFlusher _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Ensure()
    {
        if (_instance != null) return;

        GameObject host = new GameObject(nameof(GamePrefsLifecycleFlusher));
        DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        _instance = host.AddComponent<GamePrefsLifecycleFlusher>();
    }

    private void LateUpdate()
    {
        if (GamePrefs.IsDirty)
        {
            GamePrefs.SaveImmediate();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && GamePrefs.IsDirty)
        {
            GamePrefs.SaveImmediate();
        }
    }

    private void OnApplicationQuit()
    {
        if (GamePrefs.IsDirty)
        {
            GamePrefs.SaveImmediate();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        if (GamePrefs.IsDirty)
        {
            GamePrefs.SaveImmediate();
        }
    }
}

