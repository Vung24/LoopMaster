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
			return hours > 0 ? $"{days}d:{hours}h" : $"{days}";
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

	public static string FormatRewardDuration(long seconds)
	{
		if (seconds < 0) seconds = 0;
		long hours = seconds / 3600;
		long minutes = seconds % 3600 / 60;
		long secs = seconds % 60;

		string duration = "";
		if (hours > 0) duration = $"{hours} h";
		if (minutes > 0)
		{
			if (duration.Length > 0) duration += " ";
			duration += $"{minutes} min";
		}
		if (secs > 0 || duration.Length == 0)
		{
			if (duration.Length > 0) duration += " ";
			duration += $"{secs} s";
		}
		return duration;
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
