using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Snui
{
    public static class PopupAssets
    {
        public static async UniTask<GameObject> Instantiate(string key, Transform parent,
            CancellationToken cancellationToken = default)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            cancellationToken.ThrowIfCancellationRequested();
            key = PopupAddresses.Resolve(key);
            var handle = Addressables.LoadAssetAsync<GameObject>(key);
            GameObject instance = null;
            bool ownsHandle = false;
            try
            {
                var prefab = await handle.Task;
                cancellationToken.ThrowIfCancellationRequested();
                if (parent == null) throw new OperationCanceledException("The popup parent was destroyed.");
                if (prefab == null) throw new InvalidOperationException("Popup prefab is missing: " + key);
                instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                var lifetime = instance.GetComponent<AddressCell>() ?? instance.AddComponent<AddressCell>();
                lifetime.AddReferenceHandle(key, handle);
                ownsHandle = true;
                instance.SetActive(true);
                return instance;
            }
            catch
            {
                if (instance != null) UnityEngine.Object.Destroy(instance);
                if (!ownsHandle && handle.IsValid()) Addressables.Release(handle);
                throw;
            }
        }

        public static async UniTask<T> Instantiate<T>(string key, Transform parent,
            CancellationToken cancellationToken = default) where T : Component
        {
            var instance = await Instantiate(key, parent, cancellationToken);
            var content = instance.GetComponent<T>();
            if (content != null) return content;
            UnityEngine.Object.Destroy(instance);
            throw new InvalidOperationException(key + " has no " + typeof(T).Name);
        }
    }
}
