public sealed class WinRewardEconomy : IWinRewardEconomy
{
    private InventoryManager _inventory;

    public int GetLevelCoins(HardLevel difficulty) => ConfigManager.GetFinishLevelCoinCount(difficulty);

    public bool TryGrantCoins(int coins, bool doubled)
    {
        if (_inventory == null) _inventory = InventoryManager.Ensure();
        return _inventory.Add(InventoryKeys.COIN, coins, doubled ? "win_offline_double" : "win");
    }
}
