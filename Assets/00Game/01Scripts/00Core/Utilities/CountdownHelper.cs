using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class CountdownHelper : MonoBehaviour
{
	public UnityEvent OnTimerStart = new UnityEvent();
	public UnityEvent OnTimerStoped = new UnityEvent();
	public UnityEvent<long> OnTimerTick = new UnityEvent<long>();

	private ICountdownTimer _countdownTimer;
	private Coroutine timerCoroutine;
	private WaitForSeconds oneSecond;
	private bool _notifyingTimerStopped;

	private void Awake()
	{
		oneSecond = new WaitForSeconds(1f);
	}

	private void OnEnable()
	{
		if (_countdownTimer != null && timerCoroutine == null)
		{
			timerCoroutine = StartCoroutine(IEUpdateTimer());
		}
	}

	private void OnDisable()
	{
		if (timerCoroutine != null)
		{
			StopCoroutine(timerCoroutine);
			timerCoroutine = null;
		}
	}

	public void CountdownUntil(long timestamp)
	{
		if (TimeUtils.IsExpired(timestamp))
		{
			StopTimer();
			return;
		}

		// Replacing a countdown is not a completion event. Stop listeners may restart it.
		ClearTimer();
		_countdownTimer = new CountdownUntil(timestamp);
		_countdownTimer.OnTimerTick += UpdateTimer;
		StartTimer();
		OnTimerStart?.Invoke();
	}

	public void StartCountdown(long timestamp) => CountdownUntil(timestamp);

	public static void CountdownUntil(Snui.Timer timer, long timestamp)
	{
		if (timer != null) timer.CountdownUntil(timestamp);
	}

	public static void CountdownFixTime(Snui.Timer timer, long fixTime)
	{
		if (timer != null) timer.CountdownFixTime(fixTime);
	}


	public void CountdownFixTime(long fixTime)
	{
		if (fixTime <= 0)
		{
			StopTimer();
			return;
		}

		ClearTimer();
		_countdownTimer = new CountdownFixTime(fixTime);
		_countdownTimer.OnTimerTick += UpdateTimer;
		StartTimer();
		OnTimerStart?.Invoke();
	}

	public void StartTimer()
	{
		if (timerCoroutine != null)
		{
			StopCoroutine(timerCoroutine);
			timerCoroutine = null;
		}

		if (gameObject.activeInHierarchy)
		{
			timerCoroutine = StartCoroutine(IEUpdateTimer());
		}
	}

	private IEnumerator IEUpdateTimer()
	{
		if (_countdownTimer != null)
		{
			UpdateTimer(_countdownTimer.GetCurrTime());
		}

		while (_countdownTimer != null)
		{
			yield return oneSecond;
			if (_countdownTimer == null) yield break;

			var currentTimer = _countdownTimer;
			currentTimer.UpdateTimer();
			// Tick/stop listeners can clear or replace the timer synchronously.
			if (_countdownTimer != currentTimer) yield break;
			if (currentTimer.GetCurrTime() <= 0)
			{
				StopTimer();
				yield break;
			}
		}
	}

	private void UpdateTimer(long time)
	{
		var currentTimer = _countdownTimer;
		OnTimerTick?.Invoke(time);
		if (time <= 0 && _countdownTimer == currentTimer)
		{
			StopTimer();
		}
	}

	public void StopTimer()
	{
		ClearTimer();
		if (_notifyingTimerStopped) return;

		_notifyingTimerStopped = true;
		try
		{
			OnTimerStoped?.Invoke();
		}
		finally
		{
			_notifyingTimerStopped = false;
		}
	}

	private void ClearTimer()
	{
		if (timerCoroutine != null)
		{
			StopCoroutine(timerCoroutine);
			timerCoroutine = null;
		}

		if (_countdownTimer != null)
		{
			_countdownTimer.OnTimerTick -= UpdateTimer;
			_countdownTimer = null;
		}
	}
}
