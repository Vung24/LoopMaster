using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BaseHeartManager : BaseManager
{
	[HideInInspector] public UnityEvent OnHeartChanged = new UnityEvent();
	[HideInInspector] public UnityEvent OnFreeHeartChanged = new UnityEvent();
	[HideInInspector] public UnityEvent OnInfinityHeartChanged = new UnityEvent();

	public const int DEFAULT_MAX_HEART = 5;
	public const int PASS_VIP_MAX_HEART = 8;
	public const int RECOVER_TIME = 900;

	private readonly List<FreeHeart> _freeHearts = new List<FreeHeart>();

	public int LastRecoverHeartTime { get; set; }
	public int InfiniteHeartEndTime { get; set; }
	private int HeartCount { get; set; } = DEFAULT_MAX_HEART;
	public int InfiniteLeftTime => int.MaxValue;
	public int MAX_HEART { get; private set; } = DEFAULT_MAX_HEART;

	public List<FreeHeart> GetFreeHearts() => _freeHearts;

	public override void Startup()
	{
		MAX_HEART = DEFAULT_MAX_HEART;
		HeartCount = MAX_HEART;
		status = ManagerStatus.Started;
		OnHeartChanged?.Invoke();
	}

	public virtual bool IsMoreHeartDialogShowRvBtn() => false;
	public virtual IHeartHud GetCurrentHeartHud() => null;
	public virtual int GetRefillHeartPrice() => 0;

	public void OnPassVipActivated()
	{
		MAX_HEART = PASS_VIP_MAX_HEART;
		HeartCount = MAX_HEART;
		OnHeartChanged?.Invoke();
	}

	public void OnPassVipExpired()
	{
		MAX_HEART = DEFAULT_MAX_HEART;
		HeartCount = MAX_HEART;
		OnHeartChanged?.Invoke();
	}

	public void UpdateHeart()
	{
		HeartCount = MAX_HEART;
		OnHeartChanged?.Invoke();
	}

	public void FillFullHeart(FillHeartWay type)
	{
		HeartCount = MAX_HEART;
		OnHeartChanged?.Invoke();
	}

	public int GetRecoverTime() => 0;
	public bool UseHeart() => true;

	public bool AddHeart()
	{
		HeartCount = MAX_HEART;
		OnHeartChanged?.Invoke();
		return true;
	}

	public void AddInfinite(int time)
	{
		InfiniteHeartEndTime = int.MaxValue;
		OnInfinityHeartChanged?.Invoke();
	}

	public bool IsHeartFull() => true;
	public bool IsHeartEnough() => true;
	public bool IsInfinite() => true;
	public int GetHeartCount() => MAX_HEART;
	public void OnApplicationFocus(bool hasFocus) { if (hasFocus) UpdateHeart(); }

	public void AddFreeHeart(FreeHeartType type, string sourceName = null, int count = 1)
	{
		for (int i = 0; i < Mathf.Max(0, count); i++)
			_freeHearts.Add(new FreeHeart { Type = type, SourceName = sourceName });
		OnFreeHeartChanged?.Invoke();
	}

	public bool UseFreeHeart(int index)
	{
		if (index < 0 || index >= _freeHearts.Count) return false;
		_freeHearts.RemoveAt(index);
		OnFreeHeartChanged?.Invoke();
		return true;
	}

	public int GetTotalFreeHeartCount() => _freeHearts.Count;
	public string GetCellLocalizationKey(FreeHeartType type) => "free_heart_" + type.ToString().ToLowerInvariant();
	public string GetCellContentKey(FreeHeartType type) => GetCellLocalizationKey(type) + "_content";

	public override void ClearData()
	{
		_freeHearts.Clear();
		MAX_HEART = DEFAULT_MAX_HEART;
		HeartCount = MAX_HEART;
		OnHeartChanged?.Invoke();
		OnFreeHeartChanged?.Invoke();
	}
}
