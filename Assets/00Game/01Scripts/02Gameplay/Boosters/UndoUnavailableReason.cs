public enum UndoUnavailableReason
{
    None = 0,
    Locked = 1,
    NoMoves = 2,
}

public static class UndoUnavailableReasonHelper
{
    public static string GetMessage(UndoUnavailableReason reason, int unlockLevel)
    {
        switch (reason)
        {
            case UndoUnavailableReason.Locked:
                return $"Unlock at Level {unlockLevel}!";
            case UndoUnavailableReason.NoMoves:
                return "No moves to undo!";
            default:
                return string.Empty;
        }
    }
}
