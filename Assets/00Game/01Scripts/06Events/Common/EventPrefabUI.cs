using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
    public static class EventPrefabUI
    {
        private static readonly Dictionary<string, GameObject> open = new Dictionary<string, GameObject>();
        private sealed class PopupLoad
        {
            public Transform Parent;
            public bool Canceled;
            public Task<GameObject> Task;
        }
        private static readonly Dictionary<string, PopupLoad> loading = new Dictionary<string, PopupLoad>();
        public static bool HasOpenDialogs
        {
            get
            {
                if (loading.Count > 0) return true;
                foreach (var entry in open.Values)
                    if (entry != null && entry.activeInHierarchy) return true;
                return false;
            }
        }
        public static void CloseDialogs(Transform owner)
        {
            if (owner == null) return;
            var canvas = owner.GetComponentInParent<Canvas>();
            foreach (var entry in new List<KeyValuePair<string, PopupLoad>>(loading))
                if (BelongsToOwner(entry.Value.Parent, owner, canvas))
                {
                    entry.Value.Canceled = true;
                    loading.Remove(entry.Key);
                }
            foreach (var entry in new List<GameObject>(open.Values))
                if (entry != null && BelongsToOwner(entry.transform, owner, canvas))
                {
                    entry.SetActive(false);
                    UnityEngine.Object.Destroy(entry);
                }
        }
        private static bool BelongsToOwner(Transform target, Transform owner, Canvas canvas) =>
            target != null && (target == owner || target.IsChildOf(owner) ||
                (canvas != null && target.IsChildOf(canvas.rootCanvas.transform)));
        public static GameObject Load(string name) => PrefabLibrary.Current?.Resolve("Events/" + name);
        public static async UniTask<T> Show<T>(Transform parent, string name) where T : Component
        {
            if (parent == null) parent = Dialog.DefaultParent;
            if (parent == null) parent = SnuiManager.GetCurrentUiCanvas()?.transform;
            if (parent == null) throw new InvalidOperationException("An event dialog requires a UI parent.");
            string key = parent.GetInstanceID() + ":" + PopupAddresses.Resolve(name);
            if (open.TryGetValue(key, out var existing) && existing != null && existing.activeInHierarchy)
                return RequireContent<T>(existing, name);
            if (!loading.TryGetValue(key, out var pending))
            {
                pending = new PopupLoad { Parent = parent };
                loading[key] = pending;
                pending.Task = ShowRoot(parent, name, key, pending).AsTask();
            }
            try { return RequireContent<T>(await pending.Task, name); }
            finally { if (loading.TryGetValue(key, out var current) && current == pending) loading.Remove(key); }
        }
        private static T RequireContent<T>(GameObject root, string name) where T : Component
        {
            var content = root == null ? null : root.GetComponentInChildren<T>(true);
            if (content != null) return content;
            if (root != null) UnityEngine.Object.Destroy(root);
            throw new InvalidOperationException(name + " has no " + typeof(T).Name);
        }
        private static async UniTask<GameObject> ShowRoot(Transform parent, string name, string key, PopupLoad pending)
        {
            GameObject root = null;
            GameObject instance = null;
            try
            {
                instance = await PopupAssets.Instantiate(name, parent, parent.GetCancellationTokenOnDestroy());
                if (pending.Canceled) throw new OperationCanceledException();
                root = instance;
                var content = instance.GetComponent<DialogContent>();
                if (content != null)
                {
                    instance.SetActive(false);
                    var frame = await Dialog.ShowEmpty(parent);
                    root = frame.gameObject;
                    if (pending.Canceled) throw new OperationCanceledException();
                    frame.AddDialogContent(content);
                }
                open[key] = root;
                RewardDialog.PrepareOverlay(root, 1300 + open.Count);
                var overlay = root.GetComponent<EventPrefabOverlay>() ?? root.AddComponent<EventPrefabOverlay>();
                GameObject tracked = root;
                overlay.Closed = () => { if (open.TryGetValue(key, out var current) && current == tracked) open.Remove(key); };
                overlay.CloseButton = HierarchyReferenceCache.DescendantComponent<Button>(root.transform, "CloseBtn")
                    ?? HierarchyReferenceCache.DescendantComponent<Button>(root.transform, "CloseButton")
                    ?? instance.GetComponentInChildren<Snui_FullButton>(true)?.Button;
                return root;
            }
            catch
            {
                if (root != null) UnityEngine.Object.Destroy(root);
                if (instance != null && instance != root) UnityEngine.Object.Destroy(instance);
                throw;
            }
        }
    }
}
