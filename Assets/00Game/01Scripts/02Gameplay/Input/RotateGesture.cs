using UnityEngine;
using UnityEngine.EventSystems;
using Input = InputWrapper.Input;

public class RotateGesture : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [Range(0f, 15f)] public float RotateSpeed = 2f;
    public bool IsEnableWhenPanelShow;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private bool IsPointerOverGameObject(int fingerId)
    {
        // The desktop wrapper uses finger 0; EventSystem uses -1 for its mouse.
        int pointerId = UnityEngine.Input.touchCount > 0 ? fingerId : -1;
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
    }

    private void Update()
    {
        if (_camera == null || (!IsEnableWhenPanelShow && Snui.Snui_Panel.ShowingCount > 0)
            || Input.touchCount != 1) return;
        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Moved || IsPointerOverGameObject(touch.fingerId)) return;

        ApplyRotation(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    private void ApplyRotation(float horizontal, float vertical)
    {
        Vector3 angles = -horizontal * RotateSpeed * _camera.transform.up
            + vertical * RotateSpeed * _camera.transform.right;
        transform.Rotate(angles, Space.World);
    }
}
