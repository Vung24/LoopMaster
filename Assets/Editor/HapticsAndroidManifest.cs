#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

// JNI haptics require an explicit permission because Unity cannot infer it from JNI calls.
public sealed class HapticsAndroidManifest : IPostGenerateGradleAndroidProject
{
	public int callbackOrder => 0;

	public void OnPostGenerateGradleAndroidProject(string path)
	{
		string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
		var document = new XmlDocument();
		document.Load(manifestPath);
		const string androidNamespace = "http://schemas.android.com/apk/res/android";
		XmlElement manifest = document.DocumentElement;
		foreach (XmlNode node in manifest.GetElementsByTagName("uses-permission"))
			if (node is XmlElement permission && permission.GetAttribute("name", androidNamespace) == "android.permission.VIBRATE")
				return;
		XmlElement entry = document.CreateElement("uses-permission");
		XmlAttribute name = document.CreateAttribute("android", "name", androidNamespace);
		name.Value = "android.permission.VIBRATE";
		entry.Attributes.Append(name);
		manifest.AppendChild(entry);
		document.Save(manifestPath);
	}
}
#endif
