using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GameplayInput
{
    private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
    private EventSystem _eventSystem;
    private PointerEventData _pointer;

    public bool GetClickedBox(Camera camera, out Box box)
    {
        box = null;
        if (!Input.GetMouseButtonDown(0) || IsPointerOverUI() || camera == null) return false;
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, camera.farClipPlane)) return false;
        box = hit.collider.GetComponent<Box>();
        return box != null;
    }

    private bool IsPointerOverUI()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;
        Vector2 pointerPosition = Input.mousePosition;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began) continue;
            pointerPosition = touch.position;
            if (eventSystem.IsPointerOverGameObject(touch.fingerId)) return true;
            break;
        }

        if (_pointer == null || _eventSystem != eventSystem)
        {
            _eventSystem = eventSystem;
            _pointer = new PointerEventData(eventSystem);
        }
        _pointer.Reset();
        _pointer.position = pointerPosition;
        try
        {
            eventSystem.RaycastAll(_pointer, _uiHits);
            for (int i = 0; i < _uiHits.Count; i++)
                if (_uiHits[i].module is GraphicRaycaster) return true;
            return false;
        }
        finally
        {
            // Keep capacity, but release references to UI objects after the query.
            _uiHits.Clear();
            _pointer.pointerCurrentRaycast = default;
        }
    }
}
