using System;
using UnityEngine;

public class TimeFormatter
{
	private static TimeFormatter _instance;
	public static TimeFormatter Instance => _instance ??= new TimeFormatter();

	public static string FormatTimeLeft(long seconds)
	{
		if (seconds < 0) seconds = 0;
		long days = seconds / 86400;
		long rem = seconds % 86400;
		long hours = rem / 3600;
		rem %= 3600;
		long minutes = rem / 60;
		long secs = rem % 60;

		if (days > 0)
		{
			return hours > 0 ? $"{days}:{hours}" : $"{days}";
		}
		if (hours > 0)
		{
			return minutes > 0 ? $"{hours}:{minutes}" : $"{hours}";
		}
		if (minutes > 0)
		{
			return secs > 0 ? $"{minutes}:{secs}" : $"{minutes}";
		}
		return $"{secs}s";
	}

	public static string FormatTimeLeftWithoutI18n(long seconds)
	{
		if (seconds < 0) seconds = 0;
		TimeSpan span = TimeSpan.FromSeconds(seconds);
		if (span.TotalHours >= 1)
		{
			return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
		}
		return $"{span.Minutes:D2}:{span.Seconds:D2}";
	}

	public static string FormatSeconds(long seconds) => FormatTimeLeftWithoutI18n(seconds);
}