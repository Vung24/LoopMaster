using System;

public class CountdownUntil : ICountdownTimer
{
	private Action<long> m_OnTimerTick;
	private long timestamp;

	public event Action<long> OnTimerTick
	{
		add => m_OnTimerTick += value;
		remove => m_OnTimerTick -= value;
	}

	public CountdownUntil(long timestamp)
	{
		this.timestamp = timestamp;
	}

	public void UpdateTimer()
	{
		long remaining = Math.Max(0, timestamp - TimeUtils.GetUnixTimestamp());
		m_OnTimerTick?.Invoke(remaining);
	}

	public long GetCurrTime()
	{
		return Math.Max(0, timestamp - TimeUtils.GetUnixTimestamp());
	}
}
