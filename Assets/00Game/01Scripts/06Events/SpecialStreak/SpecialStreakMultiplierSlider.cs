using DG.Tweening;
using TMPro;
using UnityEngine;

public enum SpecialStreakMultiplierSliderType { Normal, PreLose, FailBanner }
public sealed class SpecialStreakMultiplierSlider : MonoBehaviour
{
    [SerializeField] private Transform _multipliersNode, _slidingBlock, _bone;
    [SerializeField] private TMP_Text _slidingBlockText;
    public SpecialStreakMultiplierSliderType Type;
    public float StartSlideX = -270;
    private Tween _tween;
    public int MultiplierIndex { get; private set; }
    private struct MultiplierItem
    {
        public Transform Node;
        public TMP_Text Label;
        public Transform Tip;
    }
    private MultiplierItem[] _multiplierItems;

    private void CacheMultiplierItems()
    {
        if (_multiplierItems != null || _multipliersNode == null) return;
        int count = _multipliersNode.childCount;
        _multiplierItems = new MultiplierItem[count];
        for (int i = 0; i < count; i++)
        {
            Transform node = _multipliersNode.GetChild(i);
            _multiplierItems[i] = new MultiplierItem
            {
                Node = node,
                Label = HierarchyReferenceCache.Descendant(node, "Text_" + (i + 1))?.GetComponent<TMP_Text>()
                    ?? node.GetComponentInChildren<TMP_Text>(true),
                Tip = HierarchyReferenceCache.Child(node, "RedTip")
            };
        }
    }

    public void Init(int multiplierIndex)
    {
        _tween?.Kill();
        MultiplierIndex = Mathf.Clamp(multiplierIndex, 0, 4);
        CacheMultiplierItems();
        var manager = SpecialStreakManager.Ensure();
        var values = manager.GetMultipliers();
        for (int i = 0; i < values.Count && _multiplierItems != null && i < _multiplierItems.Length; i++)
        {
            var item = _multiplierItems[i];
            if (item.Label != null) item.Label.text = "x" + values[i];
            if (item.Tip != null) item.Tip.gameObject.SetActive(Type != SpecialStreakMultiplierSliderType.Normal && i < MultiplierIndex);
        }
        if (_slidingBlockText != null) _slidingBlockText.text = "x" + manager.GetMultiplier(MultiplierIndex);
        if (_slidingBlock != null)
        {
            var p = _slidingBlock.localPosition;
            p.x = StartSlideX - StartSlideX * MultiplierIndex * .5f; // Init 0x1287BF0
            _slidingBlock.localPosition = p;
            if (_bone != null) { var bone = _bone.localPosition; bone.x = p.x - 6; _bone.localPosition = bone; }
        }
    }
    public void PlayLoseMultiplierAnim()
    {
        _tween?.Kill();
        if (_slidingBlock == null) return;
        // Native state machine 0x128E348: wait, four warning flashes, recoil, then return to x1.
        var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _tween = sequence;
        sequence.AppendInterval(.5f);
        for (int flash = 0; flash < 4; flash++)
        {
            bool visible = (flash & 1) != 0;
            sequence.AppendCallback(() => SetWarnings(visible));
            sequence.AppendInterval(.3f);
        }
        float start = sequence.Duration();
        if (_slidingBlockText != null)
        {
            Transform label = _slidingBlockText.transform;
            sequence.Insert(start, label.DOScale(new Vector3(.9f, .9f, 1), .17f).SetEase(Ease.OutQuad));
            sequence.Insert(start + .17f, label.DOScale(new Vector3(1.1f, 1.1f, 1), .2f).SetEase(Ease.OutQuad));
            sequence.Insert(start + .37f, label.DOScale(new Vector3(.96f, .96f, 1), .2f).SetEase(Ease.InQuad));
            sequence.InsertCallback(start + .57f, () => { if (_slidingBlockText != null) _slidingBlockText.text = "x1"; });
            sequence.Insert(start + .57f, label.DOScale(1, .17f).SetEase(Ease.InOutSine));
        }
        Vector3 block = _slidingBlock.localPosition;
        sequence.Insert(start, _slidingBlock.DOLocalMoveX(block.x + 10, .17f).SetEase(Ease.OutQuad));
        sequence.Insert(start + .17f, _slidingBlock.DOLocalMoveX(StartSlideX - 8, .4f).SetEase(Ease.InQuad));
        sequence.Insert(start + .57f, _slidingBlock.DOLocalMoveX(StartSlideX, .17f));
        if (_bone != null)
        {
            Vector3 bone = _bone.localPosition, rotation = _bone.localEulerAngles;
            sequence.Insert(start, _bone.DOLocalMoveX(bone.x + 4, .17f).SetEase(Ease.OutQuad));
            sequence.Insert(start, _bone.DOLocalMoveY(bone.y - 3, .17f).SetEase(Ease.OutQuad));
            sequence.Insert(start, _bone.DOLocalRotate(rotation + new Vector3(0, 0, -3), .17f).SetEase(Ease.OutQuad));
            sequence.Insert(start + .17f, _bone.DOLocalMoveX(StartSlideX - 15, .4f).SetEase(Ease.InQuad));
            sequence.Insert(start + .17f, _bone.DOLocalMoveY(bone.y + (Type == SpecialStreakMultiplierSliderType.PreLose ? 20 : 50), .2f).SetEase(Ease.OutQuad));
            sequence.Insert(start + .17f, _bone.DOLocalRotate(rotation + new Vector3(0, 0, 7), .2f).SetEase(Ease.OutQuad));
            sequence.Insert(start + .37f, _bone.DOLocalMoveY(bone.y - 4, .2f).SetEase(Ease.InQuad));
            sequence.Insert(start + .37f, _bone.DOLocalRotate(rotation + new Vector3(0, 0, -3), .2f).SetEase(Ease.InQuad));
            sequence.Insert(start + .57f, _bone.DOLocalMoveX(StartSlideX - 6, .17f).SetEase(Ease.InOutSine));
            sequence.Insert(start + .57f, _bone.DOLocalMoveY(bone.y, .17f).SetEase(Ease.InOutSine));
            sequence.Insert(start + .57f, _bone.DOLocalRotate(rotation, .2f).SetEase(Ease.InOutSine));
        }
        sequence.OnComplete(() => { MultiplierIndex = 0; SetWarnings(false); });
    }
    private void SetWarnings(bool visible)
    {
        if (_multiplierItems == null) CacheMultiplierItems();
        if (_multiplierItems == null) return;
        int limit = Mathf.Min(MultiplierIndex, _multiplierItems.Length);
        for (int i = 0; i < limit; i++)
        {
            var item = _multiplierItems[i];
            if (item.Tip != null) item.Tip.gameObject.SetActive(visible);
            if (item.Label != null) item.Label.color = visible ? Color.white : new Color32(84, 53, 19, 255);
        }
    }
    private void OnDisable() { _tween?.Kill(); _tween = null; }
}
