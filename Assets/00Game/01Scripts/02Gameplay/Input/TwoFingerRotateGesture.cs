using UnityEngine;
using Input = InputWrapper.Input;

public class TwoFingerRotateGesture : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [Range(0f, 5f)] public float RotateSpeed = 2f;
    public bool IsEnableWhenPanelShow;
    private Vector2 rotationBeforePosition;
    private bool _hasPreviousPosition;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private void Update()
    {
        if (_camera == null || (!IsEnableWhenPanelShow && Snui.Snui_Panel.ShowingCount > 0)
            || Input.touchCount != 2)
        {
            _hasPreviousPosition = false;
            return;
        }

        Touch first = Input.GetTouch(0);
        Touch second = Input.GetTouch(1);
        HandleTouches(first, second);
    }

    private void HandleTouches(Touch first, Touch second)
    {
        if (first.phase == TouchPhase.Ended || first.phase == TouchPhase.Canceled
            || second.phase == TouchPhase.Ended || second.phase == TouchPhase.Canceled)
        {
            _hasPreviousPosition = false;
            return;
        }

        Vector2 direction = second.position - first.position;
        if (!_hasPreviousPosition || first.phase == TouchPhase.Began || second.phase == TouchPhase.Began)
        {
            rotationBeforePosition = direction;
            _hasPreviousPosition = true;
            return;
        }
        if (first.phase != TouchPhase.Moved && second.phase != TouchPhase.Moved) return;

        // Native signs Angle(current, previous) using current x previous.
        // RotateSpeed is serialized in the original, but is not used by its Update.
        float angle = Vector2.SignedAngle(direction, rotationBeforePosition);
        rotationBeforePosition = direction;
        transform.RotateAround(transform.position, _camera.transform.position - transform.position, angle);
    }

    private void OnDisable() => _hasPreviousPosition = false;
}
