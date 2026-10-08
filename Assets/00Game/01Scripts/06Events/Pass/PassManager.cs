using System;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json.Linq;
using Snui;
using UnityEngine;
using UnityEngine.Events;

public class PassManager : BasePassManager
{
    public override int GetUnlockLevel() => 36;
    public override bool ShouldShowIcon() => true;
    public override bool IsLocked() => GameProgressStore.NextPlayableLevel < GetUnlockLevel();
    public override string GetProductId() => ShopUI.ShopProductCatalog.GoldenPass;
    public override void ShowCoinAnimation(int amount, Vector2 startPos, UnityAction callback)
    {
        var parent = Utils.GetCurrentUiCanvas()?.transform;
        if (parent == null || Snui_CoinHud.Current == null || amount <= 0) { callback?.Invoke(); return; }
        var owner = new GameObject("PassCoinAnimation");
        owner.transform.SetParent(parent, false);
        var coins = owner.AddComponent<ClaimCoinAnim>();
        StartCoroutine(PlayCoins(coins, amount, parent, startPos, callback));
    }
    private IEnumerator PlayCoins(ClaimCoinAnim coins, int count, Transform parent, Vector2 startPos, UnityAction callback)
    {
        yield return coins.Play(count, parent, new Vector3(startPos.x, startPos.y), Snui_CoinHud.Current);
        if (coins != null) Destroy(coins.gameObject);
        callback?.Invoke();
    }
    protected override List<PassStageConfig> GetStageRewardsConfig()
    {
        var asset = Resources.Load<TextAsset>("configjson/PassRewardsConfig");
        if (asset == null) throw new InvalidOperationException("PassRewardsConfig missing.");
        var stages = new List<PassStageConfig>();
        foreach (var row in JArray.Parse(asset.text))
        {
            int points = (int)row["Points"];
            if (points < 0 || (stages.Count > 0 && points == 0)) throw new InvalidOperationException("Invalid pass points.");
            stages.Add(new PassStageConfig { Points = points, FreeRewards = ParseRewards(row["FreeRewards"]), VipRewards = ParseRewards(row["VipRewards"]) });
        }
        return stages;
    }
    private static List<RewardItem> ParseRewards(JToken rows)
    {
        var rewards = new List<RewardItem>();
        foreach (var row in rows)
        {
            var type = RewardType.FromString((string)row["type"]);
            int count = (int)row["count"];
            if (type == RewardType.Unknown || count < 0) throw new InvalidOperationException("Invalid pass reward.");
            rewards.Add(new RewardItem(type, count));
        }
        return rewards;
    }
}
