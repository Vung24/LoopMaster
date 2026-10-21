using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
public enum JumpHeartDialogSourceScene
{
	HudIn = 0,
	HomeBoosterIn = 1,
	TryAgainBoosterIn = 2
}

public class BaseHeartManager : BaseManager
{
	[HideInInspector] public UnityEvent OnHeartChanged = new UnityEvent();
	[HideInInspector] public UnityEvent OnFreeHeartChanged = new UnityEvent();
	[HideInInspector] public UnityEvent OnInfinityHeartChanged = new UnityEvent();

	public const int DEFAULT_MAX_HEART = 5;
	public const int PASS_VIP_MAX_HEART = 8;
	public const int HEART_REFILL_INTERVAL = 900;

	private readonly List<FreeHeart> _freeHearts = new List<FreeHeart>();

	private const string InfiniteHeartEndTimeKey = GamePrefKeys.InfiniteHeartEnd;
	private const string LastHeartRefillTimeKey = GamePrefKeys.HeartRefillTime;
	private const string HeartCountKey = GamePrefKeys.HeartCount;

	public int LastHeartRefillTime
	{
		get => GamePrefs.GetInt(LastHeartRefillTimeKey, 0);
		set { GamePrefs.SetInt(LastHeartRefillTimeKey, value); GamePrefs.Save(); }
	}

	public int InfiniteHeartEndTime
	{
		get => GamePrefs.GetInt(InfiniteHeartEndTimeKey, 0);
		set { GamePrefs.SetInt(InfiniteHeartEndTimeKey, value); GamePrefs.Save(); }
	}

	private int HeartCount
	{
		get => GamePrefs.GetInt(HeartCountKey, MAX_HEART);
		set { GamePrefs.SetInt(HeartCountKey, value); GamePrefs.Save(); }
	}

	public int InfiniteLeftTime => Mathf.Max(0, InfiniteHeartEndTime - (int)TimeUtils.GetUnixTimestamp());
	public int MAX_HEART { get; private set; } = DEFAULT_MAX_HEART;

	public List<FreeHeart> GetFreeHearts() => _freeHearts;

	public override void Startup()
	{
		MAX_HEART = Managers.Pass != null && Managers.Pass.HasActiveVip ? PASS_VIP_MAX_HEART : DEFAULT_MAX_HEART;
		status = ManagerStatus.Started;
		UpdateHeart();
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

	public void SetPassVipActive(bool active)
	{
		int maximum = active ? PASS_VIP_MAX_HEART : DEFAULT_MAX_HEART;
		if (MAX_HEART == maximum) return;
		MAX_HEART = maximum;
		if (HeartCount > maximum) HeartCount = maximum;
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
		int count = HeartCount;
		int max = MAX_HEART;
		int now = (int)TimeUtils.GetUnixTimestamp();
		if (count < max)
		{
			int last = LastHeartRefillTime;
			if (last == 0)
			{
				LastHeartRefillTime = now;
			}
			else
			{
				int elapsed = now - last;
				if (elapsed >= HEART_REFILL_INTERVAL)
				{
					int refilledHearts = elapsed / HEART_REFILL_INTERVAL;
					HeartCount = Mathf.Min(max, count + refilledHearts);
					LastHeartRefillTime = last + refilledHearts * HEART_REFILL_INTERVAL;
				}
			}
		}
		else
		{
			LastHeartRefillTime = now;
		}
		OnHeartChanged?.Invoke();
		OnInfinityHeartChanged?.Invoke();
	}

	public void FillFullHeart(FillHeartWay type)
	{
		HeartCount = MAX_HEART;
		LastHeartRefillTime = (int)TimeUtils.GetUnixTimestamp();
		OnHeartChanged?.Invoke();
	}

	public int GetHeartRefillTime()
	{
		if (HeartCount >= MAX_HEART) return 0;
		int elapsed = (int)TimeUtils.GetUnixTimestamp() - LastHeartRefillTime;
		if (elapsed < 0) elapsed = 0;
		return Mathf.Max(0, HEART_REFILL_INTERVAL - (elapsed % HEART_REFILL_INTERVAL));
	}

	public bool UseHeart()
	{
		if (IsInfinite()) return true;
		UpdateHeart();
		if (HeartCount > 0)
		{
			if (HeartCount == MAX_HEART)
				LastHeartRefillTime = (int)TimeUtils.GetUnixTimestamp();
			HeartCount--;
			OnHeartChanged?.Invoke();
			return true;
		}
		return false;
	}

	public bool AddHeart()
	{
		HeartCount = Mathf.Min(MAX_HEART, HeartCount + 1);
		if (HeartCount >= MAX_HEART)
			LastHeartRefillTime = (int)TimeUtils.GetUnixTimestamp();
		OnHeartChanged?.Invoke();
		return true;
	}

	public void AddInfinite(int time)
	{
		if (time <= 0) return;
		int now = (int)TimeUtils.GetUnixTimestamp();
		int end = InfiniteHeartEndTime;
		if (end - now < 1)
			end = now;
		InfiniteHeartEndTime = end + time;
		OnInfinityHeartChanged?.Invoke();
	}

	public bool IsHeartFull() => HeartCount >= MAX_HEART;
	public bool IsHeartEnough() => IsInfinite() || HeartCount > 0;
	public bool IsInfinite() => InfiniteHeartEndTime > (int)TimeUtils.GetUnixTimestamp();
	public int GetHeartCount() => HeartCount;
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
		GamePrefs.DeleteKey(InfiniteHeartEndTimeKey);
		GamePrefs.DeleteKey(LastHeartRefillTimeKey);
		GamePrefs.DeleteKey(HeartCountKey);
		GamePrefs.Save();
		MAX_HEART = DEFAULT_MAX_HEART;
		HeartCount = MAX_HEART;
		OnHeartChanged?.Invoke();
		OnFreeHeartChanged?.Invoke();
		OnInfinityHeartChanged?.Invoke();
	}
}
