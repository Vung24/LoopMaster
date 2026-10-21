public enum UndoUnusableReason
{
    None = 0,
    Locked = 1,
    NoMovesToUndo = 2,
    GameplayBusy = 3,
}

public static class UndoUnusableReasonHelper
{
    public static string GetMessage(UndoUnusableReason reason, int unlockLevel)
    {
        switch (reason)
        {
            case UndoUnusableReason.Locked:
                return $"Mở khóa ở Level {unlockLevel}";
            case UndoUnusableReason.NoMovesToUndo:
                return "Không có nước đi nào để hoàn tác";
            case UndoUnusableReason.GameplayBusy:
                return "Vui lòng đợi lượt di chuyển kết thúc";
            default:
                return string.Empty;
        }
    }
}

