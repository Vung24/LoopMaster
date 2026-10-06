using UnityEngine;

namespace Snui
{
	public class SingletonScriptableObject<T> : ScriptableObject where T : ScriptableObject
	{
		private static T _instance;

		public static T Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = Resources.Load<T>(typeof(T).Name);
				}
				return _instance;
			}
		}

		public static void Validate() { }
	}
}

public class SingletonScriptableObject<T> : Snui.SingletonScriptableObject<T> where T : ScriptableObject
{
}
