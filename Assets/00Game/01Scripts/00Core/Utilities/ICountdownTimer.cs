using System;

public interface ICountdownTimer
{
	event Action<long> OnTimerTick;
	void UpdateTimer();
	long GetCurrTime();
}
