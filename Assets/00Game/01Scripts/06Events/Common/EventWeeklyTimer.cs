using System;

namespace Snui
{
	public class EventWeeklyTimer
	{
		public long StartUnixTimestamp { get; private set; }
		public long EndUnixTimestamp { get; private set; }
		public DayOfWeek StartDay { get; private set; }
		public DayOfWeek EndDay { get; private set; }
		public TimeSpan DailyStartTime { get; private set; }
		public TimeSpan DailyEndTime { get; private set; }
		public DateTime StartTime { get; private set; }
		public DateTime EndTime { get; private set; }

		public EventWeeklyTimer(DayOfWeek startDay, DayOfWeek endDay)
		{
			Init(startDay, TimeSpan.Zero, endDay, new TimeSpan(23, 59, 59));
		}

		public EventWeeklyTimer(DayOfWeek startDay, TimeSpan dailyStartTime, DayOfWeek endDay, TimeSpan dailyEndTime)
		{
			Init(startDay, dailyStartTime, endDay, dailyEndTime);
		}

		private void Init(DayOfWeek startDay, TimeSpan dailyStartTime, DayOfWeek endDay, TimeSpan dailyEndTime)
		{
			StartDay = startDay;
			DailyStartTime = dailyStartTime;
			EndDay = endDay;
			DailyEndTime = dailyEndTime;
			CalculateUnixTimestamp();
		}

		private int DayOfWeekValue(DayOfWeek dayOfWeek)
		{
			return dayOfWeek == DayOfWeek.Sunday ? 7 : (int)dayOfWeek;
		}

		private int DaysBetween(DayOfWeek startDay, DayOfWeek endDay)
		{
			return DayOfWeekValue(startDay) - DayOfWeekValue(endDay);
		}

		private void CalculateUnixTimestamp()
		{
			CalculateUnixTimestamp(DateTime.UtcNow);
		}

		private void CalculateUnixTimestamp(DateTime nowUtc)
		{
			DateTime calNow = nowUtc.AddHours(-8);
			DateTime calStart = calNow.Date.AddDays(DaysBetween(StartDay, calNow.DayOfWeek)).Add(DailyStartTime);
			DateTime calEnd = calNow.Date.AddDays(DaysBetween(EndDay, calNow.DayOfWeek)).Add(DailyEndTime);
			if (calStart >= calEnd) calEnd = calEnd.AddDays(7);

			if (calNow > calEnd)
			{
				calStart = calStart.AddDays(7);
				calEnd = calEnd.AddDays(7);
			}

			StartTime = GetUtcTimeByCaliforniaOffset(calStart);
			EndTime = GetUtcTimeByCaliforniaOffset(calEnd);

			StartUnixTimestamp = new DateTimeOffset(StartTime).ToUnixTimeSeconds();
			EndUnixTimestamp = new DateTimeOffset(EndTime).ToUnixTimeSeconds();
		}

		private DateTime GetCaliforniaOffsetFromUtc()
		{
			return DateTime.UtcNow.AddHours(-8);
		}

		private DateTime GetUtcTimeByCaliforniaOffset(DateTime calTime)
		{
			return DateTime.SpecifyKind(calTime.AddHours(8), DateTimeKind.Utc);
		}

		public bool IsWaitingStart()
		{
			long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			return now < StartUnixTimestamp;
		}

		public bool IsOngoing()
		{
			long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			return now >= StartUnixTimestamp && now <= EndUnixTimestamp;
		}

		public bool IsExpired()
		{
			long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			return now > EndUnixTimestamp;
		}

		public long GetRemainingTime()
		{
			long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			if (now >= EndUnixTimestamp) return 0;
			return EndUnixTimestamp - now;
		}
	}
}
