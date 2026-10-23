using System;

public interface IWinRewardEconomy
{
    int GetLevelCoins(HardLevel difficulty);
    bool GrantCoins(int coins, bool doubled);
}

// Owns the reward snapshot and claim guard, independently of UI and navigation.
public sealed class WinRewardFlow
{
    private readonly IWinRewardEconomy _economy;
    private GameData _activeData;
    private GameData _rewardData;
    private int _coins;
    private int _version;
    private bool _ready;
    private bool _claimed;

    public WinRewardFlow(IWinRewardEconomy economy)
    {
        _economy = economy ?? throw new ArgumentNullException(nameof(economy));
    }

    public void BeginLevel(GameData data)
    {
        _ready = false;
        if (_activeData != data)
        {
            _version++;
            _rewardData = null;
            _coins = 0;
            _claimed = false;
        }
        _activeData = data;
    }

    public void Prepare(GameData data)
    {
        if (data == null || data != _activeData) return;
        if (_rewardData != data)
        {
            _rewardData = data;
            _coins = _economy.GetLevelCoins(data.GetHardLevel());
            _claimed = false;
        }
        _ready = true;
    }

    public bool HasClaimed(GameData data) => data != null && data == _rewardData && _claimed;

    public int GetCoins(GameData data) => _ready && data != null && data == _activeData &&
        data == _rewardData ? _coins : 0;

    public bool Claim(GameData data, bool doubled, bool isTransitioning, out int coins)
    {
        coins = 0;
        int reward = GetCoins(data);
        if (reward <= 0 || _claimed || isTransitioning || (doubled && reward > int.MaxValue / 2)) return false;
        if (doubled) reward *= 2;
        int version = _version;
        // Inventory callbacks can re-enter claim or initialize another level.
        _claimed = true;
        if (!_economy.GrantCoins(reward, doubled))
        {
            if (version == _version) _claimed = false;
            return false;
        }
        coins = reward;
        return true;
    }
}
