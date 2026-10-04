using System;
using System.Collections.Generic;
using UnityEngine;

public enum RewardClaimResult { Failed, Claimed, AlreadyClaimed }

public partial class InventoryManager
{
    private const string RewardJournalKey = "offline_reward_journal_v1";
    private const string RewardReceiptsKey = "offline_reward_receipts_v1";
    private bool _rewardTransaction;
    private bool _rewardStoreInvalid;
    private bool IsMutationBlocked => _isApplyingBooster || _rewardTransaction || _rewardStoreInvalid || PlayerPrefs.HasKey(RewardJournalKey);
    [Serializable] private sealed class Receipts { public List<string> ids = new List<string>(); }
    [Serializable] private sealed class Journal
    {
        public int version = 1;
        public string id;
        public string[] keys;
        public int[] before;
        public int[] after;
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Verification-only fault/process interruption injection; never grants anything.
    private static Action<string> RewardCommitCheckpoint;
    private static void Checkpoint(string stage) => RewardCommitCheckpoint?.Invoke(stage);
#else
    private static void Checkpoint(string stage) { }
#endif
    private static Receipts ReadReceipts()
    {
        if (!PlayerPrefs.HasKey(RewardReceiptsKey)) return new Receipts();
        var receipts = JsonUtility.FromJson<Receipts>(PlayerPrefs.GetString(RewardReceiptsKey));
        if (receipts == null || receipts.ids == null || receipts.ids.Exists(string.IsNullOrEmpty))
            throw new InvalidOperationException("Invalid reward receipts; original data retained.");
        return receipts;
    }
    public bool IsRewardClaimed(string claimId)
    {
        try { return ReadReceipts().ids.Contains(claimId); }
        catch (Exception error) { Debug.LogWarning(error.Message); return false; }
    }
    public bool TryClaimRewards(string claimId, IReadOnlyList<RewardItem> items, out RewardClaimResult result)
    {
        result = RewardClaimResult.Failed;
        if (_isApplyingBooster || _rewardTransaction || string.IsNullOrEmpty(claimId) || items == null || items.Count == 0) return false;
        if (!RecoverRewardJournal()) return false;
        _rewardTransaction = true;
        try
        {
            if (ReadReceipts().ids.Contains(claimId)) { result = RewardClaimResult.AlreadyClaimed; return true; }
            var totals = new Dictionary<string, long>();
            foreach (var item in items)
            {
                if (item == null || item.Type == null || item.Count < 0) return false;
                if (item.Type == RewardType.Heart || item.Type == RewardType.InfinityHeart) continue;
                string key = item.Type.ToInventoryKey();
                if (!IsInventoryKey(key)) return false;
                long previous = totals.TryGetValue(key, out long count) ? count : 0;
                long total = previous + item.Count;
                if (total > int.MaxValue) return false;
                totals[key] = total;
            }
            var journal = new Journal { id = claimId, keys = new string[totals.Count], before = new int[totals.Count], after = new int[totals.Count] };
            int index = 0;
            foreach (var pair in totals)
            {
                int before = GetAmount(pair.Key);
                long after = before + pair.Value;
                if (after > int.MaxValue) return false;
                journal.keys[index] = pair.Key; journal.before[index] = before; journal.after[index] = (int)after; index++;
            }
            Checkpoint("before-journal");
            PlayerPrefs.SetString(RewardJournalKey, JsonUtility.ToJson(journal));
            PlayerPrefs.Save();
            Checkpoint("journal-saved");
            CompleteJournal(journal);
            result = RewardClaimResult.Claimed;
            return true;
        }
        catch (Exception error) { Debug.LogWarning("Reward transaction pending/retry: " + error.Message); return false; }
        finally { _rewardTransaction = false; }
    }
    public bool RecoverRewardJournal()
    {
        if (_rewardTransaction) return false;
        _rewardTransaction = true;
        try
        {
            ReadReceipts(); // A corrupt receipt store must never be silently reset.
            if (!PlayerPrefs.HasKey(RewardJournalKey)) { _rewardStoreInvalid=false; return true; }
            var journal = JsonUtility.FromJson<Journal>(PlayerPrefs.GetString(RewardJournalKey));
            if (journal == null || journal.version != 1 || string.IsNullOrEmpty(journal.id) || journal.keys == null ||
                journal.before == null || journal.after == null || journal.keys.Length != journal.before.Length || journal.keys.Length != journal.after.Length)
                throw new InvalidOperationException("Invalid reward journal; original data retained.");
            var unique = new HashSet<string>();
            for (int i = 0; i < journal.keys.Length; i++)
                if (!IsInventoryKey(journal.keys[i]) || !unique.Add(journal.keys[i]) || journal.before[i] < 0 || journal.after[i] < journal.before[i])
                    throw new InvalidOperationException("Invalid reward journal counts; original data retained.");
            CompleteJournal(journal);
            _rewardStoreInvalid=false;
            return true;
        }
        catch (Exception error) { _rewardStoreInvalid=true; Debug.LogWarning("Reward recovery blocked: " + error.Message); return false; }
        finally { _rewardTransaction = false; }
    }
    private void CompleteJournal(Journal journal)
    {
        var receipts = ReadReceipts();
        bool already = receipts.ids.Contains(journal.id);
        if (!already)
        {
            for (int i = 0; i < journal.keys.Length; i++) PlayerPrefs.SetInt(CountKey(journal.keys[i]), journal.after[i]);
            // Allows process interruption between wallet and receipt writes.
            PlayerPrefs.Save(); Checkpoint("balances-saved");
            receipts.ids.Add(journal.id);
            PlayerPrefs.SetString(RewardReceiptsKey, JsonUtility.ToJson(receipts));
            PlayerPrefs.Save(); Checkpoint("receipt-saved");
        }
        PlayerPrefs.DeleteKey(RewardJournalKey); PlayerPrefs.Save();
        if (!already)
            for (int i = 0; i < journal.keys.Length; i++)
                try { OnInventoryChanged.Invoke(journal.keys[i], journal.after[i] - journal.before[i]); }
                catch (Exception error) { Debug.LogWarning("Reward saved; inventory listener failed: " + error.Message); }
    }
    // Called only by the existing explicit full-user-data reset.
    public void ClearRewardData()
    {
        if(_rewardTransaction || _isApplyingBooster) return;
        _rewardStoreInvalid=false;
        PlayerPrefs.DeleteKey(RewardJournalKey); PlayerPrefs.DeleteKey(RewardReceiptsKey); PlayerPrefs.Save();
    }
}
