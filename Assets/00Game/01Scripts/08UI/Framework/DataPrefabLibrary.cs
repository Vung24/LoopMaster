using UnityEngine;

namespace Snui
{
    public sealed class DataPrefabLibrary : ScriptableObject
    {
        public GameObject DialogPrefab;
        public GameObject FingerPrefab;
        public GameObject FlyCoinPrefab;
        public GameObject HudContainerPrefab;
        public GameObject SettingsDialogPrefab;
        public GameObject HomeIconNotifierPrefab;
        public GameObject HomeIconProgressPrefab;
        public GameObject SpecialStreakProgressPrefab;
        public GameObject SpecialStreakDialogPrefab;
        public GameObject SpecialStreakInfoDialogPrefab;
        public GameObject SpecialStreakCellPrefab;
        public GameObject SpecialStreakCollectItemPrefab;
        public GameObject SpecialStreakFailBannerPrefab;
        public GameObject LoseSpecialStreakPrefab;
        public GameObject RetryDialogContentPrefab;
        public GameObject LevelBoxMachinePrefab;
        public GameObject LevelBoxDialogContentPrefab;
        public GameObject RewardDialogPrefab;
        public GameObject RewardCellPrefab;
        public Sprite RewardCoinSprite, RewardHeartSprite, RewardInfinityHeartSprite;
        public Sprite RewardUndoSprite, RewardShuffleSprite, RewardPathSprite;
        public Shader LevelBoxSpineShader, LevelBoxSpineAdditiveShader, LevelBoxSpineMultiplyShader, LevelBoxSpineScreenShader;
        public Sprite HudCoinSprite;
        public GameObject NewPlayerTutorialPrefab;
        public GameObject NewPlayerTutorialBluePrefab;
        public GameObject FailTutorialPrefab;
        public GameObject FailTutorialBluePrefab;
        public GameObject LevelHardPanelPrefab;
        public GameObject LevelExpertPanelPrefab;
        public GameObject SpeedUpTutorialPrefab;
        public GameObject ReplayConfirmPrefab;
        public GameObject QuitConfirmPrefab;
        public GameObject BuyToolDialogPrefab;
        public GameObject FeatureUnlockDialogPrefab;
        public Sprite[] CoinSprites;
        private static DataPrefabLibrary current;

        public static DataPrefabLibrary Current
        {
            get
            {
                if (current == null) current = Resources.Load<DataPrefabLibrary>("DataPrefabLibrary");
                return current;
            }
        }

        public GameObject Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            path = path.Replace('\\', '/');
            if (path == "Events/Common/HomeIconNotifier" || path == "UI/Common/HomeIconNotifier") return HomeIconNotifierPrefab;
            if (path == "Events/Common/HomeIconProgress" || path == "UI/Common/HomeIconProgress") return HomeIconProgressPrefab;
            if (path == "Snui/Base/Dialog/Dialog.prefab") return DialogPrefab;
            if (path == "Snui/Common/Prefabs/Finger.prefab") return FingerPrefab;
            if (path == "Snui/Common/Prefabs/FlyCoin.prefab") return FlyCoinPrefab;
            if (path == "Common/Prefab/HudContainer.prefab" || path == "Common/Prefab/HudContainer") return HudContainerPrefab;
            if (path == "Settings/Prefab/SettingsDialog.prefab" || path == "Settings/Prefab/SettingsDialog") return SettingsDialogPrefab;
            if (path.EndsWith("SpecialStreakProgress.prefab")) return SpecialStreakProgressPrefab;
            if (path.EndsWith("SpecialStreakDialog.prefab")) return SpecialStreakDialogPrefab;
            if (path.EndsWith("SpecialStreakInfoDialog.prefab")) return SpecialStreakInfoDialogPrefab;
            if (path.EndsWith("SpecialStreakCell.prefab")) return SpecialStreakCellPrefab;
            if (path.EndsWith("SpecialStreakCollectItem.prefab")) return SpecialStreakCollectItemPrefab;
            if (path.EndsWith("SpecialStreakFailBanner.prefab")) return SpecialStreakFailBannerPrefab;
            if (path.EndsWith("LoseSpecialStreakNode.prefab")) return LoseSpecialStreakPrefab;
            if (path == "Home/LevelBox/Prefab/LevelBoxMachine.prefab" || path == "LevelBox/Prefab/LevelBoxMachine.prefab") return LevelBoxMachinePrefab;
            if (path.TrimStart('/') == "LevelBox/Prefab/LevelBoxDialogContent.prefab" || path == LevelBoxManager.PATH) return LevelBoxDialogContentPrefab;
            if (path == "Snui/Common/Prefabs/RewardDialog.prefab") return RewardDialogPrefab;
            if (path == "Snui/Common/Prefabs/RewardCell.prefab") return RewardCellPrefab;
            if (path == "Game/Prefab/UI/SpeedUpTutorialDialogContent.prefab") return SpeedUpTutorialPrefab;
            if (path == "Game/Prefab/Pause/ReplayDialogContent.prefab") return ReplayConfirmPrefab;
            if (path == "Game/Prefab/Pause/QuitLevelDialogContent.prefab") return QuitConfirmPrefab;
            return Resources.Load<GameObject>(path.EndsWith(".prefab") ? path.Substring(0, path.Length - 7) : path);
        }
        public Sprite ResolveRewardIcon(string path)
        {
            if (path == RewardType.Coin.GetIconPath()) return RewardCoinSprite;
            if (path == RewardType.Heart.GetIconPath()) return RewardHeartSprite;
            if (path == RewardType.InfinityHeart.GetIconPath()) return RewardInfinityHeartSprite;
            if (path == RewardType.Undo.GetIconPath()) return RewardUndoSprite;
            if (path == RewardType.Shuffle.GetIconPath()) return RewardShuffleSprite;
            if (path == RewardType.Path.GetIconPath()) return RewardPathSprite;
            return null;
        }
    }
}
