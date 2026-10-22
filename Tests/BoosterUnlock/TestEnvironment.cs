// Test doubles for Unity hosting and persistence. Production unlock and inventory code is linked unchanged.
using System;
using System.Collections.Generic;

#if !UNITY_5_3_OR_NEWER
namespace UnityEngine
{
    public class Object
    {
        protected static void Destroy(object value) { }
        protected static void DontDestroyOnLoad(object value) { }
    }
    public class MonoBehaviour : Object
    {
        public Transform transform = new Transform();
    }
    public class ScriptableObject : Object { }
    public class Sprite : Object { }
    public sealed class SerializeField : Attribute { }
    public sealed class Min : Attribute
    {
        public Min(int minimum) { }
    }
    public sealed class TextArea : Attribute
    {
        public TextArea(int minimum, int maximum) { }
    }
    public sealed class CreateAssetMenu : Attribute
    {
        public string fileName;
        public string menuName;
    }
    public static class Debug
    {
        public static void LogError(string message, object context) { throw new Exception(message); }
    }
    public class Transform
    {
        public Transform root => this;
        public GameObject gameObject => new GameObject("");
    }
    public class GameObject
    {
        public GameObject(string name) { }
        public T AddComponent<T>() where T : new() => new T();
    }
    public sealed class DefaultExecutionOrder : Attribute
    {
        public DefaultExecutionOrder(int value) { }
    }
    public sealed class HideInInspector : Attribute { }
    public static class Resources
    {
        public static T Load<T>(string path) where T : class
        {
            if (typeof(T) == typeof(BoosterUnlockConfig)) return TestBoosterConfig.Load() as T;
            return null;
        }
    }
}
namespace UnityEngine.Events
{
    public class UnityEvent<T, U>
    {
        public void Invoke(T first, U second) { }
    }
}
#endif
public enum ManagerStatus { Shutdown, Initializing, Started }
public interface IGameManager { }
public sealed class InventorySettings
{
    public SerializableRewardItem[] InitValues;
}
public sealed class SerializableRewardItem
{
    public string Key;
    public int Count;
}
public sealed class GameController
{
    public static GameController Current;
    public object Data;
    public bool IsEditorPlaytest;
    public int CurrentLevelNumber;
}
public static class GameProgressStore
{
    public static int HighestUnlockedLevel;
}
public static class GamePrefKeys
{
    public static string Inventory(string key) => "inventory." + key;
}
public static class GamePrefs
{
    private static readonly Dictionary<string, int> Values = new Dictionary<string, int>();
    public static bool HasKey(string key) => Values.ContainsKey(key);
    public static int GetInt(string key, int fallback = 0) => Values.TryGetValue(key, out int value) ? value : fallback;
    public static void SetInt(string key, int value) { Values[key] = value; }
    public static void DeleteKey(string key) { Values.Remove(key); }
    public static void Save() { }
    public static void Clear() { Values.Clear(); }
}
public partial class InventoryManager
{
    private bool IsMutationBlocked => _isApplyingBooster;
    private bool ReplayPendingRewardJournal() => true;
}
