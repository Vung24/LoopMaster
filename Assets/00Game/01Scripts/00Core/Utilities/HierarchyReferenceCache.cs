using System;
using UnityEngine;

internal static class HierarchyReferenceCache
{
	public static Transform Child(Transform root, string childName)
	{
		if (root == null) return null;
		for (int i = 0; i < root.childCount; i++)
		{
			Transform child = root.GetChild(i);
			if (string.Equals(child.name, childName, StringComparison.Ordinal)) return child;
		}
		return null;
	}

	public static Transform Path(Transform root, params string[] segments)
	{
		Transform current = root;
		for (int i = 0; i < segments.Length && current != null; i++)
			current = Child(current, segments[i]);
		return current;
	}

	public static Transform Descendant(Transform root, string objectName)
	{
		if (root == null) return null;
		for (int i = 0; i < root.childCount; i++)
		{
			Transform child = root.GetChild(i);
			if (string.Equals(child.name, objectName, StringComparison.Ordinal)) return child;
			Transform nested = Descendant(child, objectName);
			if (nested != null) return nested;
		}
		return null;
	}

	public static T Component<T>(Transform node) where T : Component =>
		node != null ? node.GetComponent<T>() : null;
}
