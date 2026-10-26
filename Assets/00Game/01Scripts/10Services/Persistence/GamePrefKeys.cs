using System;
using System.Collections.Generic;

// Stable storage names: lowercase group.name, independent of C# property names.
public static class GamePrefKeys
{
    public const string CurrentPuzzle = "progress.current_puzzle";
    public const string NormalPuzzle = "progress.normal_puzzle";
    public const string UnlockedLevel = "progress.unlocked_level";
    public const string CompletedLevel = "progress.completed_level";
    public const string LevelPlayCount = "progress.level_play_count";
    public const string CompletedToday = "progress.completed_today";
    public const string UserSound = "user.sound";
    public const string UserMusic = "user.music";
    public const string UserVibration = "user.vibration";
    public const string ColorBlind = "settings.color_blind";
    public const string RateUs = "user.rate_us";
    public const string FeatureTutorial = "tutorial.features";
    public const string ProfileTutorial = "tutorial.profile";
    public const string SpeedTutorial = "tutorial.speed";
    public const string FailTutorial = "tutorial.fail";
    public const string SpeedUp = "gameplay.speed_up";
    public const string TestFailCount = "debug.puzzle_fail_count";
    public const string BlueSkin = "settings.blue_skin";
    public const string LastRoute = "navigation.last_game_route";
    public const string FreeRevive = "gameplay.free_revive_used";
    public const string SkipTutorial = "debug.skip_tutorial";
    public const string HomePreviousLevel = "home.previous_level";
    public const string HomeTargetLevel = "home.target_level";
    public const string HomeHardLevel = "home.previous_difficulty";
    public const string Music = "settings.music";
    public const string Effects = "settings.effects";
    public const string Vibration = "settings.vibration";
    public const string RemoveAdsOffer = "shop.remove_ads_offer";
    public const string RemovedAds = "shop.ads_removed";
    public const string AnyPurchase = "shop.any_purchase";
    public const string FreeHearts = "hearts.free";
    public const string BonusHeartCount = "hearts.bonus_count";
    public const string HeartCount = "hearts.count";
    public const string HeartRefillTime = "hearts.refill_time";
    public const string InfiniteHeartEnd = "hearts.infinite_end";
    public const string RewardJournal = "rewards.journal";
    public const string RewardReceipts = "rewards.receipts";
    public const string SpecialStreak = "events.special_streak";
    public const string LevelBoxBaseline = "events.level_box.baseline";
    public const string LevelBoxViewed = "events.level_box.viewed_level";
    public const string LevelBoxTutorial = "tutorial.level_box";
    public const string PassState = "events.pass";
    public const string PassTutorial = "tutorial.pass";
    public const string StreakLine = "events.streak_line";
    public const string DailyCheckIn = "events.daily_check_in";
    public const string Coins = "inventory.coin";
    public const string Undo = "inventory.undo";
    public const string Shuffle = "inventory.shuffle";
    public const string Path = "inventory.path";
    public static string Inventory(string item)
    {
        switch (item)
        {
            case "coin": return Coins;
            case "undo": return Undo;
            case "shuffle": return Shuffle;
            case "path": return Path;
            default: return "inventory." + item;
        }
    }
    public static string ShopPurchase(string product) => "shop.purchase." + product;
    public static string JsonFile(string file) => "files." + file;

    private static readonly Dictionary<string, string> LegacyKeys = new Dictionary<string, string>
    {
        { CurrentPuzzle, "offline_CurrentPuzzleIndex" },
        { NormalPuzzle, "offline_NormalPuzzleIndex" },
        { UnlockedLevel, "offline_HighestUnlockedLevel" },
        { CompletedLevel, "offline_HighestCompletedLevel" },
        { LevelPlayCount, "offline_CurrentLevelPlayCount" },
        { CompletedToday, "offline_FinishLevelCountToday" },
        { UserSound, "offline_IsSoundOn" },
        { UserMusic, "offline_IsMusicOn" },
        { UserVibration, "offline_IsVibrate" },
        { ColorBlind, "offline_ColorBlindnessModeSwitch" },
        { RateUs, "offline_RateUsEnabled" },
        { FeatureTutorial, "offline_FeatureTutorialState" },
        { ProfileTutorial, "offline_ProfileTutorialPopped" },
        { SpeedTutorial, "offline_HasShowSpeedUpTutorial" },
        { FailTutorial, "offline_HasShowFailTutorial" },
        { SpeedUp, "offline_IsGameSpeedUp" },
        { TestFailCount, "offline_PuzzleTestFailCount" },
        { BlueSkin, "offline_IsBlueSkin" },
        { LastRoute, "offline_LastRouteEnterGame" },
        { FreeRevive, "offline_free_revive_used" },
        { SkipTutorial, "offline_cheat_SkipTutorial" },
        { HomePreviousLevel, "offline_HomePreviousLevel" },
        { HomeTargetLevel, "offline_HomeTargetLevel" },
        { HomeHardLevel, "offline_HomePreviousHardLevel" },
        { Music, "game_music_enabled" },
        { Effects, "game_effects_enabled" },
        { Vibration, "game_vibration_enabled" },
        { RemoveAdsOffer, "is_open_remove_ad" },
        { RemovedAds, "HasRemovedAds" },
        { AnyPurchase, "offline_shop_any_purchase" },
        { FreeHearts, "offline_free_hearts" },
        { BonusHeartCount, "offline_hearts_count" },
        { HeartCount, "Heart" },
        { HeartRefillTime, "lastRecoverHeartTime" },
        { InfiniteHeartEnd, "infiniteHeartEndTime" },
        { RewardJournal, "offline_reward_journal_v1" },
        { RewardReceipts, "offline_reward_receipts_v1" },
        { SpecialStreak, "offline_special_streak_data_v1" },
        { LevelBoxBaseline, "offline_LevelBoxBaseline_v1" },
        { LevelBoxViewed, "LevelBoxLastViewedLevel" },
        { PassState, "pass_state" },
        { PassTutorial, "pass_tutorial_shown" },
        { StreakLine, "streak_line_data" },
        { DailyCheckIn, "DailyCheckInData" },
        { Coins, "offline_inventory_coin" },
        { Undo, "offline_tool_undo_count" },
        { Shuffle, "offline_tool_shuffle_count" },
        { Path, "offline_tool_path_count" },
    };

    internal static string Legacy(string key)
    {
        if (LegacyKeys.TryGetValue(key, out string legacy)) return legacy;
        if (key.StartsWith("shop.purchase.", StringComparison.Ordinal))
            return "offline_shop_purchase_" + key.Substring("shop.purchase.".Length);
        if (key.StartsWith("files.", StringComparison.Ordinal))
            return "io_" + key.Substring("files.".Length);
        if (key.StartsWith("inventory.", StringComparison.Ordinal))
            return "offline_inventory_" + key.Substring("inventory.".Length);
        if (key.StartsWith("user.", StringComparison.Ordinal))
            return "offline_" + key.Substring("user.".Length);
        return null;
    }

    // Retains the public user-data API for callers passing property names.
    public static string UserData(string key)
    {
        switch (key)
        {
            case "CurrentPuzzleIndex": return CurrentPuzzle;
            case "NormalPuzzleIndex": return NormalPuzzle;
            case "CurrentLevelPlayCount": return LevelPlayCount;
            case "FinishLevelCountToday": return CompletedToday;
            case "IsSoundOn": return UserSound;
            case "IsMusicOn": return UserMusic;
            case "IsVibrate": return UserVibration;
            case "ColorBlindnessModeSwitch": return ColorBlind;
            case "RateUsEnabled": return RateUs;
            case "FeatureTutorialState": return FeatureTutorial;
            case "ProfileTutorialPopped": return ProfileTutorial;
            case "HasShowSpeedUpTutorial": return SpeedTutorial;
            case "HasShowFailTutorial": return FailTutorial;
            case "IsGameSpeedUp": return SpeedUp;
            case "PuzzleTestFailCount": return TestFailCount;
            case "IsBlueSkin": return BlueSkin;
            case "LastRouteEnterGame": return LastRoute;
            default: return "user." + key;
        }
    }
}

