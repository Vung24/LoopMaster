using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Flags]
public enum FeatureType
{
    None = 0, SecretCube = 1, ColorLock = 2, EnterOnly = 4, NumLock = 8,
    GroupColorLock = 16, LimitedColor = 32, AdjacentLock = 64, SingleBox = 128,
    LongBox = 256, CubeNumLock = 512, SurpriseCube = 1024
}

public class FeatureUnlockDialog : MonoBehaviour
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
    private FeatureType _featureType;
    private bool _closed;

    private void Start()
    {
        if (!GameAudioSettings.EffectsEnabled || _showClip == null) return;
        if (GameAudioPlayer.Current != null) GameAudioPlayer.Current.PlayOneShot(_showClip);
        else AudioSource.PlayClipAtPoint(_showClip, transform.position);
    }

    public void Init(FeatureType type)
    {
        _featureType = type;
        _closed = false;
        string title = GetTitle();
        foreach (TMP_Text text in new[] { _titleText1, _titleText2, _titleText3 })
            if (text != null) text.text = title;
        if (_descriptionText != null) _descriptionText.text = GetDescription();
        if (_featureImage != null) _featureImage.sprite = GetFeatureSprite();
        foreach (Button button in new[] { _continueButton, _fullButton })
        {
            if (button == null) continue;
            button.onClick.RemoveListener(OnClickContinueButton);
            button.onClick.AddListener(OnClickContinueButton);
        }
        if (_continueButton != null) _continueButton.GetComponent<BreathAnim>()?.Play(0f, 3f);
        Snui.Snui_Text.EnsureFontAssets(transform);
    }

    private void OnClickContinueButton()
    {
        if (_closed) return;
        _closed = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_continueButton != null) _continueButton.onClick.RemoveListener(OnClickContinueButton);
        if (_fullButton != null) _fullButton.onClick.RemoveListener(OnClickContinueButton);
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
            default: return null;
        }
    }

    // The original localization tables are absent; retain readable offline text.
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
            default: return string.Empty;
        }
    }

    public static FeatureType FindUnseenFeature(GameData data, FeatureType shown)
    {
        if (data == null) return FeatureType.None;
        // Native GameController.IsFeatureTutorial checks these flags in this order.
        foreach (FeatureType feature in new[] { FeatureType.SecretCube, FeatureType.ColorLock, FeatureType.EnterOnly,
            FeatureType.NumLock, FeatureType.GroupColorLock, FeatureType.LimitedColor, FeatureType.AdjacentLock,
            FeatureType.SingleBox, FeatureType.LongBox, FeatureType.CubeNumLock, FeatureType.SurpriseCube })
        {
            if ((shown & feature) != 0) continue;
            bool present;
            switch (feature)
            {
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
                default: present = data.Boxes.Any(b => b.CubeInfos.Any(c => CubeData.IsSurpriseColor(c.Type))); break;
            }
            if (present) return feature;
        }
        return FeatureType.None;
    }
}
