using System;
using Cysharp.Threading.Tasks;
using Snui;
using Spine.Unity;
using UnityEngine;

public sealed class KeyHunterBoardContext
{
    public BoardController BoardPrefab;
    public ScrewController ScrewPrefab;
    public SkeletonGraphic ScrewdriverPrefab;
    public Key KeyPrefab;
    public Func<string, BlockController> GetBlockPrefab;
    public Func<int, Color> GetColor;
    public Func<int> GetScrewdriverCount;
    public Func<bool> IsMaxScrewdriver;
    public Func<bool> IsExpired;
    public Func<bool> IsCompletedAllBoard;
    public Func<BlockData, int, bool> UseScrewdriver;
    public Func<int> GetCoinPerCell;
    public Func<Transform, string, string, UniTask> ShowToast;
    public Func<string, string, string> Localize;
    public Action<AudioClip> PlayClickAudio;
    public bool IsRevealing;

    public int ScrewdriverCount => GetScrewdriverCount?.Invoke() ?? 0;
    public bool Expired => IsExpired?.Invoke() ?? false;
    public bool CompletedAllBoard => IsCompletedAllBoard?.Invoke() ?? false;
    public bool MaxScrewdriver => IsMaxScrewdriver?.Invoke() ?? false;
}
