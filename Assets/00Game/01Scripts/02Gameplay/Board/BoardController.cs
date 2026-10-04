using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BoardController : MonoBehaviour
{
    private const int BOARD_OFFSET = 100;
    private const string PATH = "KeyHunter/Prefab/Board.prefab";
    public delegate void OnOpenBlockDelegate(BlockController block);
    public OnOpenBlockDelegate OnOpenBlockHandle;
    public UnityAction OnRevealStartAction;
    public UnityAction OnRevealEndAction;
    public Transform Screwdriver;
    public TMP_Text ScrewdriverCount;
    [SerializeField] private GameObject _reveal;
    [SerializeField] private Button _revealBtn;
    private RectTransform _revealRect;
    [SerializeField] private Transform _blockContainer;
    private List<BlockController> _blocks = new List<BlockController>();
    [SerializeField] private SkeletonGraphic _sweep;
    private BoardData _data;
    private bool _isShowingToast;
    private bool _revealRunning;
    private KeyHunterBoardContext _context;

    public static KeyHunterBoardContext DefaultContext { get; set; }
    public static BoardController Create(BoardData data, Transform parent, Vector2 offset)
        => Create(data, parent, offset, DefaultContext);

    public static BoardController Create(BoardData data, Transform parent, Vector2 offset, KeyHunterBoardContext context)
    {
        if (context?.BoardPrefab == null) throw new InvalidOperationException("Missing prefab: " + PATH);
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (context.GetBlockPrefab == null || context.ScrewPrefab == null)
            throw new InvalidOperationException("Key Hunter block and screw prefabs must be supplied.");
        BoardController board = Instantiate(context.BoardPrefab, parent);
        board.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
        board._context = context;
        board.Init(data);
        return board;
    }

    private void Awake()
    {
        if (_reveal == null)
        {
            Transform reveal = HierarchyReferenceCache.Path(transform, "TopPendant", "Reveal");
            if (reveal != null) _reveal = reveal.gameObject;
        }
        Transform revealRoot = _reveal != null ? _reveal.transform : null;
        _revealRect = revealRoot as RectTransform;
        if (_revealBtn == null)
            _revealBtn = HierarchyReferenceCache.Component<Button>(HierarchyReferenceCache.Child(revealRoot, "RevealBtn"));
        if (_sweep == null)
            _sweep = HierarchyReferenceCache.Component<SkeletonGraphic>(HierarchyReferenceCache.Child(revealRoot, "Sweep"));
        if (Screwdriver == null)
            Screwdriver = HierarchyReferenceCache.Path(transform, "TopPendant", "Screwdriver", "Screwdriver");
        if (ScrewdriverCount == null)
            ScrewdriverCount = HierarchyReferenceCache.Component<TMP_Text>(
                HierarchyReferenceCache.Path(transform, "TopPendant", "Screwdriver", "Count"));
        if (_blockContainer == null) _blockContainer = HierarchyReferenceCache.Child(transform, "Blocks");
        if (_reveal != null) _reveal.SetActive(false);
    }

    private void Start() { if (_revealBtn != null) _revealBtn.onClick.AddListener(OnRevealBtnClicked); }

    private void Init(BoardData data)
    {
        _data = data;
        if (transform is RectTransform rect)
            rect.sizeDelta = new Vector2(BlockController.BLOCK_SIZE * data.Cols + BOARD_OFFSET,
                BlockController.BLOCK_SIZE * data.Rows + BOARD_OFFSET);
        CreateBlocks();
        UpdateScrewdriverUI();
        if (_context.IsRevealing) DisableReveal();
    }

    public void CreateBlocks()
    {
        if (_data == null || _context == null) return;
        if (_blockContainer == null) throw new InvalidOperationException("Key Hunter board has no Blocks container.");
        foreach (BlockController block in _blocks.ToArray()) if (block != null) Destroy(block.gameObject);
        _blocks.Clear();
        foreach (BlockData data in _data.Blocks)
        {
            BlockController block = BlockController.Create(data, _blockContainer, _context);
            block.OnUseScrewdriverHandler += OnUseScrewdriverHandler;
            block.OnOpenBlock += OnOpenedBlock;
            block.OnShowUsedFailToast += OnShowUsedFailToast;
            _blocks.Add(block);
        }
        if (_context.IsRevealing) DisableClick();
    }

    private void UpdateScrewdriverUI()
    {
        UpdateScrewdriverCount();
        if (_context.Expired && !_context.MaxScrewdriver)
        {
            if (_reveal != null) _reveal.SetActive(false);
        }
        else EnableReveal();
    }

    private void UpdateScrewdriverCount()
    {
        if (ScrewdriverCount == null || _context == null) return;
        ScrewdriverCount.text = _context.MaxScrewdriver
            ? (_context.Localize?.Invoke("EventKeyHunter", "KeyHunter-Max") ?? "MAX")
            : _context.ScrewdriverCount.ToString();
    }

    public void EnableReveal()
    {
        if (_context == null) return;
        UpdateRevealLayout();
        if (_reveal != null) _reveal.SetActive(true);
        if (_context.ScrewdriverCount <= 0 || _context.IsRevealing) DisableReveal();
        else
        {
            if (_revealBtn != null) _revealBtn.interactable = true;
            if (_sweep != null)
            {
                _sweep.gameObject.SetActive(true);
                _sweep.Initialize(false);
                _sweep.AnimationState?.SetAnimation(0, "animation", true);
            }
        }
    }

    private void DisableReveal()
    {
        if (_revealBtn != null) _revealBtn.interactable = false;
        if (_sweep != null) _sweep.gameObject.SetActive(false);
    }

    private void UpdateRevealLayout()
    {
        if (_data == null || _data.Cols > 4 || _revealRect == null) return;
        if (_revealRect.parent != transform) _revealRect.SetParent(transform, false);
        _revealRect.anchorMin = _revealRect.anchorMax = new Vector2(0.5f, 1f);
        _revealRect.anchoredPosition = new Vector2(0f, 105f);
    }

    private void OnUseScrewdriverHandler()
    {
        UpdateScrewdriverCount();
        if (_context.ScrewdriverCount <= 0) DisableReveal();
    }

    private void OnShowUsedFailToast() { if (!_isShowingToast) ShowUsedFailToastAsync().Forget(); }
    private async UniTask ShowUsedFailToastAsync()
    {
        if (_context?.ShowToast == null) return;
        _isShowingToast = true;
        try { await _context.ShowToast(transform, "EventKeyHunter", _context.Expired ? "KeyHunter-ExprieToast" : "KeyHunter-Toast"); }
        finally { _isShowingToast = false; }
    }

    private void DisableClick() { foreach (BlockController block in _blocks) if (block != null) block.DisableClick(); }
    private void EnableClick() { foreach (BlockController block in _blocks) if (block != null) block.EnableClick(); }
    private void OnRevealBtnClicked()
    {
        if (_revealRunning || _context == null || _context.IsRevealing) return;
        DisableReveal();
        OnRevealStartAction?.Invoke();
        RevealAllBlock().Forget();
    }

    public async UniTask RevealAllBlock()
    {
        if (_revealRunning || _context == null) return;
        _revealRunning = true;
        _context.IsRevealing = true;
        DisableClick();
        try
        {
            // Snapshot because OnOpenedBlock removes entries synchronously.
            List<BlockController> prizeBlocks = _blocks.Where(b => b != null && b.IsPrize())
                .OrderBy(b => b.GetScrewCount()).ToList();
            int index = 0;
            var token = this.GetCancellationTokenOnDestroy();
            while (index < prizeBlocks.Count)
            {
                for (int batch = 0; batch < 2 && index < prizeBlocks.Count; batch++, index++)
                    if (prizeBlocks[index] != null) prizeBlocks[index].AutoScrewing();
                await UniTask.Delay(TimeSpan.FromSeconds(UnityEngine.Random.Range(0.1f, 0.3f)), cancellationToken: token);
                if (_context.ScrewdriverCount <= 0) break;
            }
            if (index >= prizeBlocks.Count) await DropEmptyBlocks();
            if (_context.CompletedAllBoard || _context.ScrewdriverCount <= 0 || prizeBlocks.Count == 0)
            {
                _context.IsRevealing = false;
                EnableClick();
                OnRevealEndAction?.Invoke();
            }
        }
        catch (OperationCanceledException) { _context.IsRevealing = false; }
        catch { _context.IsRevealing = false; EnableClick(); throw; }
        finally { _revealRunning = false; }
    }

    private void OnOpenedBlock(BlockController block) { if (_blocks.Remove(block)) OnOpenBlockHandle?.Invoke(block); }
    public BlockController GetTutorialBlock()
        => _blocks.FirstOrDefault(b => b != null && b.IsSinglePrizeBlock())
            ?? _blocks.Where(b => b != null && b.IsPrize()).OrderBy(b => b.GetScrewCount()).FirstOrDefault();

    public async UniTask DropEmptyBlocks()
    {
        foreach (BlockController block in _blocks.ToArray()) if (block != null) block.PlayDropAnimation();
        await UniTask.Delay(TimeSpan.FromSeconds(0.8f), cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    public Tween PlayDropAnim()
    {
        RectTransform parent = transform.parent as RectTransform;
        if (parent == null) throw new InvalidOperationException("Key Hunter board requires a RectTransform parent.");
        return transform.DOLocalMoveY(parent.rect.y - 500f, 0.67f).SetEase(Ease.InOutQuad)
            .OnComplete(() => Destroy(gameObject)).SetLink(gameObject);
    }

    public Tween PlaySliderInAnim() => DOTween.Sequence()
        .Append(transform.DOLocalMoveY(-45f, 0.67f).SetEase(Ease.InOutQuad))
        .Append(transform.DOLocalMoveY(-30f, 0.26f).SetEase(Ease.InOutQuad)).SetLink(gameObject);

    private void OnDestroy() { if (_revealBtn != null) _revealBtn.onClick.RemoveListener(OnRevealBtnClicked); }
}
