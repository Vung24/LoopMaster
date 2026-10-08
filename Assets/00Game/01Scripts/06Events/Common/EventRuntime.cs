using Snui;
using Snui.StreakLine;
using UnityEngine;

// Owns the offline events across Home/Game scene changes.
public sealed class EventRuntime : MonoBehaviour
{
    private static EventRuntime _current;

    public static void EnsureInitialized()
    {
        if (_current == null)
        {
            var owner = new GameObject("Events");
            _current = owner.AddComponent<EventRuntime>();
            DontDestroyOnLoad(owner);
        }
        Managers.Event = Ensure(Managers.Event);
        Managers.Pass = Ensure<BasePassManager, PassManager>(Managers.Pass);
        Managers.StreakLine = Ensure<BaseStreakLineManager, StreakLineManager>(Managers.StreakLine);
        Managers.Lava = Ensure<Snui.Lava.BaseLavaManager, LavaManager>(Managers.Lava);
    }

    private static T Ensure<T>(T manager) where T : BaseManager => Ensure<T, T>(manager);

    private static TBase Ensure<TBase, T>(TBase manager) where TBase : BaseManager where T : TBase
    {
        if (manager == null) manager = _current.gameObject.GetComponent<T>() ?? _current.gameObject.AddComponent<T>();
        if (manager.status != ManagerStatus.Started) manager.Startup();
        return manager;
    }

    public static void BeforeEnterHome()
    {
        EnsureInitialized();
        Managers.Pass.EventBeforeEnterHome();
        Managers.StreakLine.EventBeforeEnterHome();
		Managers.StreakLine.SynchronizeTempLevel();
        Managers.Lava.EventBeforeEnterHome();
    }
}
