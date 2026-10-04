using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressCell : MonoBehaviour
{
	private AsyncOperationHandle<GameObject> _handle;
	private Dictionary<string, AsyncOperationHandle> _referenceHandles;

	public AsyncOperationHandle<GameObject> Handle
	{
		get => _handle;
		set => _handle = value;
	}

	public void AddReferenceHandle(string path, AsyncOperationHandle handle)
	{
		if (_referenceHandles == null)
			_referenceHandles = new Dictionary<string, AsyncOperationHandle>();
		_referenceHandles[path] = handle;
	}

	public void RemoveReferenceHandle(string path)
	{
		if (_referenceHandles == null || !_referenceHandles.TryGetValue(path, out AsyncOperationHandle handle))
			return;
		if (handle.IsValid()) handle.Release();
		_referenceHandles.Remove(path);
	}

	public AsyncOperationHandle GetReferenceHandle(string path)
	{
		return _referenceHandles != null && _referenceHandles.TryGetValue(path, out AsyncOperationHandle handle)
			? handle
			: default;
	}

	public void ReleaseReferenceAllHandles()
	{
		if (_referenceHandles == null) return;
		foreach (AsyncOperationHandle handle in _referenceHandles.Values)
			if (handle.IsValid()) handle.Release();
		_referenceHandles.Clear();
	}

	private void OnDestroy()
	{
		ReleaseReferenceAllHandles();
		if (_handle.IsValid()) _handle.Release();
	}
}
