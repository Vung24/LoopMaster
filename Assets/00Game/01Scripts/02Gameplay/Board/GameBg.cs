using UnityEngine;
using UnityEngine.Events;

public class GameBg : MonoBehaviour
{
	public UnityEvent OnLongPress = new UnityEvent();
	public UnityEvent OnLongPressCancel = new UnityEvent();
}
