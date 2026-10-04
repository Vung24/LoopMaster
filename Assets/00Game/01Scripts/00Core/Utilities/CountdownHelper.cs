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
			OnTimerStoped?.Invoke();
			return;
		}

		StopTimer();
		_countdownTimer = new CountdownUntil(timestamp);
		_countdownTimer.OnTimerTick += UpdateTimer;
		StartTimer();
		OnTimerStart?.Invoke();
	}

	public void CountdownFixTime(long fixTime)
	{
		if (fixTime <= 0)
		{
			OnTimerStoped?.Invoke();
			return;
		}

		StopTimer();
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

			_countdownTimer.UpdateTimer();
			if (_countdownTimer.GetCurrTime() <= 0)
			{
				StopTimer();
				yield break;
			}
		}
	}

	private void UpdateTimer(long time)
	{
		OnTimerTick?.Invoke(time);
		if (time <= 0)
		{
			StopTimer();
		}
	}

	public void StopTimer()
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

		OnTimerStoped?.Invoke();
	}
}