using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LevelBoxManager : MonoBehaviour, IGameManager
{
    public const string PATH = "LevelBoxDialogContent";
    private const string BaselineKey = "offline_LevelBoxBaseline_v1";
    private const string ViewedKey = "LevelBoxLastViewedLevel";
    public static LevelBoxManager Current { get; private set; }
    public ManagerStatus status { get; private set; } = ManagerStatus.Shutdown;
    private static readonly string[] Fast = { "I","S","IC","U","IP","C","I","C","IS","C","S","C","I","C","P","C","I","C","U" };
    private static readonly string[] Normal = { "I","U","C","I","P","C","S","I","U","C" };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeBeforeScene() => EnsureInitialized();
    public static void EnsureInitialized()
    {
        if (PlayerPrefs.HasKey(BaselineKey)) return;
        PlayerPrefs.SetInt(BaselineKey, FinishedProgress()); PlayerPrefs.Save();
    }
    private static int FinishedProgress() => PlayerPrefs.HasKey("offline_HighestCompletedLevel")
        ? GameProgressStore.HighestCompletedLevel : Math.Max(0, GameProgressStore.HighestUnlockedLevel - 1);
    public int FinishedLevel => FinishedProgress();
    public bool IsUnlock => FinishedLevel >= 4;
    public int LastViewedLevel
    {
        get { EnsureInitialized(); return PlayerPrefs.GetInt(ViewedKey, Math.Max(4, PlayerPrefs.GetInt(BaselineKey))); }
        private set { PlayerPrefs.SetInt(ViewedKey, Math.Max(0,value)); PlayerPrefs.Save(); }
    }
    public void OnViewed() => LastViewedLevel = FinishedLevel;
    public static LevelBoxManager Ensure()
    {
        EnsureInitialized();
        if (Current == null) new GameObject("OfflineLevelBox").AddComponent<LevelBoxManager>();
        return Current;
    }
    private void Awake() => Startup();
    public void Startup()
    {
        if (Current != null && Current != this) { Destroy(this); return; }
        Current = this; EnsureInitialized(); status = ManagerStatus.Started;
        DontDestroyOnLoad(transform.root.gameObject);
    }
    public LevelBoxStageInfo GetCurStageInfo() => GetStageInfo(FinishedLevel);
    public LevelBoxStageInfo GetStageInfo(int finishedLevel)
    {
        long f = Math.Max(4, finishedLevel), start, index;
        int goal;
        string config;
        if (f < 99) { index=(f-4)/5; start=4+index*5; goal=5; config=Fast[(int)index]; }
        else { index=(f-99)/10; start=99+index*10; goal=10; config=Normal[(int)(index%Normal.Length)]; }
        return new LevelBoxStageInfo { Progress=(int)(f-start), Goal=goal, Rewards=Decode(config) };
    }
    private static List<RewardItem> Decode(string config)
    {
        var items=new List<RewardItem>();
        foreach(char code in config)
            items.Add(code=='I' ? new RewardItem(RewardType.InfinityHeart,1800) : code=='C' ? RewardItem.Coin(50) :
                new RewardItem(code=='U' ? RewardType.Undo : code=='S' ? RewardType.Shuffle : RewardType.Path,1));
        return items;
    }
    public static int NextMilestone(int after)
    {
        long f=Math.Max(4,after);
        long next=f<99 ? 9+((f-4)/5)*5 : 109+((f-99)/10)*10;
        return next>int.MaxValue ? 0 : (int)next;
    }
    public int PendingMilestone
    {
        get
        {
            EnsureInitialized();
            var inventory=InventoryManager.Ensure();
            int milestone=NextMilestone(PlayerPrefs.GetInt(BaselineKey));
            while(milestone>0 && milestone<=FinishedLevel)
            {
                if (!inventory.IsRewardClaimed(ClaimId(milestone))) return milestone;
                milestone=NextMilestone(milestone);
            }
            return 0;
        }
    }
    public static string ClaimId(int milestone) => "LevelBox/v1/"+milestone;
    public List<RewardItem> GetMilestoneRewards(int milestone) => GetStageInfo(milestone-1).Rewards;
    public bool TryOpen(int milestone, out RewardClaimResult result)
    {
        result=RewardClaimResult.Failed;
        var inventory=InventoryManager.Ensure();
        if(!inventory.ReplayPendingRewardJournal()) return false;
        if (inventory.IsRewardClaimed(ClaimId(milestone))) { result=RewardClaimResult.AlreadyClaimed; return true; }
        if (milestone<=0 || milestone!=PendingMilestone) return false;
        return inventory.TryClaimRewards(ClaimId(milestone),GetMilestoneRewards(milestone),out result);
    }
    public void ClearData() => ClearOfflineData();
    public static void ClearOfflineData()
    { PlayerPrefs.DeleteKey(BaselineKey); PlayerPrefs.DeleteKey(ViewedKey); PlayerPrefs.Save(); }
    private void OnDestroy() { if(Current==this) Current=null; status=ManagerStatus.Shutdown; }
}

public sealed class LevelBoxStageInfo
{
    public int Progress;
    public int Goal;
    public List<RewardItem> Rewards;
    public RewardItem Reward => Rewards==null || Rewards.Count==0 ? null : Rewards[0];
}
