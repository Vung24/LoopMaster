using System;

public static class TimeUtils
{
	public const int SECONDS_PER_DAY = 86400;
	public const int SECONDS_PER_HOUR = 3600;
	public const int SECONDS_PER_MINUTE = 60;

	public static long GetUnixTimestamp()
	{
		return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}

	public static long GetUnixUnixTimeMilliseconds()
	{
		return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
	}

	public static long Elapsed(long since)
	{
		return Math.Max(0, GetUnixTimestamp() - since);
	}

	public static bool IsExpired(long timestamp)
	{
		return GetUnixTimestamp() >= timestamp;
	}

	public static bool IsUtcToday(long unixTimeSeconds)
	{
		try
		{
			return DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds).UtcDateTime.Date == DateTime.UtcNow.Date;
		}
		catch
		{
			return false;
		}
	}

	public static bool IsToday(long unixTimeSeconds)
	{
		try
		{
			return DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds).LocalDateTime.Date == DateTime.Now.Date;
		}
		catch
		{
			return false;
		}
	}
}