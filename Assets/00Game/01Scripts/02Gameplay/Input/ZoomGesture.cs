using UnityEngine;
using Input = InputWrapper.Input;

public class ZoomGesture : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [Range(0f, 5f)] public float ZoomSpeed = 0.3f;
    [Range(0f, 5f)] public float ZoomMin = 1f;
    [Range(0f, 5f)] public float ZoomMax = 2f;
    public bool IsEnableWhenPanelShow;
    private float lastDistance;
    private bool _hasPreviousDistance;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        ZoomMax = Mathf.Max(ZoomMin, ZoomMax);
    }

    private void Update()
    {
        if ((!IsEnableWhenPanelShow && Snui.Snui_Panel.ShowingCount > 0) || Input.touchCount != 2)
        {
            _hasPreviousDistance = false;
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
            _hasPreviousDistance = false;
            return;
        }

        float distance = Vector2.Distance(first.position, second.position);
        if (!_hasPreviousDistance || first.phase == TouchPhase.Began || second.phase == TouchPhase.Began)
        {
            lastDistance = distance;
            _hasPreviousDistance = true;
            return;
        }
        if (first.phase != TouchPhase.Moved && second.phase != TouchPhase.Moved) return;

        ModifyScale((distance - lastDistance) * ZoomSpeed);
        lastDistance = distance;
    }

    private void ModifyScale(float deltaMagnitude)
    {
        Vector3 scale = transform.localScale;
        float maximum = Mathf.Max(ZoomMin, ZoomMax);
        transform.localScale = new Vector3(
            Mathf.Clamp(scale.x + deltaMagnitude, ZoomMin, maximum),
            Mathf.Clamp(scale.y + deltaMagnitude, ZoomMin, maximum),
            Mathf.Clamp(scale.z + deltaMagnitude, ZoomMin, maximum));
    }

    private void OnDisable() => _hasPreviousDistance = false;
}
