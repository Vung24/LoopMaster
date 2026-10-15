using UnityEngine;

public class Utils : MonoBehaviour
{
	public static Canvas GetCurrentUiCanvas()
	{
		return SnuiManager.GetCurrentUiCanvas();
	}
}
