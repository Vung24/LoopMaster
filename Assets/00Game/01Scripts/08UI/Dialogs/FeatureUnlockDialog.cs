using System;
using System.Linq;
using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Flags]
public enum FeatureType
{
    None = 0, SecretCube = 1, ColorLock = 2, EnterOnly = 4, NumLock = 8,
    GroupColorLock = 16, LimitedColor = 32, AdjacentLock = 64, SingleBox = 128,
    LongBox = 256, CubeNumLock = 512, SurpriseCube = 1024, LinkedCubes = 2048,
    LinkedHiddenCubes = 4096
}

public class FeatureUnlockDialog : BasePopup
{
    [SerializeField] private TMP_Text _titleText1;
    [SerializeField] private TMP_Text _titleText2;
    [SerializeField] private TMP_Text _titleText3;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _fullButton;
    [SerializeField] private Image _featureImage;
    [SerializeField] private AudioClip _showClip;
    [SerializeField] private Sprite _colorLockSprite;
    [SerializeField] private Sprite _enterOnlySprite;
    [SerializeField] private Sprite _secretCubeSprite;
    [SerializeField] private Sprite _numLockSprite;
    [SerializeField] private Sprite _groupColorLockSprite;
    [SerializeField] private Sprite _limitColorSprite;
    [SerializeField] private Sprite _adjacentLockSprite;
    [SerializeField] private Sprite _singleBoxSprite;
    [SerializeField] private Sprite _longSlotSprite;
    [SerializeField] private Sprite _cubeNumLockSprite;
    [SerializeField] private Sprite _surpriseCubeSprite;
    [SerializeField] private Sprite _linkedCubesSprite;
    [SerializeField] private Sprite _linkedHiddenCubesSprite;
    private FeatureType _featureType;

    protected override void Start()
    {
        base.Start();
        if (!GameAudioSettings.EffectsEnabled || _showClip == null) return;
        if (GameAudioPlayer.Current != null) GameAudioPlayer.Current.PlayOneShot(_showClip);
        else AudioSource.PlayClipAtPoint(_showClip, transform.position);
    }

    public void Init(FeatureType type)
    {
        _featureType = type;
        string title = GetTitle();
        foreach (TMP_Text text in new[] { _titleText1, _titleText2, _titleText3 })
            if (text != null) text.text = title;
        if (_descriptionText != null) _descriptionText.text = GetDescription();
        if (_featureImage != null)
        {
            _featureImage.sprite = GetFeatureSprite();
            if (type == FeatureType.LinkedCubes || type == FeatureType.LinkedHiddenCubes)
                _featureImage.preserveAspect = true;
        }
        foreach (Button button in new[] { _continueButton, _fullButton })
        {
            if (button == null) continue;
            button.onClick.RemoveListener(OnClickContinueButton);
            button.onClick.AddListener(OnClickContinueButton);
        }
        if (_continueButton != null) _continueButton.GetComponent<BreathAnim>()?.Play(0f, 3f);
        Snui.Snui_Text.EnsureFontAssets(transform);
    }

    public void InitBooster(string title, string description, Sprite icon)
    {
        _featureType = FeatureType.None;
        foreach (TMP_Text text in new[] { _titleText1, _titleText2, _titleText3 })
            if (text != null) text.text = title;
        if (_descriptionText != null) _descriptionText.text = description;
        if (_featureImage != null)
        {
            _featureImage.sprite = icon;
            _featureImage.preserveAspect = true;
        }
        TMP_Text subTitle = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "SubTitle");
        if (subTitle != null) subTitle.text = "New booster unlocked!";
        foreach (Button button in new[] { _continueButton, _fullButton })
        {
            if (button == null) continue;
            button.onClick.RemoveListener(OnClickContinueButton);
            button.onClick.AddListener(OnClickContinueButton);
        }
        if (_continueButton != null) _continueButton.GetComponent<BreathAnim>()?.Play(0f, 3f);
        Snui.Snui_Text.EnsureFontAssets(transform);
    }

    public Vector3 IconPosition => _featureImage != null
        ? _featureImage.rectTransform.TransformPoint(_featureImage.rectTransform.rect.center) : transform.position;

    public Vector2 IconWorldSize
    {
        get
        {
            if (_featureImage == null) return Vector2.zero;
            RectTransform icon = _featureImage.rectTransform;
            Vector2 size = icon.rect.size;
            Sprite sprite = _featureImage.sprite;
            if (_featureImage.preserveAspect && sprite != null && sprite.rect.width > 0f && sprite.rect.height > 0f)
            {
                float fit = Mathf.Min(size.x / sprite.rect.width, size.y / sprite.rect.height);
                size = sprite.rect.size * fit;
            }
            return new Vector2(size.x * Mathf.Abs(icon.lossyScale.x), size.y * Mathf.Abs(icon.lossyScale.y));
        }
    }

    private void OnClickContinueButton()
    {
        TryClose();
    }

    protected override void OnDestroy()
    {
        if (_continueButton != null) _continueButton.onClick.RemoveListener(OnClickContinueButton);
        if (_fullButton != null) _fullButton.onClick.RemoveListener(OnClickContinueButton);
        base.OnDestroy();
    }

    private Sprite GetFeatureSprite()
    {
        switch (_featureType)
        {
            case FeatureType.SecretCube: return _secretCubeSprite;
            case FeatureType.ColorLock: return _colorLockSprite;
            case FeatureType.EnterOnly: return _enterOnlySprite;
            case FeatureType.NumLock: return _numLockSprite;
            case FeatureType.GroupColorLock: return _groupColorLockSprite;
            case FeatureType.LimitedColor: return _limitColorSprite;
            case FeatureType.AdjacentLock: return _adjacentLockSprite;
            case FeatureType.SingleBox: return _singleBoxSprite;
            case FeatureType.LongBox: return _longSlotSprite;
            case FeatureType.CubeNumLock: return _cubeNumLockSprite;
            case FeatureType.SurpriseCube: return _surpriseCubeSprite;
            case FeatureType.LinkedCubes: return _linkedCubesSprite;
            case FeatureType.LinkedHiddenCubes: return _linkedHiddenCubesSprite;
            default: return null;
        }
    }

    private string GetTitle()
    {
        switch (_featureType)
        {
            case FeatureType.SecretCube: return "Hidden Blocks";
            case FeatureType.ColorLock: return "Color Lock";
            case FeatureType.EnterOnly: return "Receive Only";
            case FeatureType.NumLock: return "Frozen Slot";
            case FeatureType.GroupColorLock: return "Locked Group";
            case FeatureType.LimitedColor: return "Color Slot";
            case FeatureType.AdjacentLock: return "Wooden Slot";
            case FeatureType.SingleBox: return "Single Color Slot";
            case FeatureType.LongBox: return "Long Slot";
            case FeatureType.CubeNumLock: return "Frozen Block";
            case FeatureType.SurpriseCube: return "Surprise Blocks";
            case FeatureType.LinkedCubes: return "Linked Blocks";
            case FeatureType.LinkedHiddenCubes: return "Hidden Linked Blocks";
            default: return "New Feature";
        }
    }

    private string GetDescription()
    {
        switch (_featureType)
        {
            case FeatureType.SecretCube: return "Remove the blocks above to reveal the hidden color.";
            case FeatureType.ColorLock: return "Complete a slot of the matching color to unlock it.";
            case FeatureType.EnterOnly: return "This slot receives blocks and cannot send them out.";
            case FeatureType.NumLock: return "Complete slots to reduce the counter and break the ice.";
            case FeatureType.GroupColorLock: return "Release the matching key to open the locked group.";
            case FeatureType.LimitedColor: return "This slot accepts only its marked color.";
            case FeatureType.AdjacentLock: return "Complete a neighboring slot to unlock this slot.";
            case FeatureType.SingleBox: return "This slot holds one group of blocks.";
            case FeatureType.LongBox: return "Long slots hold six groups of blocks.";
            case FeatureType.CubeNumLock: return "Complete slots to reduce the counter and free the frozen block.";
            case FeatureType.SurpriseCube: return "Complete the surprise color to reveal more blocks.";
            case FeatureType.LinkedCubes: return "Clear above both linked blocks. Tap either one to release them together.";
            case FeatureType.LinkedHiddenCubes: return "Clear the blocks above to reveal the hidden partner. When both linked blocks are clear, tap either to release them together.";
            default: return string.Empty;
        }
    }

    public static FeatureType GetUnseenFeature(GameData data, FeatureType shown)
    {
        if (data == null) return FeatureType.None;
        foreach (FeatureType feature in new[] { FeatureType.LinkedHiddenCubes, FeatureType.SecretCube, FeatureType.ColorLock, FeatureType.EnterOnly,
            FeatureType.NumLock, FeatureType.GroupColorLock, FeatureType.LimitedColor, FeatureType.AdjacentLock,
            FeatureType.SingleBox, FeatureType.LongBox, FeatureType.CubeNumLock, FeatureType.SurpriseCube, FeatureType.LinkedCubes })
        {
            if ((shown & feature) != 0) continue;
            bool present;
            switch (feature)
            {
                case FeatureType.LinkedHiddenCubes: present = data.Boxes.Any(b => b.CubeInfos.Any(c => c.LinkedPairId > 0 && c.IsSecret)); break;
                case FeatureType.SecretCube: present = data.Boxes.Any(b => b.CubeInfos.Any(c => c.IsSecret)); break;
                case FeatureType.ColorLock: present = data.Boxes.Any(b => b.LockColor > 0); break;
                case FeatureType.EnterOnly: present = data.Boxes.Any(b => b.IsEnterOnly); break;
                case FeatureType.NumLock: present = data.Boxes.Any(b => b.NumLock > 0); break;
                case FeatureType.GroupColorLock: present = data.Groups.Any(g => g != null && g.IsLocked); break;
                case FeatureType.LimitedColor: present = data.Boxes.Any(b => b.LimitedColor > 0); break;
                case FeatureType.AdjacentLock: present = data.Boxes.Any(b => b.IsAdjacentLocked); break;
                case FeatureType.SingleBox: present = data.Boxes.Any(b => b.BoxType == BoxType.Single); break;
                case FeatureType.LongBox: present = data.Boxes.Any(b => b.BoxType == BoxType.Long); break;
                case FeatureType.CubeNumLock: present = data.Boxes.Any(b => b.CubeInfos.Any(c => c.NumLock > 0)); break;
                case FeatureType.LinkedCubes: present = data.LinkedPair != null; break;
                default: present = data.Boxes.Any(b => b.CubeInfos.Any(c => CubeData.IsSurpriseColor(c.Type))); break;
            }
            if (present) return feature;
        }
        return FeatureType.None;
    }
}
