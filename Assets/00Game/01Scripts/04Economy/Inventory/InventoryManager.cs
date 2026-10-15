using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DefaultExecutionOrder(-1100)]
public partial class InventoryManager : MonoBehaviour, IGameManager
{
	private static readonly string[] BoosterKeys =
		{ InventoryKeys.TOOL_UNDO, InventoryKeys.TOOL_SHUFFLE, InventoryKeys.TOOL_PATH };
	private const int OfflineInitialCount = 3;
	public static InventoryManager Current { get; private set; }
	public ManagerStatus status { get; private set; } = ManagerStatus.Shutdown;
	[HideInInspector] public UnityEvent<string, int> OnInventoryChanged = new UnityEvent<string, int>();
	private bool _isApplyingBooster;

	public static InventoryManager Ensure()
	{
		if (Current == null)
		{
			new GameObject("OfflineInventory").AddComponent<InventoryManager>();
		}
		return Current;
	}

	private void Awake() => Startup();

	public void Startup()
	{
		if (Current != null && Current != this)
		{
			Destroy(this);
			return;
		}
		Current = this;
		if (status == ManagerStatus.Started) return;
		status = ManagerStatus.Initializing;
		DontDestroyOnLoad(transform.root.gameObject);
		if (!ReplayPendingRewardJournal()) { status = ManagerStatus.Started; return; }
		InitializeCounts();
		status = ManagerStatus.Started;
	}

	private void InitializeCounts()
	{
		InventorySettings settings = Resources.Load<InventorySettings>("InventorySettings");
		if (settings != null && settings.InitValues != null)
		{
			var values = new Dictionary<string, int>();
			foreach (SerializableRewardItem item in settings.InitValues)
				if (item != null && IsInventoryKey(item.Key)) values[item.Key] = Math.Max(0, item.Count);
			SetInitValues(values);
		}
		var fallback = new Dictionary<string, int>();
		foreach (string key in BoosterKeys) fallback[key] = OfflineInitialCount;
		SetInitValues(fallback);
	}

	public void SetInitValues(Dictionary<string, int> values)
	{
		if (IsMutationBlocked || values == null) return;
		bool changed = false;
		foreach (KeyValuePair<string, int> item in values)
		{
			if (!IsInventoryKey(item.Key) || GamePrefs.HasKey(CountKey(item.Key))) continue;
			GamePrefs.SetInt(CountKey(item.Key), Math.Max(0, item.Value));
			changed = true;
		}
		if (changed) GamePrefs.Save();
	}

	public static bool IsBoosterKey(string key) => GetToolUnlockLevel(key) > 0;
	private static bool IsInventoryKey(string key) => key == InventoryKeys.COIN || IsBoosterKey(key);
	public static int GetToolUnlockLevel(string key)
	{
		switch (key)
		{
			case InventoryKeys.TOOL_UNDO: return 4;
			case InventoryKeys.TOOL_SHUFFLE: return 7;
			case InventoryKeys.TOOL_PATH: return 9;
			default: return 0;
		}
	}

	public bool IsBoosterUnlocked(string key) => IsBoosterKey(key) &&
		(GameProgressStore.HighestUnlockedLevel >= GetToolUnlockLevel(key) ||
		 (GameController.Current != null && (GameController.Current.IsEditorPlaytest || GameController.Current.CurrentLevelNumber >= GetToolUnlockLevel(key))));
	public bool CanUseBooster(string key) => !IsMutationBlocked && IsBoosterUnlocked(key) && GetAmount(key) > 0;
	public int GetAmount(string key, int def = 0) => IsInventoryKey(key)
		? Math.Max(0, GamePrefs.GetInt(CountKey(key), def)) : Math.Max(0, def);
	private static string CountKey(string key) => GamePrefKeys.Inventory(key);

	public bool Use(string key, int amount = 1, string way = "")
	{
		if (IsMutationBlocked || !IsInventoryKey(key) || amount < 0) return false;
		int count = GetAmount(key);
		if (amount > count) return false;
		if (amount == 0) return true;
		WriteCount(key, count - amount);
		OnInventoryChanged.Invoke(key, -amount);
		return true;
	}

	public bool Add(string key, int amount = 1, string way = "")
	{
		if (IsMutationBlocked || !IsInventoryKey(key) || amount < 0) return false;
		int count = GetAmount(key);
		if (amount > int.MaxValue - count) return false;
		if (amount == 0) return true;
		WriteCount(key, count + amount);
		OnInventoryChanged.Invoke(key, amount);
		return true;
	}

	public bool TryUseBooster(string key, Func<bool> apply)
	{
		if (apply == null || !CanUseBooster(key)) return false;
		int count = GetAmount(key);
		_isApplyingBooster = true;
		try
		{
			if (!apply()) return false;
			WriteCount(key, count - 1);
		}
		finally { _isApplyingBooster = false; }
		OnInventoryChanged.Invoke(key, -1);
		return true;
	}

	private static void WriteCount(string key, int count)
	{
		GamePrefs.SetInt(CountKey(key), count);
		GamePrefs.Save();
	}

	public bool TryPurchaseBooster(string key, int price)
	{
		if (IsMutationBlocked || !IsBoosterUnlocked(key) || price < 0) return false;
		int coins = GetAmount(InventoryKeys.COIN), count = GetAmount(key);
		if (coins < price || count == int.MaxValue) return false;
		GamePrefs.SetInt(CountKey(InventoryKeys.COIN), coins - price);
		GamePrefs.SetInt(CountKey(key), count + 1);
		GamePrefs.Save();
		OnInventoryChanged.Invoke(InventoryKeys.COIN, -price);
		OnInventoryChanged.Invoke(key, 1);
		return true;
	}

	public bool TrySpendCoins(int price, Func<bool> apply)
	{
		if (IsMutationBlocked || price < 0 || apply == null) return false;
		int coins = GetAmount(InventoryKeys.COIN);
		if (coins < price) return false;
		_isApplyingBooster = true;
		try
		{
			if (!apply()) return false;
			WriteCount(InventoryKeys.COIN, coins - price);
		}
		finally { _isApplyingBooster = false; }
		OnInventoryChanged.Invoke(InventoryKeys.COIN, -price);
		return true;
	}

	public void ResetInventory()
	{
		if (IsMutationBlocked) return;
		int coins = GetAmount(InventoryKeys.COIN);
		GamePrefs.DeleteKey(CountKey(InventoryKeys.COIN));
		ResetBoosters();
		GamePrefs.Save();
		OnInventoryChanged.Invoke(InventoryKeys.COIN, GetAmount(InventoryKeys.COIN) - coins);
	}

	public void ResetBoosters()
	{
		if (IsMutationBlocked) return;
		var previous = new Dictionary<string, int>();
		foreach (string key in BoosterKeys)
		{
			previous[key] = GetAmount(key);
			GamePrefs.DeleteKey(CountKey(key));
		}
		InitializeCounts();
		foreach (string key in BoosterKeys) OnInventoryChanged.Invoke(key, GetAmount(key) - previous[key]);
	}

	private void OnDestroy()
	{
		if (Current == this) Current = null;
		status = ManagerStatus.Shutdown;
	}

}
