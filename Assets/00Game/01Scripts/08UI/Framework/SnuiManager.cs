using UnityEngine;

public class SnuiManager : MonoBehaviour
{
	private static Canvas _currentCanvas;

	public static Canvas CurrentUiCanvas
	{
		get => _currentCanvas;
		set => _currentCanvas = value;
	}

	public static Canvas GetCurrentUiCanvas()
	{
		return _currentCanvas;
	}

	public Canvas GetCurrentUiCanvasInstance()
	{
		return _currentCanvas;
	}
}