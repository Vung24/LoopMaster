using TMPro;
using UnityEngine;

public static class GameObjectExtensions
{
	public static void SetLocalizedString(this TMP_Text text, string table, string key, params object[] args)
	{
		if (text == null) return;
		if (args != null && args.Length > 0)
		{
			text.text = string.Format(key, args);
		}
		else
		{
			text.text = key;
		}
	}

	public static T GetOrAddComponent<T>(this GameObject go) where T : Component
	{
		if (go == null) return null;
		var comp = go.GetComponent<T>();
		return comp != null ? comp : go.AddComponent<T>();
	}

	public static T GetOrAddComponent<T>(this Component comp) where T : Component
	{
		if (comp == null) return null;
		return comp.gameObject.GetOrAddComponent<T>();
	}
}