using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class JsonUtils
{
	public static Dictionary<string, object> Parse(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return null;

		try
		{
			return JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public static string Serialize(object obj)
	{
		return JsonConvert.SerializeObject(obj);
	}

	public static JObject TryParse(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return null;

		try
		{
			return JObject.Parse(text);
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
