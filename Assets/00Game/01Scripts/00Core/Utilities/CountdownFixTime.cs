using System;

public class CountdownFixTime : ICountdownTimer
{
	private Action<long> m_OnTimerTick;
	private long fixTime;

	public event Action<long> OnTimerTick
	{
		add => m_OnTimerTick += value;
		remove => m_OnTimerTick -= value;
	}

	public CountdownFixTime(long fixTime)
	{
		this.fixTime = Math.Max(0, fixTime);
	}

	public void UpdateTimer()
	{
		if (fixTime > 0)
		{
			fixTime--;
		}
		m_OnTimerTick?.Invoke(fixTime);
	}

	public long GetCurrTime()
	{
		return fixTime;
	}
}
