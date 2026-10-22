using UnityEngine;

public class CheatGesture : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float _holdSeconds = 0.75f;
    private float _held;
    private bool _triggered;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            Snui.CheatManager.Ensure().Toggle();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && SRDebug.Instance != null && SRDebug.Instance.IsDebugPanelVisible)
        {
            SRDebug.Instance.HideDebugPanel();
        }

        int heldTouches = 0;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
            {
                heldTouches++;
            }
        }

        if (heldTouches < 3)
        {
            _held = 0f;
            _triggered = false;
            return;
        }

        if (_triggered) return;
        _held += Time.unscaledDeltaTime;
        if (_held < _holdSeconds) return;

        _triggered = true;
        Snui.CheatManager.Ensure().Toggle();
    }
}
