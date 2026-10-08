using System;
using System.Collections.Generic;

public class EventManager : BaseManager
{
	private readonly Dictionary<string, Action> _simpleEvents = new Dictionary<string, Action>();
	private readonly Dictionary<string, Action<object>> _paramEvents = new Dictionary<string, Action<object>>();

	public override void Startup()
	{
		base.Startup();
	}

	public void RegisterEvent(string eventName, Action listener)
	{
		if (string.IsNullOrEmpty(eventName) || listener == null) return;
		if (_simpleEvents.TryGetValue(eventName, out var existing))
		{
			_simpleEvents[eventName] = existing + listener;
		}
		else
		{
			_simpleEvents[eventName] = listener;
		}
	}

	public void UnregisterEvent(string eventName, Action listener)
	{
		if (string.IsNullOrEmpty(eventName) || listener == null) return;
		if (_simpleEvents.TryGetValue(eventName, out var existing))
		{
			existing -= listener;
			if (existing == null)
			{
				_simpleEvents.Remove(eventName);
			}
			else
			{
				_simpleEvents[eventName] = existing;
			}
		}
	}

	public void RegisterEvent(string eventName, Action<object> listener)
	{
		if (string.IsNullOrEmpty(eventName) || listener == null) return;
		if (_paramEvents.TryGetValue(eventName, out var existing))
		{
			_paramEvents[eventName] = existing + listener;
		}
		else
		{
			_paramEvents[eventName] = listener;
		}
	}

	public void UnregisterEvent(string eventName, Action<object> listener)
	{
		if (string.IsNullOrEmpty(eventName) || listener == null) return;
		if (_paramEvents.TryGetValue(eventName, out var existing))
		{
			existing -= listener;
			if (existing == null)
			{
				_paramEvents.Remove(eventName);
			}
			else
			{
				_paramEvents[eventName] = existing;
			}
		}
	}

	public void DispatchEvent(string eventName, object args = null)
	{
		if (string.IsNullOrEmpty(eventName)) return;
		if (_simpleEvents.TryGetValue(eventName, out var simpleAction))
		{
			simpleAction?.Invoke();
		}
		if (_paramEvents.TryGetValue(eventName, out var paramAction))
		{
			paramAction?.Invoke(args);
		}
	}
}