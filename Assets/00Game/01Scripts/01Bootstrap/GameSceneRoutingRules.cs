public static class GameSceneRoutingRules
{
    public static bool ReturnHomeAfterWin(int currentPuzzleIndex, bool backHomeTestGroupB = false)
    {
        int firstHomeLevel = backHomeTestGroupB ? 3 : 10;
        return currentPuzzleIndex >= firstHomeLevel - 1;
    }
}
