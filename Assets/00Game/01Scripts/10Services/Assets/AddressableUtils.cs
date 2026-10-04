using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class AddressableUtils
{
	public static AsyncOperationHandle<GameObject> AsyncLoadAsGameObject(string key, Transform parent)
	{
		AsyncOperationHandle<GameObject> handle =
			Addressables.InstantiateAsync(key, parent, false, true);

		handle.Completed += result =>
		{
			if (!result.IsValid()) return;
			if (result.Result == null)
			{
				result.Release();
				return;
			}

			AddressCell cell = result.Result.AddComponent<AddressCell>();
			cell.Handle = handle;
		};

		return handle;
	}

	public static AsyncOperationHandle<GameObject> AsyncLoadAsGameObject(
		AssetReference reference, Transform parent)
	{
		return AsyncLoadAsGameObject(reference.RuntimeKey.ToString(), parent);
	}
}
