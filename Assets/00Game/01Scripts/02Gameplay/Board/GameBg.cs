using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class GameBg : MonoBehaviour
{
	public UnityEvent OnLongPress = new UnityEvent();
	public UnityEvent OnLongPressCancel = new UnityEvent();
	private Camera _mainCamera;
	private bool _isPointerDown;
	private float _pressTime;
	private bool _isLongPressTriggered;
	private float _longPressDuration = 0.3f;

	private void Awake()
	{
		_mainCamera = Camera.main;
	}

	private void Update()
	{
		if (_isPointerDown && !_isLongPressTriggered)
		{
			_pressTime += Time.deltaTime;
			if (_pressTime >= _longPressDuration)
			{
				_isLongPressTriggered = true;
				OnLongPress?.Invoke();
			}
		}
		if (Input.touchCount > 0) HandleTouchEvent();
		else HandleMouseEvent(); // Editor/desktop adaptation of InputWrapper.Input.
	}

	private bool IsPointerOverGameObject(int fingerId)
	{
		EventSystem eventSystem = EventSystem.current;
		return eventSystem != null && eventSystem.IsPointerOverGameObject(fingerId);
	}

	private void HandleTouchEvent()
	{
		Touch touch = Input.GetTouch(0);
		if (touch.phase == TouchPhase.Began) OnTouchBegan(touch);
		else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) OnTouchEnded(touch);
	}

	private void OnTouchBegan(Touch touch)
	{
		if (IsPointerOverGameObject(touch.fingerId)) return;
		TryBeginPress(touch.position);
	}

	private void OnTouchEnded(Touch touch)
	{
		EndPress();
	}

	private void HandleMouseEvent()
	{
		if (Input.GetMouseButtonDown(0) && !IsPointerOverGameObject(-1)) TryBeginPress(Input.mousePosition);
		if (Input.GetMouseButtonUp(0)) EndPress();
	}

	private void TryBeginPress(Vector2 screenPosition)
	{
		if (_mainCamera == null) return;
		Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
		if (!Physics.Raycast(ray, out RaycastHit hit, 30f) || hit.collider.GetComponent<GameBg>() == null) return;
		_isPointerDown = true;
		_isLongPressTriggered = false;
		_pressTime = 0f;
	}

	private void EndPress()
	{
		if (!_isPointerDown) return;
		_isPointerDown = false;
		if (_isLongPressTriggered) OnLongPressCancel?.Invoke();
		_isLongPressTriggered = false;
	}

	private void OnDisable()
	{
		EndPress();
	}
}
