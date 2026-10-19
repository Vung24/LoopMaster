using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IGroupLockScene
{
    IReadOnlyList<Box> Boxes { get; }
    bool TryGetGroupRollDoor(int groupId, out GroupRollDoor door);
    void RemoveGroupRollDoor(int groupId, GroupRollDoor expectedDoor);
}

public sealed class GroupLockFlow
{
    private readonly IGroupLockScene _scene;
    private readonly CancellationToken _destroyToken;
    private readonly HashSet<int> _unlockingGroups = new HashSet<int>();
    private readonly HashSet<GroupColorLockKey> _keys = new HashSet<GroupColorLockKey>();
    private readonly List<GroupColorLockKey> _cleanupKeys = new List<GroupColorLockKey>();
    private GameData _data;
    private int _version;

    public GroupLockFlow(IGroupLockScene scene, CancellationToken destroyToken)
    {
        _scene = scene;
        _destroyToken = destroyToken;
    }

    public bool IsBusy => _unlockingGroups.Count > 0;

    public bool IsUnlocking(Box box) => box != null && box.Info != null &&
        box.Info.GroupInfo != null && _unlockingGroups.Contains(box.Info.GroupInfo.Id);

    public void Reset(GameData data)
    {
        Clear();
        _data = data;
    }

    public void HandleFinishedBox(BoxInfo box)
    {
        if (_data == null || box == null) return;
        foreach (CubeInfo cube in box.CubeInfos)
        {
            if (cube.GroupKeyId <= 0) continue;
            int groupId = cube.GroupKeyId;
            if (_data.TryUnlockGroupColorLock(groupId))
            {
                _unlockingGroups.Add(groupId);
                PlayUnlock(cube, groupId, _version).Forget();
            }
            else TakeKey(cube)?.DestroySelf();
        }
    }

    public void UnlockAvailableKeys()
    {
        if (_data == null) return;
        for (int i = 0; i < _scene.Boxes.Count; i++)
        {
            Box box = _scene.Boxes[i];
            if (box == null || box.Info == null || box.Info.HasBlockingLock()) continue;
            CubeInfo keyCube = box.Info.GetTopGroupLockKey();
            if (keyCube == null || _unlockingGroups.Contains(keyCube.GroupKeyId)) continue;
            int groupId = keyCube.GroupKeyId;
            if (!_data.TryUnlockGroupColorLock(groupId)) continue;
            _unlockingGroups.Add(groupId);
            _data.UndoData.Clear();
            PlayUnlock(keyCube, groupId, _version).Forget();
        }
    }

    private static GroupColorLockKey TakeKey(CubeInfo cube)
    {
        if (cube == null) return null;
        if (cube.CubeSc != null) return cube.CubeSc.TakeGroupColorLockKey();
        cube.ClearGroupColorLockKey();
        return null;
    }

    private async UniTask PlayUnlock(CubeInfo cube, int groupId, int version)
    {
        _scene.TryGetGroupRollDoor(groupId, out GroupRollDoor door);
        GroupColorLockKey key = TakeKey(cube);
        if (key != null) _keys.Add(key);
        try
        {
            if (key != null && door != null &&
                door.TryGetKeyInsertPose(out Vector3 position, out Quaternion rotation, out Vector3 direction))
            {
                key.DetachFromCube();
                await key.PlayInsertTo(position, rotation, direction);
                if (version != _version || door == null) return;
                door.Unlock();
                if (key != null) await key.PlayDismiss();
            }
            else
            {
                if (key != null) key.DestroySelf();
                if (door != null) door.Unlock();
            }
            await UniTask.WaitUntil(() => version != _version || door == null || door.IsUnlockComplete,
                cancellationToken: _destroyToken);
        }
        finally
        {
            _keys.Remove(key);
            if (key != null) key.DestroySelf();
            if (version == _version)
            {
                _unlockingGroups.Remove(groupId);
                _scene.RemoveGroupRollDoor(groupId, door);
            }
        }
    }

    public void Clear()
    {
        // Invalidate before killing animations: their continuations may run during cleanup.
        _version++;
        _data = null;
        _unlockingGroups.Clear();
        _cleanupKeys.Clear();
        _cleanupKeys.AddRange(_keys);
        _keys.Clear();
        for (int i = 0; i < _cleanupKeys.Count; i++)
            if (_cleanupKeys[i] != null) _cleanupKeys[i].DestroySelf();
        _cleanupKeys.Clear();
    }
}
