using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class GroupColorLockKey : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    [SerializeField] private Renderer[] _renderers;
    private Sequence _animation;

    public void Init(int groupKeyColor)
    {
        Color color = GroupColorLockConstants.GetColorByType(groupKeyColor);
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>(true);
        var block = new MaterialPropertyBlock();
        foreach (Renderer renderer in _renderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }

    public void DetachFromCube()
    {
        transform.SetParent(null, true);
        _animation?.Kill();
        transform.DOKill();
    }

    public async UniTask PlayInsertTo(Vector3 insertPosition, Quaternion insertRotation, Vector3 insertDirection)
    {
        _animation?.Kill();
        Vector3 direction = insertDirection.normalized;
        Vector3 start = transform.position;
        Vector3 preInsert = insertPosition - direction * 0.45f;
        Vector3 control = (start + preInsert) * 0.5f + Vector3.up *
            Mathf.Max(0.45f, Vector3.Distance(start, preInsert) * 0.25f);
        Vector3 scale = transform.localScale;
        Quaternion upright = GetKeyUprightRotation(insertRotation);
        Quaternion flyRotation = Quaternion.AngleAxis(90f, direction) * upright;
        Quaternion turned = Quaternion.AngleAxis(90f, -direction) * flyRotation;
        Sequence scaleAnimation = DOTween.Sequence()
            .Append(transform.DOScale(scale * 1.3f, 0.175f).SetEase(Ease.OutSine))
            .Append(transform.DOScale(1.25f, 0.175f).SetEase(Ease.InSine));
        Sequence sequence = DOTween.Sequence()
            .Append(DOTween.To(() => 0f,
                t => transform.position = GetQuadraticBezierPoint(start, control, preInsert, t),
                1f, 0.35f).SetEase(Ease.InOutSine))
            .Join(transform.DORotateQuaternion(flyRotation, 0.35f).SetEase(Ease.InOutSine))
            .Join(scaleAnimation)
            .Append(transform.DOMove(insertPosition, 0.18f).SetEase(Ease.InQuad))
            .Append(transform.DORotateQuaternion(turned, 0.2f).SetEase(Ease.InOutSine))
            .Append(transform.DORotateQuaternion(flyRotation, 0.2f).SetEase(Ease.InOutSine))
            .SetTarget(transform).SetLink(gameObject);
        _animation = sequence;
        await WaitForAnimation(sequence);
        // The door opens after insertion. Dismiss is a separate awaited operation.
    }

    public async UniTask PlayDismiss()
    {
        _animation?.Kill();
        Sequence sequence = DOTween.Sequence()
            .AppendInterval(0.2f)
            .Append(transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack))
            .SetTarget(transform).SetLink(gameObject);
        _animation = sequence;
        await WaitForAnimation(sequence);
        if (this != null) DestroySelf();
    }

    private async UniTask WaitForAnimation(Sequence sequence)
    {
        try
        {
            await UniTask.WaitUntil(() => !sequence.IsActive() || sequence.IsComplete(),
                cancellationToken: destroyCancellationToken);
        }
        finally
        {
            if (_animation == sequence) _animation = null;
            sequence.Kill();
        }
    }

    public void DestroySelf()
    {
        _animation?.Kill();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private static Vector3 GetQuadraticBezierPoint(Vector3 start, Vector3 control, Vector3 end, float progress)
    {
        float inverse = 1f - progress;
        return inverse * inverse * start + 2f * inverse * progress * control + progress * progress * end;
    }

    private Quaternion GetKeyUprightRotation(Quaternion targetRotation)
    {
        Vector3 head = targetRotation * GetKeyHeadDirectionInKeySpace();
        return head.sqrMagnitude < 0.001f ? targetRotation :
            Quaternion.FromToRotation(head.normalized, Vector3.up) * targetRotation;
    }

    private Vector3 GetKeyHeadDirectionInKeySpace()
    {
        Renderer renderer = GetComponentInChildren<Renderer>(true);
        if (renderer == null) return Vector3.up;
        return transform.InverseTransformDirection(renderer.transform.TransformDirection(Vector3.up)).normalized;
    }
}
