namespace Snui
{
    public static class PopupAddresses
    {
        public static bool Contains(string key)
        {
            switch (Resolve(key))
            {
                case "BadgeTokenDialog": return true;
                case "BlackFridayBundleDialog": return true;
                case "BuyToolDialog": return true;
                case "BuyToolDialogContent": return true;
                case "ChangeNameDialogContent": return true;
                case "CheatDialog": return true;
                case "ChristmasBundleDialog": return true;
                case "Dialog": return true;
                case "DialogContent": return true;
                case "DialogContentIAP": return true;
                case "DialogRibbon": return true;
                case "DialogTips": return true;
                case "EditLogoDialogContent": return true;
                case "EditTeamDialogContent": return true;
                case "FailDialog": return true;
                case "FailDialogContent": return true;
                case "FeatureUnlockDialog": return true;
                case "FeedbackDialogContent": return true;
                case "HalloweenBundleDialog": return true;
                case "HeartbankDialogContent": return true;
                case "JoinTeamDialogContent": return true;
                case "KickOutMemberConfirmDialogContent": return true;
                case "KickOutMemberDialogContent": return true;
                case "LavaCdDialogContent": return true;
                case "LavaDetailDialog": return true;
                case "LavaOpenDialogContent": return true;
                case "LavaStartDialogContent": return true;
                case "LavaTipDialogContent": return true;
                case "LavaWinDialogContent": return true;
                case "LeaveTeamDialogContent": return true;
                case "LevelBoxDialogContent": return true;
                case "LevelExpertPanel": return true;
                case "LevelHardPanel": return true;
                case "LogPanel": return true;
                case "LoopDialogContentBase": return true;
                case "MoreHeartDialogContent": return true;
                case "NetErrorDialogContent": return true;
                case "PassDetailDialog": return true;
                case "ChristmasPassDetailDialog": return true;
                case "ChristmasPassOpenDialogContent": return true;
                case "ChristmasPassEndDialogContent": return true;
                case "PassEndDialogContent": return true;
                case "PassFinalBonusRewardDialog": return true;
                case "PassInfoDialog": return true;
                case "PassOpenDialogContent": return true;
                case "PassPurchaseDialogContent": return true;
                case "PassPurchaseSuccDialog": return true;
                case "PauseDialogContent": return true;
                case "PreFailDialogContent": return true;
                case "ProfileDialogContent": return true;
                case "PurchaseFailDialogContent": return true;
                case "PushBonusDialogContent": return true;
                case "PushGuideDialog": return true;
                case "PushLevelDialogContent": return true;
                case "QuitLevelDialogContent": return true;
                case "RateDialogContent": return true;
                case "RemoveAdDialog": return true;
                case "RemoveAdsSuccessDialogContent": return true;
                case "ReplayDialogContent": return true;
                case "RestoreFinishDialogContent": return true;
                case "RetryDialogContent": return true;
                case "RewardDialog": return true;
                case "RocketBundleDialog": return true;
                case "SettingsDialog": return true;
                case "SettingsDialogBase": return true;
                case "ShopDialogContent": return true;
                case "SpecialStreakDialog": return true;
                case "SpecialStreakInfoDialog": return true;
                case "SpeedUpTutorialDialogContent": return true;
                case "StreakLineCompleteContent": return true;
                case "StreakLineDialog": return true;
                case "StreakLineInfoDialog": return true;
                case "TeamInfoDialogContent": return true;
                case "TeamMasterInfoDialog": return true;
                case "UpdateDialogContent": return true;
                case "UserMasterInfoDialog": return true;
                case "WinDialog": return true;
                case "WinDialogContent": return true;
                case "WinLogoDialogContent": return true;
                default: return false;
            }
        }

        public static string Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new System.ArgumentException("A popup address is required.", nameof(key));
            key = System.IO.Path.GetFileNameWithoutExtension(key.Replace('\\', '/'));
            switch (key)
            {
                case "LavaCdDialog": return "LavaCdDialogContent";
                case "LavaOpenDialog": return "LavaOpenDialogContent";
                case "LavaStartDialog": return "LavaStartDialogContent";
                case "LavaTipDialog": return "LavaTipDialogContent";
                case "LavaWinDialog": return "LavaWinDialogContent";
                case "MoreHeartDialog": return "MoreHeartDialogContent";
                case "QuitConfirm": return "QuitLevelDialogContent";
                case "ReplayConfirm": return "ReplayDialogContent";
                case "SpeedUpTutorial": return "SpeedUpTutorialDialogContent";
                default: return key;
            }
        }
    }
}
