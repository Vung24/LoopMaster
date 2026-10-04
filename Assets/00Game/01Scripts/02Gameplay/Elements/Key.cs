using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class Key : MonoBehaviour
{
    private const string PrefabPath = "KeyHunter/Prefab/Key.prefab";
    private float _originScale;
    private Tween _animation;

    public static async UniTask<Key> Create(Transform parent)
    {
        Key prefab = BoardController.DefaultContext?.KeyPrefab;
        if (prefab != null) return await Create(parent, prefab);
        var handle = AddressableUtils.AsyncLoadAsGameObject(PrefabPath, parent);
        GameObject instance = await handle.Task;
        Key key = instance == null ? null : instance.GetComponent<Key>();
        if (key != null) return key;
        if (instance != null) Destroy(instance);
        throw new InvalidOperationException(PrefabPath + " requires a Key component.");
    }

    public static UniTask<Key> Create(Transform parent, Key prefab)
    {
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        return UniTask.FromResult(Instantiate(prefab, parent, false));
    }

    private void Awake() => _originScale = transform.localScale.x;

    public Tween PlayAnimation(Vector3 position)
    {
        _animation?.Kill();
        float y = transform.localPosition.y;
        Sequence scale = DOTween.Sequence()
            .Append(transform.DOScale(0f, 0f))
            .Append(transform.DOScale(_originScale * 1.1f, 0.23f))
            .Append(transform.DOScale(_originScale, 0.13f));
        _animation = DOTween.Sequence()
            .Append(transform.DOLocalMoveY(y - 10f, 0.23f))
            .Append(transform.DOLocalMoveY(y, 0.13f))
            .Append(transform.DOLocalMoveY(y - 10f, 0.47f))
            .Append(transform.DOLocalMoveY(y, 0.5f))
            .Append(transform.DOLocalMoveY(y - 20f, 0.13f))
            .Append(transform.DOMove(position, 0.27f).SetEase(Ease.InSine))
            .Insert(0f, scale)
            .SetLink(gameObject);
        return _animation;
    }
}
