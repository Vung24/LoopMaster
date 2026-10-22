using UnityEngine;
using UnityEngine.Events;

[System.Obsolete("Deprecated background component")]
public class GameBg : MonoBehaviour
{
    public UnityEvent OnLongPress = new UnityEvent();
    public UnityEvent OnLongPressCancel = new UnityEvent();
}

